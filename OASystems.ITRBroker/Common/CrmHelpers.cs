using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrmEarlyBound;
using Microsoft.Xrm.Sdk;
using System.Text;
using OASystems.ITRBroker.Models;

namespace OASystems.ITRBroker.Common
{
    public static class CrmHelpers
    {
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
