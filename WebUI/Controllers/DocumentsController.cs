using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly IWebHostEnvironment _env;

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private bool IsOwner(string? userId) =>
            !string.IsNullOrEmpty(userId) &&
            string.Equals(userId, CurrentUserId, StringComparison.Ordinal);

        public DocumentsController(ApplicationDbContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
        }

        // === Talep tipi bazlı form (GET) ===
        [HttpGet]
        public async Task<IActionResult> ViewRequest(int id)
        {
            var req = await _ctx.DocumentRequests
                .Include(r => r.AttendanceRef)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null) return NotFound();
            if (!User.IsInRole("Admin") && !IsOwner(req.UserId)) return Forbid();

            if (req.Type == DocumentRequestType.MedicalReport)
                return View("RequestReport", req); // Rapor yükleme şablonu
            else
                return View("RequestLeave", req);  // İzin formu şablonu
        }

        // === Rapor yükleme (MedicalReport) ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReport(int id, IFormFile? reportFile, DateTime? startDate, DateTime? endDate)
        {
            var req = await _ctx.DocumentRequests
                .Include(r => r.AttendanceRef)
                .FirstOrDefaultAsync(r => r.Id == id && r.Type == DocumentRequestType.MedicalReport);

            if (req == null) return NotFound();
            if (!User.IsInRole("Admin") && !IsOwner(req.UserId)) return Forbid();

            if (reportFile == null || reportFile.Length == 0)
                ModelState.AddModelError("", "Lütfen rapor dosyası yükleyiniz.");

            if (startDate.HasValue && endDate.HasValue && endDate < startDate)
                ModelState.AddModelError("", "Bitiş tarihi başlangıç tarihinden önce olamaz.");

            if (!ModelState.IsValid)
                return View("RequestReport", req);

            // Dosyayı kaydet
            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "reports");
            Directory.CreateDirectory(uploadsDir);
            var safeName = Path.GetFileName(reportFile!.FileName);
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeName}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            using (var fs = new FileStream(fullPath, FileMode.Create))
                await reportFile.CopyToAsync(fs);

            req.UploadedFileName = safeName;
            req.UploadedFilePath = $"/uploads/reports/{fileName}";
            req.StartDate = startDate;
            req.EndDate = endDate;
            req.CompletedAt = DateTime.UtcNow;

            // Attendance tamamlandı
            if (req.AttendanceRef != null)
                req.AttendanceRef.IsResolved = true;

            // Seçilen günler için Transport = WillUse=false
            if (startDate.HasValue && endDate.HasValue)
                await ApplyTransportBlackout(req.UserId!, startDate.Value.Date, endDate.Value.Date);

            await _ctx.SaveChangesAsync();
            return View("Success", req);
        }

        // === Yıllık izin formu (AnnualLeave) ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestLeave(int id, DateTime? startDate, DateTime? endDate)
        {
            var req = await _ctx.DocumentRequests
                .Include(r => r.AttendanceRef)
                .FirstOrDefaultAsync(r => r.Id == id && r.Type == DocumentRequestType.AnnualLeave);

            if (req == null) return NotFound();
            if (!User.IsInRole("Admin") && !IsOwner(req.UserId)) return Forbid();

            if (!startDate.HasValue || !endDate.HasValue)
                ModelState.AddModelError("", "Lütfen başlangıç ve bitiş tarihlerinin ikisini de seçiniz.");
            else if (endDate < startDate)
                ModelState.AddModelError("", "Bitiş tarihi başlangıç tarihinden önce olamaz.");

            if (!ModelState.IsValid)
                return View("RequestLeave", req);

            req.StartDate = startDate!.Value.Date;
            req.EndDate   = endDate!.Value.Date;
            req.CompletedAt = DateTime.UtcNow;

            if (req.AttendanceRef != null)
                req.AttendanceRef.IsResolved = true;

            // Seçilen günler için Transport = WillUse=false
            await ApplyTransportBlackout(req.UserId!, req.StartDate.Value, req.EndDate.Value);

            await _ctx.SaveChangesAsync();
            return View("Success", req);
        }

        // === Yardımcı: seçilen tarih aralığı için kişiye Transport "WillUse=false" yaz/ güncelle ===
        private async Task ApplyTransportBlackout(string userId, DateTime start, DateTime end)
        {
            var dates = Enumerable.Range(0, (end - start).Days + 1)
                                  .Select(offset => start.AddDays(offset).Date);

            var existing = await _ctx.Transports
                .Where(t => t.UserId == userId && t.TravelDate >= start && t.TravelDate <= end)
                .ToListAsync();

            foreach (var d in dates)
            {
                var row = existing.FirstOrDefault(t => t.TravelDate.Date == d);
                if (row == null)
                {
                    _ctx.Transports.Add(new Transport
                    {
                        UserId = userId,
                        TravelDate = d,
                        From = null,
                        To = null,
                        WillUse = false,
                        Notes = "Otomatik: İzin/Rapor kapsamı"
                    });
                }
                else
                {
                    row.WillUse = false;
                    row.Notes = "Otomatik: İzin/Rapor kapsamı";
                }
            }
        }

        // === Kullanıcının talep listesi (tek aksiyon) ===
        [HttpGet]
        public async Task<IActionResult> Index(DocumentRequestType? type, string status = "all")
        {
            var uid = CurrentUserId;

            var q = _ctx.DocumentRequests
                        .Where(r => r.UserId == uid)               // sadece kendi kayıtları
                        .OrderByDescending(r => r.CreatedAt)
                        .AsQueryable();

            if (type.HasValue) q = q.Where(r => r.Type == type.Value);
            if (status == "incomplete")      q = q.Where(r => r.CompletedAt == null);
            else if (status == "completed")  q = q.Where(r => r.CompletedAt != null);

            ViewBag.Type = type;
            ViewBag.Status = status;

            var list = await q.AsNoTracking().ToListAsync();
            return View(list);
        }

        // === Tekil talep görüntüleme (tipine göre uygun forma gönderir) ===
        [HttpGet]
        public async Task<IActionResult> GetRequest(int id)
        {
            var req = await _ctx.DocumentRequests
                .Include(r => r.AttendanceRef)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null) return NotFound();
            if (!User.IsInRole("Admin") && !IsOwner(req.UserId)) return Forbid();

            if (req.Type == DocumentRequestType.MedicalReport)
                return View("RequestReport", req);
            else
                return View("RequestLeave", req);
        }
    }
}
