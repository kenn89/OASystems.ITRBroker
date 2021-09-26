using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OASystems.ITRBroker.Job;
using OASystems.ITRBroker.Models;
using Quartz;
using Quartz.Impl;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Services
{
    public interface ISchedulerService
    {
        void InitializeITRJobScheduler();
        Task<ITRJobMetadata> SyncDbToSchedulerById(Guid iTRJobMetadata);
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly DatabaseContext _context;
        private readonly IScheduler _scheduler;

        public SchedulerService(DatabaseContext context, IConfiguration configuration)
        {
            _scheduler = new StdSchedulerFactory().GetScheduler(configuration["Quartz:quartz.scheduler.instanceName"]).Result;
            _context = context;
        }

        // Get list of enabled ITR Job Metadata from the database and start running them as scheduled jobs
        public void InitializeITRJobScheduler()
        {
            var iTRJobMetadataList = _context.ITRJobMetadata.ToList();

            foreach (ITRJobMetadata iTRJobMetadata in iTRJobMetadataList)
            {
                if (iTRJobMetadata.IsEnabled && iTRJobMetadata.IsScheduled && CronExpression.IsValidExpression(iTRJobMetadata.CronSchedule))
                {
                    iTRJobMetadata.NextFireTimeUtc = ScheduleNewJob(iTRJobMetadata).Result;
                    _context.Update(iTRJobMetadata);
                }
            }
            _context.SaveChanges();
        }

        public async Task<ITRJobMetadata> SyncDbToSchedulerById(Guid iTRJobMetadataId)
        {
            ITRJobMetadata iTRJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadataId).FirstOrDefaultAsync();
            ITrigger triggerOfJob = (await _scheduler.GetTriggersOfJob(new JobKey(iTRJobMetadataId.ToString()))).FirstOrDefault();

            if (iTRJobMetadata.IsEnabled && iTRJobMetadata.IsScheduled && triggerOfJob == null)
            {
                iTRJobMetadata.NextFireTimeUtc = await ScheduleNewJob(iTRJobMetadata);
                _context.Update(iTRJobMetadata);
                await _context.SaveChangesAsync();
            }
            else if (iTRJobMetadata.IsEnabled && iTRJobMetadata.IsScheduled && triggerOfJob != null && iTRJobMetadata.CronSchedule != ((ICronTrigger)triggerOfJob).CronExpressionString)
            {
                iTRJobMetadata.NextFireTimeUtc = await ResecheduleJob(iTRJobMetadata);
                _context.Update(iTRJobMetadata);
                await _context.SaveChangesAsync();
            }
            else if ((!iTRJobMetadata.IsEnabled || !iTRJobMetadata.IsScheduled) && triggerOfJob != null)
            {
                await DeleteJob(iTRJobMetadata);
                iTRJobMetadata.NextFireTimeUtc = null;
                _context.Update(iTRJobMetadata);
                await _context.SaveChangesAsync();
            }

            return iTRJobMetadata;
        }

        #region Private Methods
        // Schedule new ITR Job
        private async Task<DateTime?> ScheduleNewJob(ITRJobMetadata iTRJobMetadata)
        {
            IJobDetail jobDetail = JobBuilder.Create<ITRJob>()
                .UsingJobData("crmUrl", iTRJobMetadata.CrmUrl)
                .UsingJobData("crmClientID", iTRJobMetadata.CrmClientID)
                .UsingJobData("crmSecret", iTRJobMetadata.CrmSecret)
                .WithIdentity(iTRJobMetadata.ID.ToString())
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(iTRJobMetadata.ID.ToString())
                .WithCronSchedule(iTRJobMetadata.CronSchedule)
                .Build();

            await _scheduler.ScheduleJob(jobDetail, trigger);

            return trigger.GetNextFireTimeUtc().HasValue ? trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
        }

        // Reschedule existing ITR Job
        private async Task<DateTime?> ResecheduleJob(ITRJobMetadata iTRJobMetadata)
        {
            ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(iTRJobMetadata.ID.ToString())
            .WithCronSchedule(iTRJobMetadata.CronSchedule)
            .Build();

            await _scheduler.RescheduleJob(new TriggerKey(iTRJobMetadata.ID.ToString()), trigger);

            return trigger.GetNextFireTimeUtc().HasValue ? trigger.GetNextFireTimeUtc().Value.UtcDateTime : null;
        }

        // Delete an existing ITR Job
        private async Task DeleteJob(ITRJobMetadata iTRJobMetadata)
        {
            await _scheduler.DeleteJob(new JobKey(iTRJobMetadata.ID.ToString()));
        }
        #endregion
    }
}
