using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrmEarlyBound;
using ESIS;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Quartz;
using OASystems.ITRBroker.Models;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using OASystems.ITRBroker.Common;
using Microsoft.Xrm.Sdk;
using KissLog;

namespace OASystems.ITRBroker.Job
{
    public class PullEsisMessagesJob : IJob
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _dbContext;
        private readonly ILogger _logger;

        private CrmServiceContext crmServiceContext;
        private MessageHeadersType messageHeader;

        public PullEsisMessagesJob(IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = new Logger(url: "/PullEsisMessagesJob");
        }

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;
            _logger.Info($"CRM Instance: {datamap.GetString("crmUrl")}\nExecuting Pull Message from Esis Job...");

            try
            {
                InitializeVariables(context);

                // Get list of ITR Messages with status reason "Sent to ITR" from CRM
                List<OA_itrmessage> itrMessageList = GetSentToItrItrMessages();
                _logger.Info($"Retrieved \"Sent to ITR\" ITR Messages from CRM.\n{itrMessageList.Count} record(s) retrieved.");

                // Get two separate lists of "Fetch" and "Upload" Messages
                GetOutgoingMessageFromNoteAttachment(itrMessageList, out List <Message> fetchMessageList, out List<Message> uploadMessageList);
                _logger.Info($"Retrieved {fetchMessageList.Count} \"Fetch\" record(s).\nRetrieved {uploadMessageList.Count} \"Upload\" record(s).");

                // Pull the Message from Esis
                var pulledEsisUploadMessageList = await PullEsisUploadMessage(uploadMessageList);
                var pulledEsisFetchMessageList = await PullEsisFetchMessage(fetchMessageList);
                _logger.Info($"Pull Messages from Esis complete.");

                // Update Status/ Dates / etc. in CRM
                var mergeLists = new List<Message>();
                foreach(var message in pulledEsisFetchMessageList)
                {
                    mergeLists.Add(message);
                }
                foreach(var message in pulledEsisFetchMessageList)
                {
                    mergeLists.Add(message);
                }
                UpdateItrMessageRecord(mergeLists, itrMessageList);
                _logger.Info("Update ITR Message records to CRM complete.");
            }
            catch (Exception ex)
            {
                _logger.Critical(ex.ToString());
            }
            finally
            {
                _logger.Info("Completed Pull Message from Esis Job.");
                Logger.NotifyListeners(_logger);
            }
        }

        #region Private Methods
        private void InitializeVariables(IJobExecutionContext context)
        {
            // Retrieve CRM connection parameters
            JobDataMap datamap = context.JobDetail.JobDataMap;
            string crmUrl = datamap.GetString("crmUrl");
            string crmClientId = datamap.GetString("crmClientId");
            string crmSecret = datamap.GetString("crmSecret");

            // CRM Service Context
            var connectionString = $"AuthType=ClientSecret;Url={crmUrl};ClientId={crmClientId};Secret={crmSecret};";
            var service = new ServiceClient(connectionString);
            crmServiceContext = new CrmServiceContext(service);

            // ESIS Message Headers Type
            var oasSetting = crmServiceContext.Oas_settingsSet.Where(x => x.Oas_name == _configuration["ITRSettings:Name"]).FirstOrDefault();
            var oasOthers = crmServiceContext.Oas_oasotherSet.Where(x => x.oas_parentsettingid.Id == oasSetting.Id).ToList();
            messageHeader = new MessageHeadersType();
            foreach (var other in oasOthers)
            {
                if (other.Oas_name == _configuration["ITRSettings:ESAAUsername"])
                {
                    messageHeader.EsaaUsername = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:ESAAPassword"])
                {
                    messageHeader.EsaaPassword = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:ESAAProviderCode"])
                {
                    messageHeader.ProviderNumber = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:TMSName"])
                {
                    messageHeader.TMSUsername = other.Oas_Value;
                }
            }
        }

        private List<OA_itrmessage> GetSentToItrItrMessages()
        {
            return crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.SenttoITR).ToList();
        }

        private void GetOutgoingMessageFromNoteAttachment(List<OA_itrmessage> itrMessageList, out List<Message> fetchMessageList, out List<Message> uploadMessageList)
        {
            fetchMessageList = new List<Message>();
            uploadMessageList = new List<Message>();

            foreach (OA_itrmessage itrMessage in itrMessageList)
            {
                // Get the message content from the note attachment
                Message message = CrmHelpers.GetMessageFromNoteAttachment(crmServiceContext, itrMessage.Id, _configuration["IncomingFileName"]);

                if (EsisHelpers.IsFetchTertiaryPerformanceDataMessage(message.Request))
                {
                    fetchMessageList.Add(message);
                }
                else
                {
                    uploadMessageList.Add(message);
                }
            }
        }

        private void UpdateItrMessageRecord(List<Message> messageList, List<OA_itrmessage> itrMessageList)
        {
            foreach (Message message in messageList)
            {
                var itrMessage = itrMessageList.Where(x => x.Id == new Guid(message.MessageId)).FirstOrDefault();

                string errorMessage = string.Empty;
                bool errorsAddressed = false;
                OA_itrmessage_StatusCode? statusCode = null;

                if (message.Status == (int)Status.PulledFromEsis)
                {
                    statusCode = OA_itrmessage_StatusCode.Accepted;
                    errorsAddressed = true;

                    message.Status = (int)Status.ReturnedToTms;
                }
                else if (message.Status == (int)Status.Failed || message.Status == (int)Status.Unkown)
                {
                    statusCode = OA_itrmessage_StatusCode.Error;
                    errorMessage = message.Errors;
                    errorsAddressed = false;
                }

                if (statusCode != null)
                {
                    OA_itrmessage itrMessageUpdate = new OA_itrmessage()
                    {
                        Id = new Guid(message.MessageId),
                        OA_ResponseReceived = message.PullCompletedOn,
                        StatusCode = statusCode,
                        OA_Errors = errorMessage,
                        OA_Allerrorsaddressed = errorsAddressed
                    };

                    crmServiceContext.Detach(itrMessage);
                    crmServiceContext.Attach(itrMessageUpdate);
                    crmServiceContext.UpdateObject(itrMessageUpdate);

                    CrmHelpers.CreateIncomingMessageToNoteAttachment(crmServiceContext, message, _configuration["IncomingFileName"]);
                }
            }
        }

        private async Task<List<Message>> PullEsisFetchMessage(List<Message> messageLists)
        {
            ESIS_TecItrLearnerEventServices_v1Client esisServiceClient = new ESIS_TecItrLearnerEventServices_v1Client();

            // List to be returned
            List<Message> messageListReturn = new List<Message>();

            foreach (var message in messageLists)
            {
                try
                {
                    FetchLearnerEventDataResultsRequest request = new FetchLearnerEventDataResultsRequest()
                    {
                        MessageHeaders = messageHeader,
                        MessageId = message.PushResponse
                    };

                    message.PullStartedOn = DateTime.Now;
                    FetchLearnerEventDataResultsResponse response = await esisServiceClient.FetchLearnerEventDataResultsAsync(request);
                    message.PullCompletedOn = DateTime.Now;

                    _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Pulled \"Fetch\" Message Type from Esis Response:\n{response.ToString()}"));

                    FetchLearnerEventDataResultType result = response.FetchLearnerEventDataResult;
                    if (result.StatusCode == MessageStatusCodeType.RETRIEVED)
                    {
                        if (result.LearnerEventDataResult != null)
                        {
                            message.Status = (int)Status.PulledFromEsis;
                            message.PullResponse = result.LearnerEventDataResult.ToString();

                            messageListReturn.Add(message);
                            _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                        }
                        else
                        {
                            message.Status = (int)Status.Failed;

                            messageListReturn.Add(message);
                            _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\". However \"LearnerEventDataResult\" is null, hence status is saved as Failed."));
                        }
                    }
                    else if (result.StatusCode == MessageStatusCodeType.MESSAGE_ERROR)
                    {
                        message.Status = (int)Status.Failed;

                        messageListReturn.Add(message);
                        _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                    }
                    else if (result.StatusCode == MessageStatusCodeType.UNKOWN)
                    {
                        message.Status = (int)Status.Unkown;

                        messageListReturn.Add(message);
                        _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                    }
                    else
                    {
                        _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Status Code \"{result.StatusCode}\" did not satisfy the condition to process the Message further."));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"ITR Message ID: {message.MessageId}\nEsis Message ID: {message.PushResponse}\nStep: PullEsisFetchMessage\n\nError Details:\n{ex.ToString()}");
                }
            }

            return messageListReturn;
        }

        private async Task<List<Message>> PullEsisUploadMessage(List<Message> messageList)
        {
            ESIS_TecItrLearnerEventServices_v1Client esisServiceClient = new ESIS_TecItrLearnerEventServices_v1Client();

            GetLearnerEventResultsRequest request = new GetLearnerEventResultsRequest(messageHeader, EsisHelpers.CreateMessageIdListType(messageList));

            DateTime pullStartedOn = DateTime.Now;
            GetLearnerEventResultsResponse response = await esisServiceClient.GetLearnerEventResultsAsync(request);
            DateTime pullCompletedOn = DateTime.Now;

            _logger.Info($"Pulled \"Upload\" Message Type from Esis Response:\n{response.ToString()}");

            // List to be returned
            List<Message> messageListReturn = new List<Message>();

            if (response.LearnerEventResultList != null)
            {
                foreach (LearnerEventResultType result in response.LearnerEventResultList)
                {
                    Message message = messageList.Where(x => x.PushResponse == result.MessageId).FirstOrDefault();
                    try
                    {
                        _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Pulled Upload from Esis Result:\n{result.ToString()}."));

                        message.PullStartedOn = pullStartedOn;
                        message.PullCompletedOn = pullCompletedOn;

                        if (result.StatusCode == MessageStatusCodeType.RETRIEVED)
                        {
                            if (result.ITRResult == null)
                            {
                                message.Status = (int)Status.Failed;

                                messageListReturn.Add(message);
                                _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\". However \"LearnerEventDataResult\" is null, hence status is saved as Failed."));
                            }
                            else if (result.ITRResult.LocalName.ToLower() == SystemMessages.Success.ToLower())
                            {
                                EsisHelpers.ProcessITRSuccessResponse(result, out _, out _, out string transactionResult);
                                message.Status = (int)Status.PulledFromEsis;
                                message.PullResponse = result.ITRResult.ToString();

                                messageListReturn.Add(message);
                                _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                            }
                            else
                            {
                                EsisHelpers.ProcessITRFailureResponse(result, out string transactionResult);
                                message.Status = (int)Status.Failed;
                                message.PullResponse = result.ITRResult.ToString();
                                message.Errors = transactionResult;

                                messageListReturn.Add(message);
                                _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\". However \"ITR Result\" stated that the upload has Failed, hence status is savead as Failed."));
                            }
                        }
                        else if (result.StatusCode == MessageStatusCodeType.MESSAGE_ERROR)
                        {
                            message.Status = (int)Status.Failed;

                            messageListReturn.Add(message);
                            _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                        }
                        else if (result.StatusCode == MessageStatusCodeType.UNKOWN)
                        {
                            message.Status = (int)Status.Unkown;

                            messageListReturn.Add(message);
                            _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Successfully processed Message with Status Code \"{result.StatusCode}\"."));
                        }
                        else
                        {
                            _logger.Info(Utility.GeneratePullEsisInfoText(message.MessageId, message.PushResponse, $"Status Code \"{result.StatusCode}\" did not satisfy the condition to process the Message further."));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"ITR Message ID: {message.MessageId}\nEsis Message ID: {result.MessageId}\nStep: PullEsisUploadMessage\n\nResult:\n{result.ToString()}\n\nError Details:\n{ex.ToString()}");
                    }
                }
            }
            return messageListReturn;
        }
        #endregion
    }
}
