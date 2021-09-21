using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Options;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using OASystems.ITRBroker.Handler;

namespace OASystems.ITRBroker.Controllers
{
    [Authorize(AuthenticationSchemes = "BasicAuthentication")]
    [Route("api/itrjobs")]
    [ApiController]
    public class ITRJobsApiController : ControllerBase
    {
        private readonly ITRJobHandler _itrJobHandler;

        public ITRJobsApiController(DatabaseContext context, ISchedulerService schedulerService)
        {
            _itrJobHandler = new ITRJobHandler(context, schedulerService);
        }

        // GET: api/itrjobs
        [HttpGet]
        public async Task<ITRJob> Get()
        {
            // Get the ITR Job ID and return the ITR Job details
            Guid itrJobID = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));
            ITRJob itrJob = await _itrJobHandler.GetITRJobByID(itrJobID);

            return itrJob;
        }

        // POST: api/itrjobs
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ITRJob itrJob)
        {
            try
            {
                itrJob.ID = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));
                itrJob.Name = User.FindFirstValue(ClaimTypes.Name);
                itrJob = await _itrJobHandler.UpdateITRJob(itrJob);
                return Ok(itrJob);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
