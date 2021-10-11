using OASystems.ITRBroker.Attributes;
using Quartz;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OASystems.ITRBroker.Models
{
    public class ITRJobMetadata : IValidatableObject
    {
        [JsonIgnore]
        public Guid ID { get; set; }
        [DisplayName("Name")]
        [Required]
        public string Name { get; set; }
        [DisplayName("API Username")]
        [JsonIgnore]
        public string ApiUsername { get; set; }
        [DisplayName("API Password")]
        [JsonIgnore]
        public string ApiPassword { get; set; }
        [DisplayName("CRM URL")]
        [JsonIgnore]
        [Required]
        public string CrmUrl { get; set; }
        [JsonIgnore]
        [Required]
        public string CrmClientID { get; set; }
        [JsonIgnore]
        [Required]
        public string CrmSecret { get; set; }
        [DisplayName("Cron Schedule")]
        [CronExpression]
        public string CronSchedule { get; set; }
        [DisplayName("Scheduled")]
        public bool IsScheduled { get; set; }
        [DisplayName("Previous Run Time")]
        public DateTime? PreviousFireTimeUtc { get; set; }
        [DisplayName("Next Run Time")]
        public DateTime? NextFireTimeUtc { get; set; }
        [DisplayName("Enabled")]
        [JsonIgnore]
        public bool IsEnabled { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsScheduled && (CronSchedule == null || !CronExpression.IsValidExpression(CronSchedule)))
            {
                yield return new ValidationResult("The \"CronSchedule\" field must contain a valid Cron Expression when the \"IsScheduled\" field is set to true.");
            }
        }
    }
}
