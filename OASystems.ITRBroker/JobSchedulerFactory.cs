using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;

namespace OASystems.ITRBroker
{
    public class ITRJobSchedulerFactory
    {
        public static IScheduler _scheduler;
        public static int nextId;

        public static async Task InitializeITRJobsAsync()
        {
            StdSchedulerFactory factory = new StdSchedulerFactory();
            _scheduler = await factory.GetScheduler();
            nextId = 0;

            await _scheduler.Start();

            var list = GetITRJobs();
            foreach (ITRJob itrJob in list)
            {
                await ScheduleNewJob(itrJob);
            }
        }

        public static async Task ScheduleNewJob(ITRJob itrJob)
        {
            itrJob.Id = nextId.ToString();
            nextId++;

            IJobDetail job = JobBuilder.Create<ITRJobExecution>()
                .UsingJobData("message", itrJob.Id)
                .WithIdentity(itrJob.Id)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(itrJob.Id)
                .WithCronSchedule(itrJob.CronSchedule)
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
        }

        public static async Task ResecheduleJob(ITRJob itrJob)
        {
            ITrigger newTrigger = TriggerBuilder.Create()
                .WithIdentity(itrJob.Id)
                .WithCronSchedule(itrJob.CronSchedule)
                .Build();

            await _scheduler.RescheduleJob(new TriggerKey(itrJob.Id), newTrigger);
        }

        public static async Task DeleteJob(int id)
        {
            var job = _scheduler.GetJobDetail(new JobKey(id.ToString()));

            await _scheduler.DeleteJob(new JobKey(id.ToString()));
        }

        public static List<ITRJob> GetITRJobs()
        {
            List<ITRJob> list = new List<ITRJob>();

            return list;
        }
    }

    public class ITRJob
    {
        public string Id { get; set; }
        public string CronSchedule { get; set; }
    }

    public class ITRJobExecution : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap datamap = context.JobDetail.JobDataMap;
            string message = datamap.GetString("message");

            await TestAPI.DoTestAsync(message);
        }
    }
}
