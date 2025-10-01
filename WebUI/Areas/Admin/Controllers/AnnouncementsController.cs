using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _ctx;

        public AnnouncementsController(ApplicationDbContext ctx)
        {
            _ctx = ctx;
        }

        // GET: /Admin/Announcements/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _ctx.Announcements
                .FirstOrDefaultAsync(a => a.Id == id.Value);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: /Admin/Announcements/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Admin/Announcements/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Announcement model)
        {
            if (!ModelState.IsValid) return View(model);

            model.CreatedAt = DateTime.UtcNow;
            _ctx.Announcements.Add(model);
            await _ctx.SaveChangesAsync();

            TempData["Success"] = "Duyuru oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Announcements/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _ctx.Announcements.FindAsync(id.Value);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: /Admin/Announcements/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Announcement model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            try
            {
                // Sadece düzenlenebilir alanları güncelle
                var entity = await _ctx.Announcements.FirstOrDefaultAsync(a => a.Id == id);
                if (entity == null) return NotFound();

                entity.Title = model.Title;
                entity.Body = model.Body;
                // CreatedAt'i değiştirmiyoruz

                await _ctx.SaveChangesAsync();
                TempData["Success"] = "Duyuru güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await AnnouncementExists(id))
                    return NotFound();
                throw;
            }
        }

        // GET: /Admin/Announcements/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _ctx.Announcements
                .FirstOrDefaultAsync(a => a.Id == id.Value);

            if (item == null) return NotFound();

            return View(item);
        }

        // POST: /Admin/Announcements/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _ctx.Announcements.FindAsync(id);
            if (item == null) return NotFound();

            _ctx.Announcements.Remove(item);
            await _ctx.SaveChangesAsync();

            TempData["Success"] = "Duyuru silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q = null)
        {
            var query = _ctx.Announcements
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    (a.Title ?? "").Contains(q) ||
                    (a.Body ?? "").Contains(q));

            var list = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.Q = q;
            return View(list);
        }

        private async Task<bool> AnnouncementExists(int id)
            => await _ctx.Announcements.AnyAsync(e => e.Id == id);
    }
}
