using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Models
{
    public class Message
    {
		public long RowId { get; set; }

		public string MessageId { get; set; }

		public string TmsName { get; set; }

		public string MessageLabel { get; set; }

		public string Request { get; set; }

		public string PushResponse { get; set; }

		public string PullResponse { get; set; }

		public int Status { get; set; }

		public DateTime? PostponeUntil { get; set; }

		public DateTime? ReceivedFromTmsOn { get; set; }

		public DateTime? PushStartedOn { get; set; }

		public DateTime? PushCompletedOn { get; set; }

		public DateTime? PullStartedOn { get; set; }

		public DateTime? PullCompletedOn { get; set; }

		public DateTime? ReturnedToTmsOn { get; set; }

		public string CreatedBy { get; set; }

		public DateTime? CreatedOn { get; set; }

		public DateTime? ProcessedOn { get; set; }

		public DateTime? RequestType { get; set; }

		public DateTime? ResponseType { get; set; }

		public string Comment { get; set; }

		public string Errors { get; set; }
	}
}
