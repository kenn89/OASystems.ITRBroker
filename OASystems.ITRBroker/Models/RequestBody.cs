using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using OASystems.ITRBroker.Attributes;

namespace OASystems.ITRBroker.Models
{
    public class RequestBody
    {
        [JsonIgnore]
        public Guid ID { get; set; }
        [JsonIgnore]
        public string Name { get; set; }
        [CronExpression]
        public string CronSchedule { get; set; }
        public bool? IsScheduled { get; set; }
    }
}
