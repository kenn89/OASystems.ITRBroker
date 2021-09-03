using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using OASystems.ITRBroker.Model;
using OASystems.ITRBroker.Job;

namespace OASystems.ITRBroker.JobFactory
{
    public class ITRJobSchedulerFactory
    {
        public static IScheduler _scheduler;
        public static List<ITRJobMetadata> _itrJobMetadataList;
        public static int nextId;

        public static async Task InitializeITRJobsAsync()
        {
            StdSchedulerFactory factory = new StdSchedulerFactory();
            _scheduler = await factory.GetScheduler();

            nextId = 0;

            _itrJobMetadataList = new List<ITRJobMetadata>();

            await _scheduler.Start();

            var list = GetITRJobMetadatas();
            foreach (ITRJobMetadata itrJobMetadata in list)
            {
                await ScheduleNewJob(itrJobMetadata);
            }
        }

        public static List<ITRJobMetadata> GetAllJobs()
        {
            return _itrJobMetadataList;
        }

        public static ITRJobMetadata GetJobWithId(string id)
        {
            return _itrJobMetadataList.Where(x => x.Id == id).FirstOrDefault();
        }

        public static async Task ScheduleNewJob(ITRJobMetadata itrJobMetadata)
        {
            itrJobMetadata.Id = nextId.ToString();
            nextId++;

            IJobDetail job = JobBuilder.Create<ITRJob>()
                .UsingJobData("message", itrJobMetadata.Id)
                .WithIdentity(itrJobMetadata.Id)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(itrJobMetadata.Id)
                .WithCronSchedule(itrJobMetadata.CronSchedule)
                .Build();

            await _scheduler.ScheduleJob(job, trigger);

            _itrJobMetadataList.Add(itrJobMetadata);
        }

        public static async Task ResecheduleJob(ITRJobMetadata itrJobMetadata)
        {
            ITrigger newTrigger = TriggerBuilder.Create()
                .WithIdentity(itrJobMetadata.Id)
                .WithCronSchedule(itrJobMetadata.CronSchedule)
                .Build();

            await _scheduler.RescheduleJob(new TriggerKey(itrJobMetadata.Id), newTrigger);
        }

        public static async Task DeleteJob(string id)
        {
            var job = _scheduler.GetJobDetail(new JobKey(id));

            await _scheduler.DeleteJob(new JobKey(id.ToString()));

            _itrJobMetadataList.RemoveAll(x => x.Id == id);
        }

        public static List<ITRJobMetadata> GetITRJobMetadatas()
        {
            List<ITRJobMetadata> list = new List<ITRJobMetadata>();

            return list;
        }
    }
}
