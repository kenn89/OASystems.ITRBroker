using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OASystems.ITRBroker.Common
{
    public static class Utility
    {
        public static string ToXMLString(object obj)
        {
            using (var stringwriter = new System.IO.StringWriter())
            {
                var serializer = new XmlSerializer(obj.GetType());
                serializer.Serialize(stringwriter, obj);
                return stringwriter.ToString();
            }
        }

        public static string GeneratePushEsisInfoText(string itrMessageId, string logText)
        {
            return $"ITR Message ID: {itrMessageId}\n\n{logText}";
        }

        public static string GeneratePullEsisInfoText(string itrMessageId, string esisMessageId, string logText)
        {
            return $"ITR Message ID: {itrMessageId}\nEsis Message ID: {esisMessageId}\n\n{logText}";
        }

        public static string GeneratePullEsisInnerErrorText(string itrMessageId, string esisMessageId, string result, string errorMessage)
        {
            return $"ITR Message ID: {itrMessageId}\nEsis Message ID: {esisMessageId}\nStep: PullEsisUploadMessage\n\nResult:\n{result}\n\nError Details:\n{errorMessage}";
        }
    }
}
