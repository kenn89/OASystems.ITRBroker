using Microsoft.EntityFrameworkCore;
using Microsoft.PowerPlatform.Dataverse.Client;
using OASystems.ITRBroker.Models;
using Quartz;
using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;
using CrmEarlyBound;
using ESIS;
using System.Xml;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using OASystems.ITRBroker.Common;
using Microsoft.Xrm.Sdk;

namespace OASystems.ITRBroker.Job
{
    public class PushEsisMessagesJob : IJob
    {
        private readonly IConfiguration _configuration;

        private CrmServiceContext crmServiceContext;
        private MessageHeadersType messageHeader;

        public PushEsisMessagesJob(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                InitializeVariables(context);

                // Get list of ITR Messages with status reason "Queued"
                List<OA_itrmessage> itrMessageList = crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.Queued).ToList();

                foreach (OA_itrmessage itrMessage in itrMessageList)
                {
                    // Get the message content from the note attachment
                    Message message = GetOutgoingMessageFromNoteAttachment(itrMessage.Id);

                    // Push the Message to ESIS
                    message = await PushEsisMessage(messageHeader, message);

                    // Update Status/Dates/etc. in CRM
                    UpdateItrMessageRecord(message, itrMessage);

                    // Crate Note with incoming.xml attachment
                    CrmHelpers.CreateIncomingMessageToNoteAttachment(crmServiceContext, message, _configuration["IncomingFileName"]);
                }
                crmServiceContext.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log error
                var log = ex.Message;
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

        private Message GetOutgoingMessageFromNoteAttachment(Guid itrMessageId)
        {
            Annotation note = crmServiceContext.AnnotationSet.Where(x => x.ObjectId.Id == itrMessageId && x.FileName == _configuration["OutgoingFileName"]).FirstOrDefault();

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
            message.Status = (int)Status.PulledFromTms;
            message.TmsName = messageHeader.TMSUsername;
            message.ProcessedOn = DateTime.Now;
            message.ReceivedFromTmsOn = DateTime.Now;

            return message;
        }

        private void UpdateItrMessageRecord(Message message, OA_itrmessage itrMessage)
        {
            OA_itrmessage itrMessageUpdate = new OA_itrmessage()
            {
                Id = new Guid(message.MessageId),
                OA_Sent = message.PushStartedOn,
                StatusCode = OA_itrmessage_StatusCode.SenttoITR
            };
            crmServiceContext.Detach(itrMessage);
            crmServiceContext.Attach(itrMessageUpdate);
            crmServiceContext.UpdateObject(itrMessageUpdate);
        }

        public static async Task<Message> PushEsisMessage(MessageHeadersType messageHeader, Message message)
        {
            ESIS_TecItrLearnerEventServices_v1Client esisServiceClient = new ESIS_TecItrLearnerEventServices_v1Client();

            if (EsisHelpers.IsFetchTertiaryPerformanceDataMessage(message.Request))
            {
                FetchEventDataMessageType fetchEventDataMessage = EsisHelpers.CreateFetchEventDataMessage(message.Request);
                FetchLearnerEventDataRequest request = new FetchLearnerEventDataRequest(messageHeader, fetchEventDataMessage);

                message.PushStartedOn = DateTime.Now;
                FetchLearnerEventDataResponse response = await esisServiceClient.FetchLearnerEventDataAsync(request);

                if (Guid.TryParse(response.MessageId, out _))
                {
                    message.Status = (int)Status.PushedFetchMsgToEsis;
                    message.PushCompletedOn = DateTime.Now;
                    message.PushResponse = response.MessageId;
                }
                else
                {
                    // Log and throw error?
                }
            }
            else
            {
                EventDataMessageType eventDataMessage = EsisHelpers.CreateEventDataMessage(message.Request);
                message.PushStartedOn = DateTime.Now;
                UploadLearnerEventDataResponse response = await esisServiceClient.UploadLearnerEventDataAsync(messageHeader, eventDataMessage);
                if (Guid.TryParse(response.MessageId, out _))
                {
                    message.Status = (int)Status.PushedToEsis;
                    message.PushCompletedOn = DateTime.Now;
                    message.PushResponse = response.MessageId;
                }
                else
                {
                    // Log and throw error?
                }
            }
            return message;
        }
        #endregion
    }
}
