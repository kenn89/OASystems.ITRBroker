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

        public async Task<ITRJob> UpdateITRJob(ITRJob itrJob)
        {
            bool isUpdated = false;

            var itrJobToUpdate = await _context.ITRJob.Where(x => x.ID == itrJob.ID).FirstOrDefaultAsync();

            // Update IsScheduled
            if (itrJob.IsScheduled.HasValue)
            {
                itrJobToUpdate.IsScheduled = itrJob.IsScheduled;
                isUpdated = true;
            }

            // Validate and update the CronSchedule
            if (itrJob.CronSchedule != null)
            {
                if (itrJob.CronSchedule == "")
                {
                    itrJobToUpdate.CronSchedule = null;
                    itrJobToUpdate.IsScheduled = false;
                    isUpdated = true;
                }
                else if (CronExpression.IsValidExpression(itrJob.CronSchedule))
                {
                    itrJobToUpdate.CronSchedule = itrJob.CronSchedule;
                    isUpdated = true;
                }
                else if (!CronExpression.IsValidExpression(itrJob.CronSchedule))
                {
                    throw new Exception("The CronSchedule provided is invalid.");
                }
            }

            // Save the changes
            if (isUpdated)
            {
                _context.SaveChanges();

                await SyncDbToSchedulerByJobID(itrJob.ID);

                itrJobToUpdate = await _context.ITRJob.Where(x => x.ID == itrJob.ID).FirstOrDefaultAsync();
            }

            return itrJobToUpdate;
        }

        private async Task SyncDbToSchedulerByJobID(Guid itrJobID)
        {
            ITRJob schJob = _schedulerService.GetScheduledJobByJobKey(itrJobID.ToString());
            ITRJob dbJob = await _context.ITRJob.Where(x => x.ID == itrJobID).FirstOrDefaultAsync();

            if (dbJob.IsEnabled && dbJob.IsScheduled.Value && schJob == null)
            {
                // Schedule new job
                dbJob.NextFireTimeUtc = (await _schedulerService.ScheduleNewJob(dbJob)).Value.UtcDateTime;
                _context.SaveChanges();
            }
            else if (dbJob.IsEnabled && dbJob.IsScheduled.Value && schJob != null && dbJob.CronSchedule != schJob.CronSchedule)
            {
                // Reschedule the job
                dbJob.NextFireTimeUtc = (await _schedulerService.ResecheduleJob(dbJob)).Value.UtcDateTime;
                _context.SaveChanges();
            }
            else if ((!dbJob.IsEnabled || !dbJob.IsScheduled.Value) && schJob != null)
            {
                // Stop the job
                await _schedulerService.DeleteJob(dbJob);
            }
        }
    }
}
