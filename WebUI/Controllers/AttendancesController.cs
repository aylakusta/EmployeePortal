using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Authorize] // Kullanıcı (personel) erişir
    public class AttendancesController : Controller
    {
        // Controller içine (field değil, property):
        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Kayıt sahipliği kontrolü için yardımcı:
        private bool IsOwner(string? userId) =>
            !string.IsNullOrEmpty(userId) && string.Equals(userId, CurrentUserId, StringComparison.Ordinal);

        private readonly ApplicationDbContext _ctx;

        public AttendancesController(ApplicationDbContext ctx) => _ctx = ctx;

        // Liste
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId;
            var items = await _ctx.Attendances
                .Where(a => a.UserId == uid)
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(items);
        }

        // Yeni bildirim formu
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Attendance { });
        }

        // Yeni bildirim kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Attendance input)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

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

            // Otomatik DocumentRequest oluştur
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

            // İlgili talep ekranına yönlendir (GetRequest kullanılıyor)
            return RedirectToAction("GetRequest", "Documents", new { id = req.Id });
        }

        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var a = await _ctx.Attendances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            if (!User.IsInRole("Admin") && !IsOwner(a.UserId))
                return Forbid(); // AccessDenied’a gitsin

            return View(a);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Attendance input)
        {
            var a = await _ctx.Attendances.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            if (!User.IsInRole("Admin") && !IsOwner(a.UserId))
                return Forbid();

            // ... (sadece izin verdiğin alanları güncelle)
            a.Description = input.Description;
            a.Reason = input.Reason;

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var a = await _ctx.Attendances.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            if (!User.IsInRole("Admin") && !IsOwner(a.UserId))
                return Forbid();

            _ctx.Attendances.Remove(a);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


    }
}