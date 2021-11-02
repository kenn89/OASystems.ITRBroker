using CrmEarlyBound;
using ESIS;
using KissLog;
using Microsoft.Extensions.Configuration;
using OASystems.ITRBroker.Common;
using OASystems.ITRBroker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.BusinessLogic
{
    public class PullEsisMessages
    {
        private readonly IConfiguration _configuration;
        private readonly CrmServiceContext _crmServiceContext;
        private readonly MessageHeadersType _messageHeaders;
        private readonly ILogger _logger;

        public PullEsisMessages(IConfiguration configuration, CrmServiceContext crmServiceContext, MessageHeadersType messageHeaders, ILogger logger)
        {
            _configuration = configuration;
            _crmServiceContext = crmServiceContext;
            _messageHeaders = messageHeaders;
            _logger = logger;
        }

        public async Task<ILogger> Execute()
        {
            _logger.Info("Executing PullEsisMessage...");

            try
            {
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
                _logger.Info("Completed Pull Esis Message.");
            }

            return _logger;
        }

        #region Private Methods
        private List<OA_itrmessage> GetSentToItrItrMessages()
        {
            return _crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.SenttoITR).ToList();
        }

        private void GetOutgoingMessageFromNoteAttachment(List<OA_itrmessage> itrMessageList, out List<Message> fetchMessageList, out List<Message> uploadMessageList)
        {
            fetchMessageList = new List<Message>();
            uploadMessageList = new List<Message>();

            foreach (OA_itrmessage itrMessage in itrMessageList)
            {
                // Get the message content from the note attachment
                Message message = CrmHelpers.GetMessageFromNoteAttachment(_crmServiceContext, itrMessage.Id, _configuration["IncomingFileName"]);

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

                    _crmServiceContext.Detach(itrMessage);
                    _crmServiceContext.Attach(itrMessageUpdate);
                    _crmServiceContext.UpdateObject(itrMessageUpdate);

                    CrmHelpers.CreateIncomingMessageToNoteAttachment(_crmServiceContext, message, _configuration["IncomingFileName"]);
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
                        MessageHeaders = _messageHeaders,
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

            GetLearnerEventResultsRequest request = new GetLearnerEventResultsRequest(_messageHeaders, EsisHelpers.CreateMessageIdListType(messageList));

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
