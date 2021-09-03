using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;

namespace OASystems.ITRBroker
{
    public class JobSchedulerFactory
    {
        public static Tuple<IJobDetail, ITrigger> GenerateJobScheduler(ITRJobDetails itrJobDetails)
        {
            // define the job and tie it to our HelloJob class
            IJobDetail job = JobBuilder.Create<ITRJob>()
                .UsingJobData("message", itrJobDetails.name)
                .WithIdentity(itrJobDetails.name)
                .Build();

            // Trigger the job to run now, and then repeat every 10 seconds
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(itrJobDetails.name)
                .WithCronSchedule(itrJobDetails.time)
                .Build();

            return new Tuple<IJobDetail, ITrigger>(job, trigger);
        }
    }

    public class ITRJobDetails
    {
        public string name { get; set; }
        public string time { get; set; }
    }

    public class ITRJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;

            string message = datamap.GetString("message");

            await Console.Out.WriteLineAsync($"Time: {DateTime.Now.ToLocalTime()}, Message: {message}");
        }
    }
}
