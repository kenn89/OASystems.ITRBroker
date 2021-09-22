using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Handler;

namespace OASystems.ITRBroker.Services
{
    public interface ISchedulerService
    {
        Task InitializeITRJobScheduler(DatabaseContext context);
        ITRJob GetScheduledJobByJobKey(string jobKeyName);
        Task<DateTimeOffset?> ScheduleNewJob(ITRJob itrJob);
        Task<DateTimeOffset?> ResecheduleJob(ITRJob itrJob);
        Task DeleteJob(ITRJob itrJob);
        List<ITRJob> GetAllScheduledJobs();
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly IScheduler _scheduler;

        public SchedulerService()
        {
            _scheduler = new StdSchedulerFactory().GetScheduler().Result;
        }

        // Get list of enabled ITR Jobs from the database and start running them as scheduled jobs
        public async Task InitializeITRJobScheduler(DatabaseContext context)
        {
            await _scheduler.Start();

            ITRJobHandler itrJobHandler = new ITRJobHandler(context);
            List<ITRJob> itrJobs = itrJobHandler.GetAllITRJobs();

            foreach (ITRJob itrJob in itrJobs)
            {
                if (itrJob.IsEnabled && itrJob.IsScheduled && CronExpression.IsValidExpression(itrJob.CronSchedule))
                {
                    await ScheduleNewJob(itrJob);
                }
            }
        }

        // Use Job Key to retrieve the ITR Job from scheduler
        public List<ITRJob> GetAllScheduledJobs()
        {
            List<ITRJob> itrJobList = new List<ITRJob>();

            var jobKeys = _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).Result;

            foreach (var jobKey in jobKeys)
            {
                var triggers = _scheduler.GetTriggersOfJob(jobKey).Result;
                foreach (var trigger in triggers)
                {
                    ITRJob itrJob = new ITRJob();
                    itrJob.ID = new Guid(jobKey.Name);
                    itrJob.Name = jobKey.Group;
                    itrJob.CronSchedule = ((ICronTrigger)trigger).CronExpressionString;
                    itrJob.PreviousFireTimeUtc = trigger.GetPreviousFireTimeUtc().HasValue ? (DateTime?)trigger.GetPreviousFireTimeUtc().Value.UtcDateTime : null;
                    itrJob.NextFireTimeUtc = trigger.GetNextFireTimeUtc().HasValue ? (DateTime?)trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
                    itrJob.IsScheduled = true;

                    itrJobList.Add(itrJob);
                }
            }
            return itrJobList;
        }

        // Use Job Key to retrieve the ITR Job from scheduler
        public ITRJob GetScheduledJobByJobKey(string jobKeyName)
        {
            ITRJob itrJob = null;

            var jobKeys = _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).Result;

            foreach(var jobKey in jobKeys)
            {
                if (jobKey.Name == jobKeyName)
                {
                    var triggers = _scheduler.GetTriggersOfJob(jobKey).Result;
                    foreach (var trigger in triggers)
                    {
                        itrJob = new ITRJob();
                        itrJob.ID = new Guid(jobKey.Name);
                        itrJob.Name = jobKey.Group;
                        itrJob.CronSchedule = ((ICronTrigger)trigger).CronExpressionString;
                        itrJob.PreviousFireTimeUtc = trigger.GetPreviousFireTimeUtc().HasValue ? (DateTime?)trigger.GetPreviousFireTimeUtc().Value.UtcDateTime : null;
                        itrJob.NextFireTimeUtc = trigger.GetNextFireTimeUtc().HasValue ? (DateTime?)trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
                        itrJob.IsScheduled = true;
                        break;
                    }
                }
            }
            return itrJob;
        }

        // Schedule new ITR Job
        public async Task<DateTimeOffset?> ScheduleNewJob(ITRJob itrJob)
        {
            IJobDetail job = JobBuilder.Create<ITRJobProcessService>()
                .UsingJobData("crmUrl", itrJob.CrmUrl)
                .UsingJobData("crmClientID", itrJob.CrmClientID)
                .UsingJobData("crmSecret", itrJob.CrmSecret)
                .WithIdentity(itrJob.ID.ToString(), itrJob.Name)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(itrJob.ID.ToString())
                .WithCronSchedule(itrJob.CronSchedule)
                .Build();

            await _scheduler.ScheduleJob(job, trigger);

            return trigger.GetNextFireTimeUtc();
        }

        // Reschedule existing ITR Job
        public async Task<DateTimeOffset?> ResecheduleJob(ITRJob itrJob)
        {
            ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(itrJob.ID.ToString())
            .WithCronSchedule(itrJob.CronSchedule)
            .Build();

            await _scheduler.RescheduleJob(new TriggerKey(itrJob.ID.ToString()), trigger);

            return trigger.GetNextFireTimeUtc();
        }

        // Delete an existing Quartz job
        public async Task DeleteJob(ITRJob itrJob)
        {
            await _scheduler.DeleteJob(new JobKey(itrJob.ID.ToString(), itrJob.Name));
        }
    }
}
