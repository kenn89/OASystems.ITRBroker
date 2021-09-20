using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OASystems.ITRBroker.Services;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Handler;

namespace OASystems.ITRBroker.Pages.Admin
{
    public class ITRJobModel : PageModel
    {
        private readonly ITRJobHandler _itrJobHandler;
        private readonly ISchedulerService _schedulerService;

        public ITRJobModel(DatabaseContext context, ISchedulerService schedulerService)
        {
            _itrJobHandler = new ITRJobHandler(context);
            _schedulerService = schedulerService;
        }

        public List<ITRJob> ITRJobList { get; set; }

        public void OnGet()
        {
            ITRJobList = new List<ITRJob>();

            var itrJobListFromDb = _itrJobHandler.GetAllITRJobs();
            var itrJobListFromScheduler = _schedulerService.GetAllScheduledJobs();

            foreach(var itrJobFromDb in itrJobListFromDb)
            {
                foreach(var itrJobFromScheduler in itrJobListFromScheduler)
                {
                    if (itrJobFromDb.ID == itrJobFromScheduler.ID)
                    {
                        itrJobFromDb.PreviousFireTimeUtc = itrJobFromScheduler.PreviousFireTimeUtc;
                        itrJobFromDb.NextFireTimeUtc = itrJobFromScheduler.NextFireTimeUtc;
                        break;
                    }
                }
                ITRJobList.Add(itrJobFromDb);
            }
        }
    }
}
