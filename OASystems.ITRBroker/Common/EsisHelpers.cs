using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using ESIS;
using OASystems.ITRBroker.Models;

namespace OASystems.ITRBroker.Common
{
    public static class EsisHelpers
    {
        public static bool IsFetchTertiaryPerformanceDataMessage(string request)
        {
            try
            {
                var doc = new XmlDocument();

                doc.LoadXml(request);
                if (doc.DocumentElement != null &&
                    (doc.DocumentElement.Name.Equals("FetchEnrolmentDetails", StringComparison.CurrentCultureIgnoreCase) ||
                        doc.DocumentElement.Name.Equals("FetchTrainingAgreementDetails", StringComparison.CurrentCultureIgnoreCase))
                    )
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        public static FetchEventDataMessageType CreateFetchEventDataMessage(string request)
        {
            var fetchEventDataMessage = new FetchEventDataMessageType();
            var doc = new XmlDocument();
            doc.LoadXml(request);
            fetchEventDataMessage.FetchTertiaryPerformanceData = doc.DocumentElement;
            return fetchEventDataMessage;
        }

        public static EventDataMessageType CreateEventDataMessage(string request)
        {
            var eventDataMessage = new EventDataMessageType();
            var doc = new XmlDocument();
            doc.LoadXml(request);
            eventDataMessage.TertiaryPerformanceData = doc.DocumentElement;
            return eventDataMessage;
        }

        public static MessageIdListType CreateMessageIdListType(List<Message> messageList)
        {
            List<string> messageIdList = new List<string>();
            foreach (Message message in messageList)
            {
                messageIdList.Add(message.PushResponse);
            }
            MessageIdListType messageIdListType = new MessageIdListType()
            {
                MessageId = messageIdList.ToArray()
            };
            return messageIdListType;
        }

        public static void ProcessITRSuccessResponse(LearnerEventResultType result, out string programmeNumber, out string programmeVersion, out string transactionResult)
        {
            programmeNumber = string.Empty;
            programmeVersion = string.Empty;
            transactionResult = string.Empty;

            XmlSerializer internalSerializer = new XmlSerializer(typeof(PerformanceDataCaptureMessages.SuccessType));
            PerformanceDataCaptureMessages.SuccessType successMessage = new PerformanceDataCaptureMessages.SuccessType();

            using (StringReader xr = new StringReader(result.ITRResult.OuterXml))
            {
                successMessage = (PerformanceDataCaptureMessages.SuccessType)internalSerializer.Deserialize(xr);
            }

            if (successMessage.PerformanceDataMessage == null || successMessage.PerformanceDataMessage.Count == 0)
            {
                return;
            }

            foreach (var pdm in successMessage.PerformanceDataMessage)
            {

                if (pdm.ObjectIdentifiers != null && pdm.ObjectIdentifiers.Count > 0)
                {

                    foreach (var objectIdentifier in pdm.ObjectIdentifiers)
                    {
                        if (objectIdentifier.Key.ToLower() == "programme number")
                        {
                            programmeNumber = objectIdentifier.Value;
                        }

                        if (objectIdentifier.Key.ToLower() == "programme version number")
                        {
                            programmeVersion = objectIdentifier.Value;
                        }
                    }
                }

                if (!String.IsNullOrEmpty(pdm.TransactionResultCode) && !String.IsNullOrEmpty(pdm.TransactionResultDescription))
                {
                    transactionResult = pdm.TransactionResultCode + ": " + pdm.TransactionResultDescription;
                }
            }
        }

        public static void ProcessITRFailureResponse(LearnerEventResultType result, out string transactionResult)
        {
            transactionResult = string.Empty;

            XmlSerializer internalSerializer = new XmlSerializer(typeof(PerformanceDataCaptureMessages.FailureType));

            PerformanceDataCaptureMessages.FailureType failureMessage = new PerformanceDataCaptureMessages.FailureType();

            using (StringReader xr = new StringReader(result.ITRResult.OuterXml))
            {
                failureMessage = (PerformanceDataCaptureMessages.FailureType)internalSerializer.Deserialize(xr);
            }

            StringBuilder allErrors = new StringBuilder();

            foreach (var pdm in failureMessage.PerformanceDataMessage)
            {
                if (!String.IsNullOrEmpty(pdm.ObjectCategoryDescription))
                {
                    allErrors.AppendLine(pdm.ObjectCategoryDescription);
                }
                else
                {
                    allErrors.AppendLine("No ObjectCategoryDescription");
                }

                if (pdm.ObjectIdentifiers != null && pdm.ObjectIdentifiers.Count > 0)
                {
                    allErrors.AppendLine("ObjectIdentifiers");
                    foreach (var objectIdetifier in pdm.ObjectIdentifiers)
                    {
                        allErrors.AppendLine(objectIdetifier.Key + ": " + objectIdetifier.Value);
                    }
                }
                else
                {
                    allErrors.AppendLine("No ObjectIdentifiers");
                }

                if (!String.IsNullOrEmpty(pdm.TransactionResultCode))
                {
                    allErrors.AppendLine("TransactionResultCode: " + pdm.TransactionResultCode);
                }
                else
                {
                    allErrors.AppendLine("No TransactionResultCode");
                }

                if (!String.IsNullOrEmpty(pdm.TransactionResultDescription))
                {
                    allErrors.AppendLine("TransactionResultDescription: " + pdm.TransactionResultDescription);
                }
                else
                {
                    allErrors.AppendLine("No TransactionResultDescription");
                }

                if (pdm.OperationParameters != null && pdm.OperationParameters.Count > 0)
                {
                    allErrors.AppendLine("OperationParameters");
                    foreach (var operationParameter in pdm.OperationParameters)
                    {
                        allErrors.AppendLine(operationParameter.Key + ": " + operationParameter.Value);
                    }
                }
                else
                {
                    allErrors.AppendLine("No OperationParameters");
                }

            }
            transactionResult = allErrors.ToString();
        }
    }
}
