using CrmEarlyBound;
using ESIS;
using KissLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using OASystems.ITRBroker.Common;
using OASystems.ITRBroker.Models;
using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OASystems.ITRBroker.Job
{
    public class PushEsisMessagesJob : IJob
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _dbContext;
        private readonly ILogger _logger;

        private CrmServiceContext crmServiceContext;
        private MessageHeadersType messageHeader;

        public PushEsisMessagesJob(IConfiguration configuration, DatabaseContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = new Logger(url: "/PushEsisMessagesJob");
        }

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;
            _logger.Info($"CRM Instance: {datamap.GetString("crmUrl")}\nExecuting Push Message to Esis Job...");

            try
            {
                await UpdateFireTimeInDb(context);

                InitializeVariables(datamap);

                // Get list of ITR Messages with status reason "Queued"
                List<OA_itrmessage> itrMessageList = GetQueuedItrItrMessages();
                _logger.Info($"Retrieved \"Queued\" ITR Messages from CRM.\n{itrMessageList.Count} record(s) retrieved.");

                foreach (OA_itrmessage itrMessage in itrMessageList)
                {
                    string step = string.Empty;
                    try
                    {
                        // Get the message content from the note attachment
                        step = "GetOutgoingMessageFromNoteAttachment";
                        Message message = GetOutgoingMessageFromNoteAttachment(itrMessage.Id);
                        _logger.Info(Utility.GeneratePushEsisInfoText(itrMessage.Id.ToString(), "Retrieve Note attachment complete."));

                        // Push the Message to ESIS
                        step = "PushEsisMessage";
                        message = await PushEsisMessage(messageHeader, message);
                        _logger.Info(Utility.GeneratePushEsisInfoText(itrMessage.Id.ToString(), "Push Messages to Esis complete."));

                        // Update Status/Dates/etc. in CRM
                        step = "UpdateItrMessageRecord";
                        UpdateItrMessageRecord(message, itrMessage);
                        _logger.Info(Utility.GeneratePushEsisInfoText(itrMessage.Id.ToString(), "Update ITR Message record to CRM complete."));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"ITR Message ID: {itrMessage.Id.ToString()}\nStep: {step}\n\nError Details:\n{ex.ToString()}");
                    }
                }
                crmServiceContext.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.Critical(ex.ToString());
            }
            finally
            {
                _logger.Info("Completed Push Message to Esis Job.");
                Logger.NotifyListeners(_logger);
            }
        }

        #region Private Methods
        private async Task UpdateFireTimeInDb(IJobExecutionContext context)
        {
            var iTRJobMetadata = await _dbContext.ITRJobMetadata.Where(x => x.ID == new Guid(context.JobDetail.Key.Name)).FirstOrDefaultAsync();
            iTRJobMetadata.PreviousFireTimeUtc = context.FireTimeUtc.UtcDateTime;
            iTRJobMetadata.NextFireTimeUtc = context.NextFireTimeUtc.HasValue ? context.NextFireTimeUtc.Value.UtcDateTime : null;
            _dbContext.Update(iTRJobMetadata);
            await _dbContext.SaveChangesAsync();
        }

        private void InitializeVariables(JobDataMap datamap)
        {
            // Retrieve CRM connection parameters
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
                //else if (other.Oas_name == _configuration["ITRSettings:TMSName"])
                //{
                //    messageHeader.TMSUsername = other.Oas_Value;
                //}
            }

            if (messageHeader.EsaaUsername == string.Empty || messageHeader.EsaaPassword == string.Empty || messageHeader.ProviderNumber == string.Empty || messageHeader.TMSUsername == string.Empty)
            {
                throw new Exception("Message Header information mising.");
            }
        }

        private List<OA_itrmessage> GetQueuedItrItrMessages()
        {
            return crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.Queued).ToList();
        }

        private Message GetOutgoingMessageFromNoteAttachment(Guid itrMessageId)
        {
            Message message = CrmHelpers.GetMessageFromNoteAttachment(crmServiceContext, itrMessageId, _configuration["OutgoingFileName"]);
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

            CrmHelpers.CreateIncomingMessageToNoteAttachment(crmServiceContext, message, _configuration["IncomingFileName"]);
        }

        public async Task<Message> PushEsisMessage(MessageHeadersType messageHeader, Message message)
        {
            ESIS_TecItrLearnerEventServices_v1Client esisServiceClient = new ESIS_TecItrLearnerEventServices_v1Client();

            if (EsisHelpers.IsFetchTertiaryPerformanceDataMessage(message.Request))
            {
                FetchEventDataMessageType fetchEventDataMessage = EsisHelpers.CreateFetchEventDataMessage(message.Request);
                FetchLearnerEventDataRequest request = new FetchLearnerEventDataRequest(messageHeader, fetchEventDataMessage);

                message.PushStartedOn = DateTime.Now;
                FetchLearnerEventDataResponse response = await esisServiceClient.FetchLearnerEventDataAsync(request);
                _logger.Info($"Pushed to Esis Response: {response.ToString()}");

                if (Guid.TryParse(response.MessageId, out _))
                {
                    message.Status = (int)Status.PushedFetchMsgToEsis;
                    message.PushCompletedOn = DateTime.Now;
                    message.PushResponse = response.MessageId;
                }
                else
                {
                    throw new Exception($"Esis responded Message ID is not in Guid format. Message returned: {response.MessageId}");
                }
            }
            else
            {
                EventDataMessageType eventDataMessage = EsisHelpers.CreateEventDataMessage(message.Request);
                message.PushStartedOn = DateTime.Now;
                UploadLearnerEventDataResponse response = await esisServiceClient.UploadLearnerEventDataAsync(messageHeader, eventDataMessage);
                _logger.Info($"Pushed to Esis Response: {response.ToString()}");

                if (Guid.TryParse(response.MessageId, out _))
                {
                    message.Status = (int)Status.PushedToEsis;
                    message.PushCompletedOn = DateTime.Now;
                    message.PushResponse = response.MessageId;
                }
                else
                {
                    throw new Exception($"Esis responded Message ID is not in Guid format. Message returned: {response.MessageId}");
                }
            }
            return message;
        }
        #endregion
    }
}
