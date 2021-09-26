using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OASystems.ITRBroker.Models;
using OASystems.ITRBroker.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OASystems.ITRBroker.Controllers
{
    public class ITRJobMetadataController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly ISchedulerService _schedulerService;

        public ITRJobMetadataController(DatabaseContext context, ISchedulerService schedulerService)
        {
            _context = context;
            _schedulerService = schedulerService;
        }

        // GET: ITRJobMetadata
        public async Task<IActionResult> Index()
        {
            return View(await _context.ITRJobMetadata.ToListAsync());
        }

        // GET: ITRJobMetadata/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iTRJobMetadata = await _context.ITRJobMetadata
                .FirstOrDefaultAsync(m => m.ID == id);
            if (iTRJobMetadata == null)
            {
                return NotFound();
            }

            return View(iTRJobMetadata);
        }

        // GET: ITRJobMetadata/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ITRJobMetadata/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJobMetadata iTRJobMetadata)
        {
            if (ModelState.IsValid)
            {
                iTRJobMetadata.ID = Guid.NewGuid();
                _context.Add(iTRJobMetadata);
                await _context.SaveChangesAsync();
                await _schedulerService.SyncDbToSchedulerById(iTRJobMetadata.ID);
                return RedirectToAction(nameof(Index));
            }
            return View(iTRJobMetadata);
        }

        // GET: ITRJobMetadata/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iTRJobMetadata = await _context.ITRJobMetadata.FindAsync(id);
            if (iTRJobMetadata == null)
            {
                return NotFound();
            }
            return View(iTRJobMetadata);
        }

        // POST: ITRJobMetadata/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ID,Name,ApiUsername,ApiPassword,CrmUrl,CrmClientID,CrmSecret,CronSchedule,IsScheduled,PreviousFireTimeUtc,NextFireTimeUtc,IsEnabled")] ITRJobMetadata iTRJobMetadata)
        {
            if (id != iTRJobMetadata.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var itrJobMetadataLocal = await _context.ITRJobMetadata.Where(x => x.ID == id).FirstOrDefaultAsync();
                    iTRJobMetadata.PreviousFireTimeUtc = itrJobMetadataLocal.PreviousFireTimeUtc;
                    iTRJobMetadata.NextFireTimeUtc = itrJobMetadataLocal.NextFireTimeUtc;
                    _context.Entry(itrJobMetadataLocal).State = EntityState.Detached;

                    _context.Entry(iTRJobMetadata).State = EntityState.Modified;
                    _context.Update(iTRJobMetadata);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ITRJobExists(iTRJobMetadata.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                await _schedulerService.SyncDbToSchedulerById(iTRJobMetadata.ID);
                return RedirectToAction(nameof(Index));
            }
            return View(iTRJobMetadata);
        }

        // GET: ITRJobMetadata/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iTRJobMetadata = await _context.ITRJobMetadata
                .FirstOrDefaultAsync(m => m.ID == id);
            if (iTRJobMetadata == null)
            {
                return NotFound();
            }

            return View(iTRJobMetadata);
        }

        // POST: ITRJobMetadata/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var iTRJobMetadata = await _context.ITRJobMetadata.FindAsync(id);
            _context.ITRJobMetadata.Remove(iTRJobMetadata);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ITRJobExists(Guid id)
        {
            return _context.ITRJobMetadata.Any(e => e.ID == id);
        }
    }
}
