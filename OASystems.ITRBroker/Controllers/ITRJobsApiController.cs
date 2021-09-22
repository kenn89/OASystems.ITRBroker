using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OASystems.ITRBroker.Handler;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Post([FromBody] RequestBody body)
        {
            var test = TryValidateModel(body);
            try
            {
                body.ID = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));
                body.Name = User.FindFirstValue(ClaimTypes.Name);
                var itrJob = await _itrJobHandler.UpdateITRJob(body);
                return Ok(itrJob);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
