using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

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
        [JsonIgnore]
        public string CrmUrl { get; set; }
        [JsonIgnore]
        public string CrmClientID { get; set; }
        [JsonIgnore]
        public string CrmSecret { get; set; }
    }
}
