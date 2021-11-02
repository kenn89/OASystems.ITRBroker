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
    public class PushEsisMessages
    {
        private readonly IConfiguration _configuration;
        private readonly CrmServiceContext _crmServiceContext;
        private readonly MessageHeadersType _messageHeaders;
        private readonly ILogger _logger;

        public PushEsisMessages(IConfiguration configuration, CrmServiceContext crmServiceContext, MessageHeadersType messageHeaders, ILogger logger)
        {
            _configuration = configuration;
            _crmServiceContext = crmServiceContext;
            _messageHeaders = messageHeaders;
            _logger = logger;
        }

        public async Task<ILogger> Execute()
        {
            _logger.Info("Executing Push Esis Message...");

            try
            {
                // Get list of ITR Messages with status reason "Queued"
                List<OA_itrmessage> itrMessageList = GetQueuedItrItrMessages();
                _logger.Info($"Retrieved \"Queued\" ITR Messages from CRM.\n{itrMessageList.Count} record(s) retrieved.");

                foreach (OA_itrmessage itrMessage in itrMessageList)
                {
                    // Initialize step variable for logging purpose
                    string step = string.Empty;
                    try
                    {
                        // Get the message content from the note attachment
                        step = "GetOutgoingMessageFromNoteAttachment";
                        Message message = GetOutgoingMessageFromNoteAttachment(itrMessage.Id);
                        _logger.Info(Utility.GeneratePushEsisInfoText(itrMessage.Id.ToString(), "Retrieve Note attachment complete."));

                        // Push the Message to ESIS
                        step = "PushEsisMessage";
                        message = await PushEsisMessage(_messageHeaders, message);
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
                _crmServiceContext.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.Critical(ex.ToString());
            }
            finally
            {
                _logger.Info("Completed Push Esis Message.");
            }

            return _logger;
        }

        #region Private Methods
        private List<OA_itrmessage> GetQueuedItrItrMessages()
        {
            return _crmServiceContext.OA_itrmessageSet.Where(x => x.StatusCode == OA_itrmessage_StatusCode.Queued).ToList();
        }

        private Message GetOutgoingMessageFromNoteAttachment(Guid itrMessageId)
        {
            Message message = CrmHelpers.GetMessageFromNoteAttachment(_crmServiceContext, itrMessageId, _configuration["OutgoingFileName"]);
            message.Status = (int)Status.PulledFromTms;
            message.TmsName = _messageHeaders.TMSUsername;
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
            _crmServiceContext.Detach(itrMessage);
            _crmServiceContext.Attach(itrMessageUpdate);
            _crmServiceContext.UpdateObject(itrMessageUpdate);

            CrmHelpers.CreateIncomingMessageToNoteAttachment(_crmServiceContext, message, _configuration["IncomingFileName"]);
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
