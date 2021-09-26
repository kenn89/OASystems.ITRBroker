using Microsoft.EntityFrameworkCore;
using OASystems.ITRBroker.Models;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Services
{
    public interface ISchedulerService
    {
        Task InitializeITRJobScheduler();
        ITRJobMetadata GetScheduledJobByJobKey(string jobKeyName);
        Task<DateTimeOffset?> ScheduleNewJob(ITRJobMetadata iTRJobMetadata);
        Task<DateTimeOffset?> ResecheduleJob(ITRJobMetadata iTRJobMetadata);
        Task DeleteJob(ITRJobMetadata iTRJobMetadata);
        List<ITRJobMetadata> GetAllScheduledJobs();
        Task SyncDbToSchedulerById(Guid iTRJobMetadata);
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly DatabaseContext _context;
        private readonly IScheduler _scheduler;

        public SchedulerService(DatabaseContext context)
        {
            _context = context;
            _scheduler = new StdSchedulerFactory().GetScheduler().Result;
        }

        // Get list of enabled ITR Job Metadata from the database and start running them as scheduled jobs
        public async Task InitializeITRJobScheduler()
        {
            await _scheduler.Start();

            List<ITRJobMetadata> iTRJobMetadataList = _context.ITRJobMetadata.ToList();

            foreach (ITRJobMetadata iTRJobMetadata in iTRJobMetadataList)
            {
                if (iTRJobMetadata.IsEnabled && iTRJobMetadata.IsScheduled && CronExpression.IsValidExpression(iTRJobMetadata.CronSchedule))
                {
                    await ScheduleNewJob(iTRJobMetadata);
                }
            }
        }

        // Use Job Key to retrieve the ITR Job Metadata from scheduler
        public List<ITRJobMetadata> GetAllScheduledJobs()
        {
            List<ITRJobMetadata> iTRJobMetadataList = new List<ITRJobMetadata>();

            var jobKeys = _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).Result;

            foreach (var jobKey in jobKeys)
            {
                var triggers = _scheduler.GetTriggersOfJob(jobKey).Result;
                foreach (var trigger in triggers)
                {
                    ITRJobMetadata iTRJobMetadata = new ITRJobMetadata();
                    iTRJobMetadata.ID = new Guid(jobKey.Name);
                    iTRJobMetadata.Name = jobKey.Group;
                    iTRJobMetadata.CronSchedule = ((ICronTrigger)trigger).CronExpressionString;
                    iTRJobMetadata.PreviousFireTimeUtc = trigger.GetPreviousFireTimeUtc().HasValue ? (DateTime?)trigger.GetPreviousFireTimeUtc().Value.UtcDateTime : null;
                    iTRJobMetadata.NextFireTimeUtc = trigger.GetNextFireTimeUtc().HasValue ? (DateTime?)trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
                    iTRJobMetadata.IsScheduled = true;

                    iTRJobMetadataList.Add(iTRJobMetadata);
                }
            }
            return iTRJobMetadataList;
        }

        // Use Job Key to retrieve the ITR Job Metadata from scheduler
        public ITRJobMetadata GetScheduledJobByJobKey(string jobKeyName)
        {
            ITRJobMetadata iTRJobMetadata = null;

            var jobKeys = _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).Result;

            foreach(var jobKey in jobKeys)
            {
                if (jobKey.Name == jobKeyName)
                {
                    var triggers = _scheduler.GetTriggersOfJob(jobKey).Result;
                    foreach (var trigger in triggers)
                    {
                        iTRJobMetadata = new ITRJobMetadata();
                        iTRJobMetadata.ID = new Guid(jobKey.Name);
                        iTRJobMetadata.Name = jobKey.Group;
                        iTRJobMetadata.CronSchedule = ((ICronTrigger)trigger).CronExpressionString;
                        iTRJobMetadata.PreviousFireTimeUtc = trigger.GetPreviousFireTimeUtc().HasValue ? (DateTime?)trigger.GetPreviousFireTimeUtc().Value.UtcDateTime : null;
                        iTRJobMetadata.NextFireTimeUtc = trigger.GetNextFireTimeUtc().HasValue ? (DateTime?)trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
                        iTRJobMetadata.IsScheduled = true;
                        break;
                    }
                }
            }
            return iTRJobMetadata;
        }

        // Schedule new ITR Job
        public async Task<DateTimeOffset?> ScheduleNewJob(ITRJobMetadata iTRJobMetadata)
        {
            IJobDetail job = JobBuilder.Create<ITRJob>()
                .UsingJobData("crmUrl", iTRJobMetadata.CrmUrl)
                .UsingJobData("crmClientID", iTRJobMetadata.CrmClientID)
                .UsingJobData("crmSecret", iTRJobMetadata.CrmSecret)
                .WithIdentity(iTRJobMetadata.ID.ToString(), iTRJobMetadata.Name)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(iTRJobMetadata.ID.ToString())
                .WithCronSchedule(iTRJobMetadata.CronSchedule)
                .Build();

            await _scheduler.ScheduleJob(job, trigger);

            return trigger.GetNextFireTimeUtc();
        }

        // Reschedule existing ITR Job
        public async Task<DateTimeOffset?> ResecheduleJob(ITRJobMetadata iTRJobMetadata)
        {
            ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(iTRJobMetadata.ID.ToString())
            .WithCronSchedule(iTRJobMetadata.CronSchedule)
            .Build();

            await _scheduler.RescheduleJob(new TriggerKey(iTRJobMetadata.ID.ToString()), trigger);

            return trigger.GetNextFireTimeUtc();
        }

        // Delete an existing ITR Job
        public async Task DeleteJob(ITRJobMetadata iTRJobMetadata)
        {
            await _scheduler.DeleteJob(new JobKey(iTRJobMetadata.ID.ToString(), iTRJobMetadata.Name));
        }

        public async Task SyncDbToSchedulerById(Guid iTRJobMetadata)
        {
            ITRJobMetadata schJobMetadata = GetScheduledJobByJobKey(iTRJobMetadata.ToString());
            ITRJobMetadata dbJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadata).FirstOrDefaultAsync();

            if (dbJobMetadata.IsEnabled && dbJobMetadata.IsScheduled && schJobMetadata == null)
            {
                // Schedule new job
                dbJobMetadata.NextFireTimeUtc = (await ScheduleNewJob(dbJobMetadata)).Value.UtcDateTime;
                await _context.SaveChangesAsync();
            }
            else if (dbJobMetadata.IsEnabled && dbJobMetadata.IsScheduled && schJobMetadata != null && dbJobMetadata.CronSchedule != schJobMetadata.CronSchedule)
            {
                // Reschedule the job
                dbJobMetadata.NextFireTimeUtc = (await ResecheduleJob(dbJobMetadata)).Value.UtcDateTime;
                await _context.SaveChangesAsync();
            }
            else if ((!dbJobMetadata.IsEnabled || !dbJobMetadata.IsScheduled) && schJobMetadata != null)
            {
                // Stop the job
                await DeleteJob(dbJobMetadata);
            }
        }
    }
}
