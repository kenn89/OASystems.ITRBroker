using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OASystems.ITRBroker.Job;
using System.IO;
using CrmEarlyBound;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace OASystems.ITRBroker.Test
{
    [TestClass]
    public class UnitTest1
    {
        private readonly IConfiguration _configuration;
        private readonly CrmServiceContext _crmServiceContext;

        public UnitTest1()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(@"appsettings.json", false, false)
                .AddEnvironmentVariables()
                .Build();

            string crmUrl = "";
            string crmClientId = "";
            string crmSecret = "";

            var connectionString = $"AuthType=ClientSecret;Url={crmUrl};ClientId={crmClientId};Secret={crmSecret};";
            var service = new ServiceClient(connectionString);
            _crmServiceContext = new CrmServiceContext(service);
        }

        [TestMethod]
        public void TestMethod1()
        {
            //PushEsisMessagesJob pushEsisMessageJob = new PushEsisMessagesJob(_configuration);
            //var message = pushEsisMessageJob.GetOutgoingMessageFromNoteAttachment(_crmServiceContext, new System.Guid("afb09790-2f61-4cf7-963b-2c774f1d7d2d"));
            //pushEsisMessageJob.CreateIncomingMessageToNoteAttachment(_crmServiceContext, message);
            //_crmServiceContext.SaveChanges();
        }
    }
}
