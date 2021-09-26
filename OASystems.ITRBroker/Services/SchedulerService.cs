using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OASystems.ITRBroker.Job;
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
        Task SyncDbToSchedulerById(Guid iTRJobMetadata);
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

        public async Task SyncDbToSchedulerById(Guid iTRJobMetadataId)
        {
            ITRJobMetadata dbJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadataId).FirstOrDefaultAsync();
            ITrigger triggerOfJob = (await _scheduler.GetTriggersOfJob(new JobKey(iTRJobMetadataId.ToString()))).FirstOrDefault();

            if (dbJobMetadata.IsEnabled && dbJobMetadata.IsScheduled && triggerOfJob == null)
            {
                await ScheduleNewJob(dbJobMetadata);
            }
            else if (dbJobMetadata.IsEnabled && dbJobMetadata.IsScheduled && triggerOfJob != null && dbJobMetadata.CronSchedule != ((ICronTrigger)triggerOfJob).CronExpressionString)
            {
                await ResecheduleJob(dbJobMetadata);
            }
            else if ((!dbJobMetadata.IsEnabled || !dbJobMetadata.IsScheduled) && triggerOfJob != null)
            {
                await DeleteJob(dbJobMetadata);
            }
        }

        #region Private Methods
        // Schedule new ITR Job
        private async Task ScheduleNewJob(ITRJobMetadata iTRJobMetadata)
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
        }

        // Reschedule existing ITR Job
        private async Task ResecheduleJob(ITRJobMetadata iTRJobMetadata)
        {
            ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(iTRJobMetadata.ID.ToString())
            .WithCronSchedule(iTRJobMetadata.CronSchedule)
            .Build();

            await _scheduler.RescheduleJob(new TriggerKey(iTRJobMetadata.ID.ToString()), trigger);
        }

        // Delete an existing ITR Job
        private async Task DeleteJob(ITRJobMetadata iTRJobMetadata)
        {
            await _scheduler.DeleteJob(new JobKey(iTRJobMetadata.ID.ToString()));
        }
        #endregion
    }
}
