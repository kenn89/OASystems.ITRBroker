using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Options;
using OASystems.ITRBroker.Model;
using OASystems.ITRBroker.Services;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using OASystems.ITRBroker.Handler;

namespace OASystems.ITRBroker.Controllers
{
    [Authorize]
    [Route("/itrjob")]
    [ApiController]
    public class ITRJobController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISchedulerService _schedulerService;

        public ITRJobController(IDatabaseService databaseService, ISchedulerService schedulerService)
        {
            _databaseService = databaseService;
            _schedulerService = schedulerService;
        }

        // GET /itrjob
        [HttpGet]
        public async Task<ITRJob> Get()
        {
            // Get the ITR Job ID and return the ITR Job details
            Guid itrJobID = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));
            ITRJob itrJob = await ITRJobHandler.GetITRJobByIdentifier(_databaseService, _schedulerService, itrJobID);

            return itrJob;
        }

        // POST /itrjob
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] RequestBody body)
        {
            if (body.CronSchedule == null && body.IsScheduled == null)
            {
                return BadRequest("The request body is invalid.");
            }

            ITRJob itrJob = new ITRJob()
            {
                ID = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier)),
                Name = User.FindFirstValue(ClaimTypes.Name)
            };

            // Update Cron Schedule
            if (body.CronSchedule != null)
            {
                try
                {
                    itrJob = await ITRJobHandler.UpdateCronSchedule(_databaseService, _schedulerService, itrJob, body.CronSchedule);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }

            // Update IsScheduled
            if (body.IsScheduled != null)
            {
                itrJob = await ITRJobHandler.UpdateIsScheduled(_databaseService, _schedulerService, itrJob, body.IsScheduled.Value);
            }

            return Ok(itrJob);
        }
    }
}
