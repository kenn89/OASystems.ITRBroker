using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Common
{
	public enum BrokerProcess
	{
		PullFromTms,
		PushToEsis,
		PullFromEsis,
		PushToTms
	}

	public enum Status
	{
		PulledFromTms = 0,
		PushedToEsis = 1,
		Failed = 2,
		PulledFromEsis = 3,
		ReturnedToTms = 4,
		Unkown = 5,
		PushedFetchMsgToEsis = 6,
	}

	public enum MessageType
	{
		Empty = 0,
		LearnerEvent = 1,
		FetchTertiaryPerformanceData = 2
	}

	public struct TmsType
	{
		public const string Itomic = "ITOMIC";
		public const string Generic = "GENERIC";
		public const string Pivotal = "PIVOTAL";
	}

	public static class SystemMessages
	{
		public static string RequestMessagePayloadInvalid = "Request Message Payload Invalid";
		public static string UserCanNotBeAuthenticatedAgainstESAA = "User cannot be authenticated against ESAA";
		public static string ViolationOfPrimaryKeyConstraint = "Violation of PRIMARY KEY constraint 'PK_Message'. Cannot insert duplicate key in object 'dbo.Message'.\r\nThe statement has been terminated.";
		public static string OperationHasTimedOut = "The operation has timed out";
		public static string BadGateway = "The remote server returned an error: (502) Bad Gateway.";
		public static string ServiceUnavailable = "The request failed with HTTP status 503: Service Unavailable.";
		public static string Failure = "Failure";
		public static string Success = "Success";
	}
}
