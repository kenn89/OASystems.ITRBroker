using CrmEarlyBound;
using Microsoft.Xrm.Sdk;
using OASystems.ITRBroker.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace OASystems.ITRBroker.Common
{
    public static class CrmHelpers
    {
        public static Message GetMessageFromNoteAttachment(CrmServiceContext crmServiceContext, Guid itrMessageId, string fileName)
        {
            Annotation note = crmServiceContext.AnnotationSet.Where(x => x.ObjectId.Id == itrMessageId && x.FileName == fileName).FirstOrDefault();

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

        public static void CreateIncomingMessageToNoteAttachment(CrmServiceContext crmServiceContext, Message message, string fileName)
        {
            Annotation note = new Annotation()
            {
                ObjectId = new EntityReference(OA_itrmessage.EntityLogicalName, new Guid(message.MessageId)),
                Subject = "Returned by ITR Broker",
                FileName = fileName,
                DocumentBody = Convert.ToBase64String(Encoding.ASCII.GetBytes(Utility.ToXMLString(message)))
            };

            // Check if note already exists in CRM, create a new Note if it doesn't exists, otherwise update the existing note
            var existingNote = crmServiceContext.AnnotationSet.Where(x => x.ObjectId.Id == new Guid(message.MessageId) && x.FileName == fileName).FirstOrDefault();
            if (existingNote == default)
            {
                crmServiceContext.AddObject(note);
            }
            else
            {
                note.Id = existingNote.Id;
                crmServiceContext.Detach(existingNote);
                crmServiceContext.AddObject(note);
            }
        }
    }
}
