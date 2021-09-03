using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using OASystems.ITRBroker.Model;
using OASystems.ITRBroker.JobFactory;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace OASystems.ITRBroker.Controllers
{
    [Route("/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        // GET: <HomeController>
        [HttpGet]
        public IEnumerable<ITRJobMetadata> Get()
        {
            var itrJobMetadataList = ITRJobSchedulerFactory.GetAllJobs();
            return itrJobMetadataList;
        }

        // GET <ValuesController>/5
        [HttpGet("{id}")]
        public ITRJobMetadata Get(int id)
        {
            ITRJobMetadata itrJobMetadata = ITRJobSchedulerFactory.GetJobWithId(id.ToString());
            return itrJobMetadata;
        }

        // POST <HomeController>
        [HttpPost]
        public async Task PostAsync([FromBody] ITRJobMetadata itrJobMetadata)
        {
            await ITRJobSchedulerFactory.ResecheduleJob(itrJobMetadata);
        }

        // PUT <HomeController>
        [HttpPut]
        public async Task Put([FromBody] ITRJobMetadata itrJobMetadata)
        {
            await ITRJobSchedulerFactory.ScheduleNewJob(itrJobMetadata);
        }

        // DELETE <HomeController>/5
        [HttpDelete("{id}")]
        public async Task Delete(int id)
        {
            await ITRJobSchedulerFactory.DeleteJob(id.ToString());
        }
    }
}
