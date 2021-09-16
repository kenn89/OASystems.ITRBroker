using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using OASystems.ITRBroker.Models;

namespace OASystems.ITRBroker.Services
{
    public interface ISchedulerService
    {
        Task InitializeITRJobScheduler(IDatabaseService databaseService);
        ITRJob GetScheduledJobByJobKey(string jobKeyName);
        Task ScheduleNewJob(ITRJob itrJob);
        Task ResecheduleJob(ITRJob itrJob);
        Task DeleteJob(ITRJob itrJob);
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly IScheduler _scheduler;

        public SchedulerService()
        {
            _scheduler = new StdSchedulerFactory().GetScheduler().Result;
        }

        // Get list of enabled ITR Jobs from the database and start running them as scheduled jobs
        public async Task InitializeITRJobScheduler(IDatabaseService databaseService)
        {
            await _scheduler.Start();

            List<ITRJob> itrJobs = await databaseService.GetAllEnabledITRJobs();

            foreach (ITRJob itrJob in itrJobs)
            {
                if (itrJob.IsScheduled.Value && CronExpression.IsValidExpression(itrJob.CronSchedule))
                {
                    await ScheduleNewJob(itrJob);
                }
            }
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
        public async Task ScheduleNewJob(ITRJob itrJob)
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
        }

        // Reschedule existing ITR Job
        public async Task ResecheduleJob(ITRJob itrJob)
        {
            ITrigger newTrigger = TriggerBuilder.Create()
            .WithIdentity(itrJob.ID.ToString())
            .WithCronSchedule(itrJob.CronSchedule)
            .Build();

            await _scheduler.RescheduleJob(new TriggerKey(itrJob.ID.ToString()), newTrigger);
        }

        // Delete an existing Quartz job
        public async Task DeleteJob(ITRJob itrJob)
        {
            await _scheduler.DeleteJob(new JobKey(itrJob.ID.ToString(), itrJob.Name));
        }
    }
}
