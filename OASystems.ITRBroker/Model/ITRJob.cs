using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Model
{
    public class ITRJob
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public string CronSchedule { get; set; }
        public DateTime? PreviousFireTimeUtc { get; set; }
        public DateTime? NextFireTimeUtc { get; set; }
        public bool? IsScheduled { get; set; }
    }
}
