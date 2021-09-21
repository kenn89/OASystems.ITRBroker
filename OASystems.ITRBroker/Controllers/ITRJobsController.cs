using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OASystems.ITRBroker.Models;

namespace OASystems.ITRBroker.Controllers
{
    public class ITRJobsController : Controller
    {
        private readonly DatabaseContext _context;

        public ITRJobsController(DatabaseContext context)
        {
            _context = context;
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

            var iTRJob = await _context.ITRJob
                .FirstOrDefaultAsync(m => m.ID == id);
            if (iTRJob == null)
            {
                return NotFound();
            }

            return View(iTRJob);
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
        public async Task<IActionResult> Create([Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJob iTRJob)
        {
            if (ModelState.IsValid)
            {
                iTRJob.ID = Guid.NewGuid();
                _context.Add(iTRJob);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(iTRJob);
        }

        // GET: ITRJobs/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iTRJob = await _context.ITRJob.FindAsync(id);
            if (iTRJob == null)
            {
                return NotFound();
            }
            return View(iTRJob);
        }

        // POST: ITRJobs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJob iTRJob)
        {
            if (id != iTRJob.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(iTRJob);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ITRJobExists(iTRJob.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(iTRJob);
        }

        // GET: ITRJobs/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iTRJob = await _context.ITRJob
                .FirstOrDefaultAsync(m => m.ID == id);
            if (iTRJob == null)
            {
                return NotFound();
            }

            return View(iTRJob);
        }

        // POST: ITRJobs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var iTRJob = await _context.ITRJob.FindAsync(id);
            _context.ITRJob.Remove(iTRJob);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ITRJobExists(Guid id)
        {
            return _context.ITRJob.Any(e => e.ID == id);
        }
    }
}
