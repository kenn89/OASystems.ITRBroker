using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Model
{
    public class RequestBody
    {
        public string CronSchedule { get; set; }

        public bool? IsScheduled { get; set; }
    }
}
