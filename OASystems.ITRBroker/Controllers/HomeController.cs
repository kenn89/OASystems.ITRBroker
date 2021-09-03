using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace OASystems.ITRBroker
{
    [Route("/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        // GET: <HomeController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET <ValuesController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST <HomeController>
        [HttpPost]
        public async Task PostAsync([FromBody] ITRJob itrJob)
        {
            await ITRJobSchedulerFactory.ResecheduleJob(itrJob);
        }

        // PUT <HomeController>
        [HttpPut]
        public async Task Put([FromBody] ITRJob itrJob)
        {
            await ITRJobSchedulerFactory.ScheduleNewJob(itrJob);
        }

        // DELETE <HomeController>/5
        [HttpDelete("{id}")]
        public async Task Delete(int id)
        {
            await ITRJobSchedulerFactory.DeleteJob(id);
        }
    }
}
