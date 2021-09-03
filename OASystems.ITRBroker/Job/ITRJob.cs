using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;

namespace OASystems.ITRBroker.Job
{
    public class ITRJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;
            string message = datamap.GetString("message");

            await TestAPI.DoTestAsync(message);
        }
    }
}
