using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace OASystems.ITRBroker.Models
{
    public class ITRJob
    {
        [JsonIgnore]
        public Guid ID { get; set; }
        [DisplayName("Name")]
        public string Name { get; set; }
        [DisplayName("Cron Schedule")]
        public string CronSchedule { get; set; }
        [DisplayName("Previous Run Time")]
        public DateTime? PreviousFireTimeUtc { get; set; }
        [DisplayName("Next Run Time")]
        public DateTime? NextFireTimeUtc { get; set; }
        [DisplayName("Scheduled")]
        public bool? IsScheduled { get; set; }
        [DisplayName("CRM URL")]
        [JsonIgnore]
        public string CrmUrl { get; set; }
        [JsonIgnore]
        public string CrmClientID { get; set; }
        [JsonIgnore]
        public string CrmSecret { get; set; }
        [DisplayName("Enabled")]
        [JsonIgnore]
        public bool IsEnabled { get; set; }
    }
}
