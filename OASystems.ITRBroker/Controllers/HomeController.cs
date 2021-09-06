using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using OASystems.ITRBroker.Model;
using OASystems.ITRBroker.JobFactory;
using OASystems.ITRBroker.Attributes;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace OASystems.ITRBroker.Controllers
{
    [ApiKey]
    [Route("/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        // GET: [controller]
        [HttpGet]
        public IEnumerable<ITRJobMetadata> Get()
        {
            var itrJobMetadataList = ITRJobSchedulerFactory.GetAllJobs();
            return itrJobMetadataList;
        }

        // GET [controller]/5
        [HttpGet("{id}")]
        public ITRJobMetadata Get(int id)
        {
            ITRJobMetadata itrJobMetadata = ITRJobSchedulerFactory.GetJobWithId(id.ToString());
            return itrJobMetadata;
        }

        // POST [controller]
        [HttpPost]
        public async Task PostAsync([FromBody] ITRJobMetadata itrJobMetadata)
        {
            await ITRJobSchedulerFactory.ScheduleNewJob(itrJobMetadata);
        }

        // PUT [controller]
        [HttpPut]
        public async Task Put([FromBody] ITRJobMetadata itrJobMetadata)
        {
            await ITRJobSchedulerFactory.ResecheduleJob(itrJobMetadata);
        }

        // DELETE [controller]/5
        [HttpDelete("{id}")]
        public async Task Delete(int id)
        {
            await ITRJobSchedulerFactory.DeleteJob(id.ToString());
        }
    }
}
