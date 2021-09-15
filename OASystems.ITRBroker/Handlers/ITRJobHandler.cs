using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OASystems.ITRBroker.Services;
using OASystems.ITRBroker.Model;
using Quartz;

namespace OASystems.ITRBroker.Handler
{
    public static class ITRJobHandler
    {
        public static async Task<ITRJob> GetITRJobByIdentifier(IDatabaseService databaseService, ISchedulerService schedulerService, Guid itrJobID)
        {
            // Try to get the ITR Job from the scheduler
            ITRJob itrJobFromScheduler = schedulerService.GetScheduledJobByJobKey(itrJobID.ToString());

            // If no scheduled job found, return the ITR Job retrieved from Database, after seting the IsScheduled to false
            if (itrJobFromScheduler == null)
            {
                // Get ITR Job from Database
                ITRJob itrJobFromDb = await databaseService.GetITRJobFromID(itrJobID);
                return itrJobFromDb;
            }
            // If scheduled job is found, return the scheduled job
            else
            {
                return itrJobFromScheduler;
            }
        }

        public static async Task<ITRJob> UpdateCronSchedule(IDatabaseService databaseService, ISchedulerService schedulerService, ITRJob itrJob, string cronSchedule)
        {
            if (!Quartz.CronExpression.IsValidExpression(cronSchedule))
            {
                throw new Exception("The CronSchedule provided is invalid.");
            }

            // Update the database
            databaseService.UpdateITRJobCronSchedule(itrJob.ID, cronSchedule);

            // Get the ITR Job from the scheduler
            var itrJobFromScheduler = schedulerService.GetScheduledJobByJobKey(itrJob.ID.ToString());
            if (itrJobFromScheduler != null && itrJobFromScheduler.CronSchedule != cronSchedule)
            {
                itrJob.CronSchedule = cronSchedule;
                await schedulerService.ResecheduleJob(itrJob);
                itrJob = schedulerService.GetScheduledJobByJobKey(itrJob.ID.ToString());
            }
            else if (itrJobFromScheduler != null && itrJobFromScheduler.CronSchedule == cronSchedule)
            {
                itrJob = itrJobFromScheduler;
            }
            else
            {
                itrJob = await databaseService.GetITRJobFromID(itrJob.ID);
            }

            return itrJob;
        }

        public static async Task<ITRJob> UpdateIsScheduled(IDatabaseService databaseService, ISchedulerService schedulerService, ITRJob itrJob, bool isScheduled)
        {
            // Update the database
            databaseService.UpdateITRJobIsScheduled(itrJob.ID, isScheduled);

            // Get the ITR Job from the scheduler
            var itrJobFromScheduler = schedulerService.GetScheduledJobByJobKey(itrJob.ID.ToString());

            if (isScheduled)
            {
                if (itrJobFromScheduler == null)
                {
                    var itrJobFromDb = await databaseService.GetITRJobFromID(itrJob.ID);
                    await schedulerService.ScheduleNewJob(itrJobFromDb);
                }

                itrJob = schedulerService.GetScheduledJobByJobKey(itrJob.ID.ToString());
            }
            else
            {
                if (itrJobFromScheduler != null)
                {
                    // Stop the job in scheduler
                    await schedulerService.DeleteJob(itrJob);
                }

                itrJob = await databaseService.GetITRJobFromID(itrJob.ID);
            }

            return itrJob;
        }
    }
}
