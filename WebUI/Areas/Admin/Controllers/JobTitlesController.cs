using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles="Admin")]
    public class JobTitlesController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        public JobTitlesController(ApplicationDbContext ctx) { _ctx = ctx; }

        public async Task<IActionResult> Index()
        {
            var list = await _ctx.JobTitles
                .ToListAsync();
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadParentsAsync();
            return View(new JobTitle());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobTitle model)
        {
            if (!ModelState.IsValid) { return View(model); }
            _ctx.JobTitles.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var jt = await _ctx.JobTitles.FindAsync(id);
            if (jt == null) return NotFound();
            await LoadParentsAsync(excludeId: id);
            return View(jt);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, JobTitle model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) { await LoadParentsAsync(excludeId: id);
                return View(model); }

            var entity = await _ctx.JobTitles.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            entity.Name = model.Name;

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var jt = await _ctx.JobTitles.FindAsync(id);
            if (jt != null)
            {
                // Kısıtlama: Çocuk veya çalışan bağlıysa önce taşı/mapping değiştir.
                _ctx.JobTitles.Remove(jt);
                await _ctx.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadParentsAsync(int? selectedId = null, int? excludeId = null)
        {
            var q = _ctx.JobTitles.AsQueryable();
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);

            var items = await q
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            ViewBag.ParentId = new SelectList(items, "Id", "Name", selectedId);
        }
    }
}
