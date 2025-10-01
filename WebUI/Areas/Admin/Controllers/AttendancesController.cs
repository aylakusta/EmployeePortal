using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models; // << ÖNEMLİ

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AttendancesController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        public AttendancesController(ApplicationDbContext ctx) => _ctx = ctx;

        // /Admin/Attendances
        public async Task<IActionResult> Index()
        {
            var set = _ctx.Attendances;
            if (set == null)
                return Problem("Attendances DbSet is not available (null).");

            var list = await set
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt) // CreatedAt non-nullable ise sorun yok
                .ToListAsync();

            return View(list);
        }

        [HttpGet]
        public IActionResult Create()
            => View(new Attendance { CreatedAt = DateTime.UtcNow.Date });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("Forms")]
        public async Task<IActionResult> Create(Attendance input)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return Challenge(); // login yoksa

            // UserId formdan gelmiyor; server tarafında set edeceğiz
            ModelState.Remove(nameof(Attendance.UserId));

            if (!Enum.IsDefined(typeof(AttendanceReason), input.Reason))
                ModelState.AddModelError(nameof(Attendance.Reason), "Geçerli bir mazeret seçiniz.");

            if (!ModelState.IsValid)
                return View(input);

            var attendance = new Attendance
            {
                UserId = uid,
                Reason = input.Reason,
                Description = input.Description
            };
            _ctx.Attendances.Add(attendance);
            await _ctx.SaveChangesAsync();

            // (Varsa) DocumentRequest oluşturma ve yönlendirme burada devam
            var type = attendance.Reason == AttendanceReason.Report
                ? DocumentRequestType.MedicalReport
                : DocumentRequestType.AnnualLeave;

            var req = new DocumentRequest
            {
                UserId = uid,
                Type = type,
                AttendanceId = attendance.Id
            };
            _ctx.DocumentRequests.Add(req);
            await _ctx.SaveChangesAsync();

            return RedirectToAction("Request", "Documents", new { id = req.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();

            var set = _ctx.Attendances;
            if (set == null)
                return Problem("Attendances DbSet is not available (null).");

            var item = await set.FindAsync(id.Value);
            if (item is null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Attendance model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var set = _ctx.Attendances;
            if (set == null)
                return Problem("Attendances DbSet is not available (null).");

            var entity = await set.FirstOrDefaultAsync(a => a.Id == id);
            if (entity is null) return NotFound();

            entity.UserId = model.UserId;
            entity.CreatedAt = model.CreatedAt == default ? entity.CreatedAt : model.CreatedAt;
            entity.Description = model.Description;

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var set = _ctx.Attendances;
            if (set == null)
                return Problem("Attendances DbSet is not available (null).");

            var item = await set.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id.Value);
            if (item is null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var set = _ctx.Attendances;
            if (set == null)
                return Problem("Attendances DbSet is not available (null).");

            var item = await set.FindAsync(id);
            if (item is null) return NotFound();

            set.Remove(item);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}