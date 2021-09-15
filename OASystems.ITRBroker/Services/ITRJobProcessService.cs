using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using RestSharp;

namespace OASystems.ITRBroker.Services
{
    public class ITRJobProcessService : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap datamap = context.JobDetail.JobDataMap;
                string message = datamap.GetString("message");

                var client = new RestClient("https://webhook.site/ec7ce0cc-51a0-4047-8c3f-486616d20629");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "text/plain");
                var body = @message;
                request.AddParameter("text/plain", body, ParameterType.RequestBody);
                IRestResponse response = await client.ExecuteAsync(request);
                Console.WriteLine(response.Content);
            }
            catch
            {
                return;
            }
        }
    }
}
