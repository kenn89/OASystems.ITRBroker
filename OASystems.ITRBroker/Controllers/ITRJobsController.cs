using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using OASystems.ITRBroker.Handler;

namespace OASystems.ITRBroker.Controllers
{
    public class ITRJobsController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly ITRJobHandler _itrJobHandler;

        public ITRJobsController(DatabaseContext context, ISchedulerService schedulerService)
        {
            _context = context;
            _itrJobHandler = new ITRJobHandler(context, schedulerService);
        }

        // GET: ITRJobs
        public async Task<IActionResult> Index()
        {
            return View(await _context.ITRJob.ToListAsync());
        }

        // GET: ITRJobs/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var itrJob = await _context.ITRJob
                .FirstOrDefaultAsync(m => m.ID == id);
            if (itrJob == null)
            {
                return NotFound();
            }

            return View(itrJob);
        }

        // GET: ITRJobs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ITRJobs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJob itrJob)
        {
            if (ModelState.IsValid)
            {
                itrJob = _itrJobHandler.ValidateCronScheduleAndUpdateITRJob(itrJob, itrJob.CronSchedule);
                itrJob.ID = Guid.NewGuid();
                _context.Add(itrJob);
                await _context.SaveChangesAsync();
                await _itrJobHandler.SyncDbToSchedulerByJobID(itrJob.ID);
                return RedirectToAction(nameof(Index));
            }
            return View(itrJob);
        }

        // GET: ITRJobs/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var itrJob = await _context.ITRJob.FindAsync(id);
            if (itrJob == null)
            {
                return NotFound();
            }
            return View(itrJob);
        }

        // POST: ITRJobs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJob itrJob)
        {
            if (id != itrJob.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                itrJob = _itrJobHandler.ValidateCronScheduleAndUpdateITRJob(itrJob, itrJob.CronSchedule);
                try
                {
                    _context.Update(itrJob);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ITRJobExists(itrJob.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                await _itrJobHandler.SyncDbToSchedulerByJobID(itrJob.ID);
                return RedirectToAction(nameof(Index));
            }
            return View(itrJob);
        }

        // GET: ITRJobs/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var itrJob = await _context.ITRJob
                .FirstOrDefaultAsync(m => m.ID == id);
            if (itrJob == null)
            {
                return NotFound();
            }

            return View(itrJob);
        }

        // POST: ITRJobs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var itrJob = await _context.ITRJob.FindAsync(id);
            _context.ITRJob.Remove(itrJob);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ITRJobExists(Guid id)
        {
            return _context.ITRJob.Any(e => e.ID == id);
        }
    }
}
