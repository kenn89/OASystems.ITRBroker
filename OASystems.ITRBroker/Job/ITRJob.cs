using CrmEarlyBound;
using ESIS;
using KissLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using OASystems.ITRBroker.BusinessLogic;
using OASystems.ITRBroker.Models;
using Quartz;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Job
{
    public class ITRJob : IJob
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _dbContext;

        public ITRJob(IConfiguration configuration, DatabaseContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            // IJob datamap parameters are in here
            JobDataMap datamap = context.JobDetail.JobDataMap;

            // Initialize Logger
            ILogger logger = new Logger(url: datamap.GetString("crmUrl"));

            try
            {
                // Update DB fire time
                await UpdateFireTimeInDb(context);

                // Initialize CRM connection
                CrmServiceContext crmServiceContext = InitializeCrmConnection(datamap);

                // Initialize ESIS Message Header
                MessageHeadersType messageHeaders = GenerateMessageHeaders(crmServiceContext);

                // Push Esis Message
                var pushEsisMessage = new PushEsisMessages(_configuration, crmServiceContext, messageHeaders, logger);
                logger = await pushEsisMessage.Execute();

                // Pull Esis Message
                var pullEsisMessage = new PullEsisMessages(_configuration, crmServiceContext, messageHeaders, logger);
                logger = await pullEsisMessage.Execute();
            }
            catch (Exception ex)
            {
                logger.Critical(ex.ToString());
            }
            finally
            {
                Logger.NotifyListeners(logger);
            }
        }

        #region Private Methods
        private CrmServiceContext InitializeCrmConnection(JobDataMap datamap)
        {
            // Retrieve CRM connection parameters
            string crmUrl = datamap.GetString("crmUrl");
            string crmClientId = datamap.GetString("crmClientId");
            string crmSecret = datamap.GetString("crmSecret");

            // CRM Service Context
            var connectionString = $"AuthType=ClientSecret;Url={crmUrl};ClientId={crmClientId};Secret={crmSecret};";
            var service = new ServiceClient(connectionString);
            return new CrmServiceContext(service);
        }

        private async Task UpdateFireTimeInDb(IJobExecutionContext context)
        {
            var iTRJobMetadata = await _dbContext.ITRJobMetadata.Where(x => x.ID == new Guid(context.JobDetail.Key.Name)).FirstOrDefaultAsync();
            iTRJobMetadata.PreviousFireTimeUtc = context.FireTimeUtc.UtcDateTime;
            iTRJobMetadata.NextFireTimeUtc = context.NextFireTimeUtc.HasValue ? context.NextFireTimeUtc.Value.UtcDateTime : null;
            _dbContext.Update(iTRJobMetadata);
            await _dbContext.SaveChangesAsync();
        }

        private MessageHeadersType GenerateMessageHeaders(CrmServiceContext crmServiceContext)
        {
            // ESIS Message Headers Type
            var oasSetting = crmServiceContext.Oas_settingsSet.Where(x => x.Oas_name == _configuration["ITRSettings:Name"]).FirstOrDefault();
            var oasOthers = crmServiceContext.Oas_oasotherSet.Where(x => x.oas_parentsettingid.Id == oasSetting.Id).ToList();
            MessageHeadersType messageHeaders = new MessageHeadersType();
            foreach (var other in oasOthers)
            {
                if (other.Oas_name == _configuration["ITRSettings:ESAAUsername"])
                {
                    messageHeaders.EsaaUsername = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:ESAAPassword"])
                {
                    messageHeaders.EsaaPassword = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:ESAAProviderCode"])
                {
                    messageHeaders.ProviderNumber = other.Oas_Value;
                }
                else if (other.Oas_name == _configuration["ITRSettings:TMSName"])
                {
                    messageHeaders.TMSUsername = other.Oas_Value;
                }
            }

            if (messageHeaders.EsaaUsername == string.Empty || messageHeaders.EsaaPassword == string.Empty || messageHeaders.ProviderNumber == string.Empty || messageHeaders.TMSUsername == string.Empty)
            {
                throw new Exception("Missing Message Header information.");
            }

            return messageHeaders;
        }
        #endregion
    }
}
