using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OASystems.ITRBroker.Services;
using OASystems.ITRBroker.Models;
using Quartz;
using Microsoft.EntityFrameworkCore;

namespace OASystems.ITRBroker.Handler
{
    public class ITRJobHandler
    {
        private readonly DatabaseContext _context;
        private readonly ISchedulerService _schedulerService;

        public ITRJobHandler(DatabaseContext context)
        {
            _context = context;
        }

        public ITRJobHandler(DatabaseContext context, ISchedulerService schedulerService)
        {
            _context = context;
            _schedulerService = schedulerService;
        }

        public async Task<ITRJob> Authenticate(string username, string password)
        {
            return await _context.ITRJob.Where(x => x.ApiUsername == username && x.ApiPassword == password && x.IsEnabled).FirstOrDefaultAsync();
        }

        public List<ITRJob> GetAllITRJobs()
        {
            return _context.ITRJob.ToList();
        }

        public async Task<ITRJob> GetITRJobByID(Guid itrJobID)
        {
            // Try to get the ITR Job from the scheduler
            ITRJob itrJobFromScheduler = _schedulerService.GetScheduledJobByJobKey(itrJobID.ToString());

            if (itrJobFromScheduler == null)
            {
                // If no scheduled job found, return the ITR Job from the Database
                ITRJob itrJobFromDb = await _context.ITRJob.Where(x => x.ID == itrJobID).FirstOrDefaultAsync();
                return itrJobFromDb;
            }
            else
            {
                // If scheduled job is found, return the ITR Job from the Scheduler
                return itrJobFromScheduler;
            }
        }

        public async Task<ITRJob> UpdateITRJob(RequestBody body)
        {
            bool isUpdated = false;

            var itrJob = await _context.ITRJob.Where(x => x.ID == body.ID).FirstOrDefaultAsync();

            // Update IsScheduled
            if (body.IsScheduled.HasValue)
            {
                itrJob.IsScheduled = body.IsScheduled.Value;
                isUpdated = true;
            }

            // Validate and update the CronSchedule
            if (body.CronSchedule != null)
            {
                itrJob = ValidateCronScheduleAndUpdateITRJob(itrJob, body.CronSchedule);
                isUpdated = true;
            }

            // Save the changes
            if (isUpdated)
            {
                _context.SaveChanges();

                await SyncDbToSchedulerByJobID(body.ID);

                itrJob = await _context.ITRJob.Where(x => x.ID == body.ID).FirstOrDefaultAsync();
            }

            return itrJob;
        }

        public async Task SyncDbToSchedulerByJobID(Guid itrJobID)
        {
            ITRJob schJob = _schedulerService.GetScheduledJobByJobKey(itrJobID.ToString());
            ITRJob dbJob = await _context.ITRJob.Where(x => x.ID == itrJobID).FirstOrDefaultAsync();

            if (dbJob.IsEnabled && dbJob.IsScheduled && schJob == null)
            {
                // Schedule new job
                dbJob.NextFireTimeUtc = (await _schedulerService.ScheduleNewJob(dbJob)).Value.UtcDateTime;
                _context.SaveChanges();
            }
            else if (dbJob.IsEnabled && dbJob.IsScheduled && schJob != null && dbJob.CronSchedule != schJob.CronSchedule)
            {
                // Reschedule the job
                dbJob.NextFireTimeUtc = (await _schedulerService.ResecheduleJob(dbJob)).Value.UtcDateTime;
                _context.SaveChanges();
            }
            else if ((!dbJob.IsEnabled || !dbJob.IsScheduled) && schJob != null)
            {
                // Stop the job
                await _schedulerService.DeleteJob(dbJob);
            }
        }

        public ITRJob ValidateCronScheduleAndUpdateITRJob(ITRJob itrJob, string cronSchedule)
        {
            if (cronSchedule == null || cronSchedule == "")
            {
                itrJob.CronSchedule = null;
                itrJob.IsScheduled = false;
            }
            else
            {
                itrJob.CronSchedule = cronSchedule;
            }

            return itrJob;
        }
    }
}
