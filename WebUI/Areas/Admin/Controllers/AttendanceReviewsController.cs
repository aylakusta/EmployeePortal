using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using WebUI.Data;
using WebUI.Models;

// PDF
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Document = QuestPDF.Fluent.Document;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AttendanceReviewsController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        public AttendanceReviewsController(ApplicationDbContext ctx) => _ctx = ctx;

        // Liste + filtre + arama + tarih kitleme için min/max
        public async Task<IActionResult> Index(
            AttendanceReason? reason,
            string status = "all",  // all | open | resolved
            DateTime? from = null,
            DateTime? to = null,
            string? q = null)
        {
            var qset = _ctx.Attendances
                .Include(a => a.DocumentRequestRef)
                .OrderByDescending(a => a.CreatedAt)
                .AsQueryable();

            if (reason.HasValue) qset = qset.Where(a => a.Reason == reason.Value);
            if (status == "open") qset = qset.Where(a => !a.IsResolved);
            else if (status == "resolved") qset = qset.Where(a => a.IsResolved);

            if (from.HasValue) qset = qset.Where(a => a.CreatedAt >= from.Value.Date);
            if (to.HasValue)   qset = qset.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));

            // Arama: kullanıcı adı/e-posta + açıklama
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.Trim().ToLower();
                // eşleşen kullanıcı id'leri
                var userIds = await _ctx.Users
                    .Where(u => (u.Email ?? "").ToLower().Contains(qLower) || (u.UserName ?? "").ToLower().Contains(qLower))
                    .Select(u => u.Id).ToListAsync();

                qset = qset.Where(a =>
                    (a.Description ?? "").ToLower().Contains(qLower) ||
                    userIds.Contains(a.UserId));
            }

            // Tarih kitleme için min/max
            DateTime? minDate = await _ctx.Attendances.AnyAsync() ? await _ctx.Attendances.MinAsync(a => a.CreatedAt) : null;
            DateTime? maxDate = await _ctx.Attendances.AnyAsync() ? await _ctx.Attendances.MaxAsync(a => a.CreatedAt) : null;
            ViewBag.MinDate = minDate?.ToString("yyyy-MM-dd");
            ViewBag.MaxDate = maxDate?.ToString("yyyy-MM-dd");

            // Kullanıcı gösterimi için basit map
            ViewBag.UserMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));

            // Filtrelerin geri basılması
            ViewBag.Reason = reason;
            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Query = q ?? "";

            return View(await qset.ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var a = await _ctx.Attendances
                .Include(x => x.DocumentRequestRef)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();
            return View(a);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkResolved(int id)
        {
            var a = await _ctx.Attendances.FindAsync(id);
            if (a == null) return NotFound();
            a.IsResolved = true;
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id)
        {
            var a = await _ctx.Attendances.FindAsync(id);
            if (a == null) return NotFound();
            a.IsResolved = false;
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // === CSV Export (filtrelerle)
        public async Task<IActionResult> ExportCsv(AttendanceReason? reason, string status = "all",
                                                   DateTime? from = null, DateTime? to = null, string? q = null)
        {
            var list = await GetFiltered(reason, status, from, to, q).ToListAsync();
            var userMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));

            string CsvEscape(string s)
            {
                if (s == null) return "";
                var needs = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
                s = s.Replace("\"", "\"\"");
                return needs ? $"\"{s}\"" : s;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Date,User,Reason,Description,Resolved,RequestId");
            foreach (var a in list)
            {
                var reasonLabel = a.Reason.ToString();
                var user = userMap.TryGetValue(a.UserId, out var name) ? name : a.UserId;
                sb.AppendLine(string.Join(",", new[]
                {
                    a.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    CsvEscape(user),
                    CsvEscape(reasonLabel),
                    CsvEscape(a.Description ?? ""),
                    a.IsResolved ? "Yes" : "No",
                    a.DocumentRequestId?.ToString() ?? ""
                }));
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"attendances_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        // === PDF Export (QuestPDF ile)
        public async Task<IActionResult> ExportPdf(AttendanceReason? reason, string status = "all",
                                                   DateTime? from = null, DateTime? to = null, string? q = null)
        {
            var list = await GetFiltered(reason, status, from, to, q).ToListAsync();
            var userMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));

            byte[] pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Katılım Bildirimleri").FontSize(18).SemiBold();
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); // Date
                            cols.RelativeColumn(3); // User
                            cols.RelativeColumn(2); // Reason
                            cols.RelativeColumn(5); // Description
                            cols.RelativeColumn(2); // Status
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Text("Tarih");
                            header.Cell().Text("Kullanıcı");
                            header.Cell().Text("Mazeret");
                            header.Cell().Text("Açıklama");
                            header.Cell().Text("Durum");
                        });

                        foreach (var a in list)
                        {
                            var user = userMap.TryGetValue(a.UserId, out var name) ? name : a.UserId;
                            table.Cell().Text(a.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                            table.Cell().Text(user);
                            table.Cell().Text(a.Reason.ToString());
                            table.Cell().Text(a.Description ?? "");
                            table.Cell().Text(a.IsResolved ? "Tamamlandı" : "Bekliyor");
                        }
                    });
                    page.Footer().AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"attendances_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        // === Yazdır görünümü
        public async Task<IActionResult> Print(AttendanceReason? reason, string status = "all",
                                               DateTime? from = null, DateTime? to = null, string? q = null)
        {
            ViewBag.UserMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));
            return View(await GetFiltered(reason, status, from, to, q).ToListAsync());
        }

        // Ortak filtre sorgusu
        private IQueryable<Attendance> GetFiltered(AttendanceReason? reason, string status, DateTime? from, DateTime? to, string? q)
        {
            var qset = _ctx.Attendances.Include(a => a.DocumentRequestRef).AsQueryable();

            if (reason.HasValue) qset = qset.Where(a => a.Reason == reason.Value);
            if (status == "open") qset = qset.Where(a => !a.IsResolved);
            else if (status == "resolved") qset = qset.Where(a => a.IsResolved);

            if (from.HasValue) qset = qset.Where(a => a.CreatedAt >= from.Value.Date);
            if (to.HasValue)   qset = qset.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.Trim().ToLower();
                var userIds = _ctx.Users
                    .Where(u => (u.Email ?? "").ToLower().Contains(qLower) || (u.UserName ?? "").ToLower().Contains(qLower))
                    .Select(u => u.Id);

                qset = qset.Where(a =>
                    (a.Description ?? "").ToLower().Contains(qLower) ||
                    userIds.Contains(a.UserId));
            }

            return qset.OrderByDescending(a => a.CreatedAt);
        }
    }
}
