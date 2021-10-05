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

namespace OASystems.ITRBroker.Job
{
    public class PullEsisMessagesJob : IJob
    {
        private readonly IConfiguration _configuration;

        private CrmServiceContext crmServiceContext;
        private MessageHeadersType messageHeader;

        public PullEsisMessagesJob(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                InitializeVariables(context);

                // Get list of ITR Messages with status reason "Sent to ITR"
                List<OA_itrmessage> itrMessageList = crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.SenttoITR).ToList();

                List<Message> fetchMessageList = new List<Message>();
                List<Message> uploadMessageList = new List<Message>();
                foreach (OA_itrmessage itrMessage in itrMessageList)
                {
                    // Get the message content from the note attachment
                    Message message = GetIncomingMessageFromNoteAttachment(itrMessage.Id);

                    if (EsisHelpers.IsFetchTertiaryPerformanceDataMessage(message.Request))
                    {
                        fetchMessageList.Add(message);
                    }
                    else
                    {
                        uploadMessageList.Add(message);
                    }
                }

                var pulledEsisUploadMessageList = await PullEsisUploadMessage(messageHeader, uploadMessageList);

                foreach (Message message in pulledEsisUploadMessageList)
                {
                    var itrMessage = itrMessageList.Where(x => x.Id == new Guid(message.MessageId)).FirstOrDefault();

                    UpdateItrMessageRecord(message, itrMessage);

                    if (message.Status == (int)Status.PulledFromEsis)
                    {
                        message.Status = (int)Status.ReturnedToTms;
                    }
                    CrmHelpers.CreateIncomingMessageToNoteAttachment(crmServiceContext, message, _configuration["IncomingFileName"]);
                }
            }
            catch (Exception ex)
            {
                // Log error
                var log = ex.Message;
            }
        }

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

        private Message GetIncomingMessageFromNoteAttachment(Guid itrMessageId)
        {
            Annotation note = crmServiceContext.AnnotationSet.Where(x => x.ObjectId.Id == itrMessageId && x.FileName == _configuration["IncomingFileName"]).FirstOrDefault();

            var messageString = Encoding.ASCII.GetString(Convert.FromBase64String(note.DocumentBody));

            var serializer = new XmlSerializer(typeof(Message));
            Message message;
            using (var sr = new StringReader(messageString))
            {
                message = (Message)serializer.Deserialize(sr);
            }

            if (message.MessageLabel == null)
            {
                message.MessageLabel = string.Empty;
            }

            return message;
        }

        private void UpdateItrMessageRecord(Message message, OA_itrmessage itrMessage)
        {
            string errorMessage = string.Empty;
            bool errorsAddressed = false;
            OA_itrmessage_StatusCode? statusCode = null;

            if (message.Status == (int)Status.PulledFromEsis)
            {
                statusCode = OA_itrmessage_StatusCode.Accepted;
                errorsAddressed = true;
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
            }
        }

        public async static Task<List<Message>> PullEsisUploadMessage(MessageHeadersType messageHeader, List<Message> messageList)
        {
            ESIS_TecItrLearnerEventServices_v1Client esisServiceClient = new ESIS_TecItrLearnerEventServices_v1Client();

            GetLearnerEventResultsRequest request = new GetLearnerEventResultsRequest(messageHeader, EsisHelpers.CreateMessageIdListType(messageList));

            DateTime pullStartedOn = DateTime.Now;
            GetLearnerEventResultsResponse response = await esisServiceClient.GetLearnerEventResultsAsync(request);
            DateTime pullCompletedOn = DateTime.Now;

            // List to be returned
            List<Message> messageListReturn = new List<Message>();

            // Dictionary to be returned
            Dictionary<string, string> transactionResultsOutput = new Dictionary<string, string>();

            if (response.LearnerEventResultList != null)
            {
                foreach (var result in response.LearnerEventResultList)
                {
                    Message message = messageList.Where(x => x.PushResponse == result.MessageId).FirstOrDefault();

                    message.PullStartedOn = pullStartedOn;
                    message.PullCompletedOn = pullCompletedOn;

                    if (result.StatusCode == MessageStatusCodeType.RETRIEVED)
                    {
                        if (result.ITRResult == null)
                        {
                            message.Status = (int)Status.Failed;

                            messageListReturn.Add(message);
                        }
                        else if (result.ITRResult.LocalName.ToLower() == SystemMessages.Success.ToLower())
                        {
                            EsisHelpers.ProcessITRSuccessResponse(result, out _, out _, out string transactionResult);
                            message.Status = (int)Status.PulledFromEsis;
                            message.PullResponse = result.ITRResult.ToString();

                            messageListReturn.Add(message);
                        }
                        else
                        {
                            EsisHelpers.ProcessITRFailureResponse(result, out string transactionResult);
                            message.Status = (int)Status.Failed;
                            message.PullResponse = result.ITRResult.ToString();
                            message.Errors = transactionResult;

                            messageListReturn.Add(message);
                        }
                    }
                    else if (result.StatusCode == MessageStatusCodeType.MESSAGE_ERROR)
                    {
                        message.Status = (int)Status.Failed;

                        messageListReturn.Add(message);
                    }
                    else if (result.StatusCode == MessageStatusCodeType.UNKOWN)
                    {
                        message.Status = (int)Status.Unkown;

                        messageListReturn.Add(message);
                    }
                }
            }
            return messageListReturn;
        }
    }
}
