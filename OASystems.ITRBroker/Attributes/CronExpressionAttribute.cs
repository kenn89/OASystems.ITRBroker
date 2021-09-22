using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Quartz;

namespace OASystems.ITRBroker.Attributes
{
    public class CronExpressionAttribute : ValidationAttribute
    {
        public string GetErrorMessage()
        {
            return "The Cron Expression is invalid.";
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                string cronExpression = value.ToString();

                if (cronExpression == "" || CronExpression.IsValidExpression(cronExpression))
                {
                    return ValidationResult.Success;
                }
                else
                {
                    return new ValidationResult(GetErrorMessage());
                }
            }
            else
            {
                return ValidationResult.Success;
            }
        }
    }
}
