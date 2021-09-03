using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;

namespace OASystems.ITRBroker
{
    public class JobSchedulerFactory
    {
        public static IScheduler _scheduler;

        public static async Task InitializeITRJobsAsync()
        {
            // Get list of itr jobs to do
            var list = GetITRJobs();

            // Grab the Scheduler instance from the Factory
            StdSchedulerFactory factory = new StdSchedulerFactory();
            _scheduler = await factory.GetScheduler();

            // and start it off
            await _scheduler.Start();

            foreach (ITRJob job in list)
            {
                var jobscheduler = GenerateJobScheduler(job);

                // Tell quartz to schedule the job using our trigger
                await _scheduler.ScheduleJob(jobscheduler.Item1, jobscheduler.Item2);
            }
        }

        public static async Task ResecheduleJob(string jobName, string cronSchedule)
        {
            ITrigger newTrigger = TriggerBuilder.Create()
                .WithIdentity(jobName)
                .WithCronSchedule(cronSchedule)
                .Build();

            await _scheduler.RescheduleJob(new TriggerKey(jobName), newTrigger);
        }

        public static Tuple<IJobDetail, ITrigger> GenerateJobScheduler(ITRJob itrJobDetails)
        {
            // define the job and tie it to our HelloJob class
            IJobDetail job = JobBuilder.Create<ITRJobExecution>()
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

        public static List<ITRJob> GetITRJobs()
        {
            List<ITRJob> list = new List<ITRJob>();
            list.Add(new ITRJob() { name = "Test A", time = "0/5 * * * * ?" });
            //list.Add(new ITRJob() { name = "Test B", time = "0/5 * * * * ?" });

            return list;
        }
    }

    public class ITRJob
    {
        public string name { get; set; }
        public string time { get; set; }
    }

    public class ITRJobExecution : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;

            string message = datamap.GetString("message");

            await TestAPI.DoTestAsync(message);

            //await Console.Out.WriteLineAsync($"Time: {DateTime.Now.ToLocalTime()}, Message: {message}");
        }
    }
}
