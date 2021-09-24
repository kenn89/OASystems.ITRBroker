using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Controllers
{
    [Authorize(AuthenticationSchemes = "BasicAuthentication")]
    [Route("api/ITRJobMetadata")]
    [ApiController]
    public class ITRJobMetadataApiController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly ISchedulerService _schedulerService;

        public ITRJobMetadataApiController(DatabaseContext context, ISchedulerService schedulerService)
        {
            _context = context;
            _schedulerService = schedulerService;
        }

        // GET: api/ITRJobMetadata
        [HttpGet]
        public async Task<ITRJobMetadata> Get()
        {
            Guid iTRJobMetadataId = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Try to get the ITR Job Metadata from the scheduler
            ITRJobMetadata schJobMetadata = _schedulerService.GetScheduledJobByJobKey(iTRJobMetadataId.ToString());

            if (schJobMetadata == null)
            {
                // If no scheduled job found, return the ITR Job Metadata from the Database
                ITRJobMetadata dbJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadataId).FirstOrDefaultAsync();
                return dbJobMetadata;
            }
            else
            {
                // If scheduled job is found, return the ITR Job Metadata from the Scheduler
                return schJobMetadata;
            }
        }

        // POST: api/ITRJobMetadata
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] RequestBody body)
        {
            // Update and return the ITR Job Metadata
            try
            {
                Guid iTRJobMetadataId = new Guid(User.FindFirstValue(ClaimTypes.NameIdentifier));

                bool isUpdated = false;

                ITRJobMetadata iTRJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadataId).FirstOrDefaultAsync();

                // Update IsScheduled
                if (body.IsScheduled.HasValue)
                {
                    iTRJobMetadata.IsScheduled = body.IsScheduled.Value;
                    isUpdated = true;
                }

                // Validate and update the CronSchedule
                if (body.CronSchedule != null)
                {
                    iTRJobMetadata.CronSchedule = body.CronSchedule;
                    isUpdated = true;
                }

                // Save the changes
                if (isUpdated)
                {
                    var context = new ValidationContext(iTRJobMetadata, null, null);
                    var results = new List<ValidationResult>();

                    if (Validator.TryValidateObject(iTRJobMetadata, context, results, true))
                    {
                        await _context.SaveChangesAsync();
                        await _schedulerService.SyncDbToSchedulerById(_context, iTRJobMetadataId);
                        iTRJobMetadata = await _context.ITRJobMetadata.Where(x => x.ID == iTRJobMetadataId).FirstOrDefaultAsync();
                    }
                    else
                    {
                        var errormessage = "";

                        foreach (var result in results)
                        {
                            errormessage = errormessage + "\n" + result.ErrorMessage;
                        }

                        throw new Exception(errormessage.Trim());
                    }
                }
                return Ok(iTRJobMetadata);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
