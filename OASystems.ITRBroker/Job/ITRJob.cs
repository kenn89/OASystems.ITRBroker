using Microsoft.EntityFrameworkCore;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using OASystems.ITRBroker.Models;
using Quartz;
using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Job
{
    public class ITRJob : IJob
    {
        private readonly DatabaseContext _dbContext;

        public ITRJob(DatabaseContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                // Retrieve CRM connection parameters
                JobDataMap datamap = context.JobDetail.JobDataMap;
                string crmUrl = datamap.GetString("crmUrl");
                string crmClientID = datamap.GetString("crmClientID");
                string crmSecret = datamap.GetString("crmSecret");

                var connectionString = $"AuthType=ClientSecret;Url={crmUrl};ClientId={crmClientID};Secret={crmSecret};";
                var serviceClient = new ServiceClient(connectionString);

                var contactCollection = serviceClient.RetrieveMultiple(new QueryExpression("contact")
                {
                    ColumnSet = new ColumnSet(true),
                    TopCount = 1
                });

                var testContactName = contactCollection.Entities.First().Attributes["fullname"];

                var client = new RestClient("https://webhook.site/ec7ce0cc-51a0-4047-8c3f-486616d20629");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "text/plain");
                var body = testContactName;
                request.AddParameter("text/plain", body, ParameterType.RequestBody);
                IRestResponse response = await client.ExecuteAsync(request);
            }
            catch
            {
                // Log error to db
            }
            finally
            {
                await LogFireTimeToDb(context);
            }
        }

        private async Task LogFireTimeToDb(IJobExecutionContext context)
        {
            ITRJobMetadata iTRJobMetadata = await _dbContext.ITRJobMetadata.Where(x => x.ID == new Guid(context.JobDetail.Key.Name)).FirstOrDefaultAsync();
            iTRJobMetadata.PreviousFireTimeUtc = context.FireTimeUtc.UtcDateTime;

            if (iTRJobMetadata.IsEnabled && iTRJobMetadata.IsScheduled)
            {
                iTRJobMetadata.NextFireTimeUtc = context.NextFireTimeUtc.Value.UtcDateTime;
            }

            _dbContext.Update(iTRJobMetadata);
            await _dbContext.SaveChangesAsync();
        }
    }
}
