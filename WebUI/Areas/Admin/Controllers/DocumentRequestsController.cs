using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
    public class DocumentRequestsController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly IWebHostEnvironment _env;

        public DocumentRequestsController(ApplicationDbContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
        }

        // Liste + filtre (type, status) + arama + tarih
        public async Task<IActionResult> Index(
            DocumentRequestType? type,
            string status = "all", // all | incomplete | completed
            DateTime? from = null,
            DateTime? to = null,
            string? q = null)
        {
            var qset = _ctx.DocumentRequests
                .Include(r => r.AttendanceRef)
                .OrderByDescending(r => r.CreatedAt)
                .AsQueryable();

            if (type.HasValue) qset = qset.Where(r => r.Type == type.Value);
            if (status == "incomplete") qset = qset.Where(r => r.CompletedAt == null);
            else if (status == "completed") qset = qset.Where(r => r.CompletedAt != null);

            if (from.HasValue) qset = qset.Where(r => r.CreatedAt >= from.Value.Date);
            if (to.HasValue)   qset = qset.Where(r => r.CreatedAt < to.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.Trim().ToLower();
                var userIds = await _ctx.Users
                    .Where(u => (u.Email ?? "").ToLower().Contains(qLower) || (u.UserName ?? "").ToLower().Contains(qLower))
                    .Select(u => u.Id).ToListAsync();

                qset = qset.Where(r =>
                    (r.UploadedFileName ?? "").ToLower().Contains(qLower) ||
                    userIds.Contains(r.UserId));
            }

            // Tarih kitleme min/max
            DateTime? minDate = await _ctx.DocumentRequests.AnyAsync() ? await _ctx.DocumentRequests.MinAsync(r => r.CreatedAt) : null;
            DateTime? maxDate = await _ctx.DocumentRequests.AnyAsync() ? await _ctx.DocumentRequests.MaxAsync(r => r.CreatedAt) : null;
            ViewBag.MinDate = minDate?.ToString("yyyy-MM-dd");
            ViewBag.MaxDate = maxDate?.ToString("yyyy-MM-dd");

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Query = q ?? "";

            return View(await qset.ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var r = await _ctx.DocumentRequests
                .Include(x => x.AttendanceRef)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) return NotFound();
            return View(r);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var r = await _ctx.DocumentRequests
                .Include(x => x.AttendanceRef)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) return NotFound();

            if (r.CompletedAt == null)
                r.CompletedAt = DateTime.UtcNow;

            if (r.AttendanceRef != null)
                r.AttendanceRef.IsResolved = true;

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public IActionResult Download(int id)
        {
            var r = _ctx.DocumentRequests.Find(id);
            if (r == null || string.IsNullOrWhiteSpace(r.UploadedFilePath))
                return NotFound();

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rel = r.UploadedFilePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var full = Path.Combine(webRoot, rel);

            if (!System.IO.File.Exists(full))
                return NotFound();

            var downloadName = string.IsNullOrWhiteSpace(r.UploadedFileName) ? Path.GetFileName(full) : r.UploadedFileName;
            return PhysicalFile(full, "application/octet-stream", downloadName);
        }

        // === CSV Export
        public async Task<IActionResult> ExportCsv(DocumentRequestType? type, string status = "all",
                                                   DateTime? from = null, DateTime? to = null, string? q = null)
        {
            var list = await GetFiltered(type, status, from, to, q).ToListAsync();
            var userMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));

            string CsvEscape(string s)
            {
                if (s == null) return "";
                var needs = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
                s = s.Replace("\"", "\"\"");
                return needs ? $"\"{s}\"" : s;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Date,Type,User,Start,End,FileName,Completed");
            foreach (var r in list)
            {
                var user = userMap.TryGetValue(r.UserId, out var name) ? name : r.UserId;
                sb.AppendLine(string.Join(",", new[]
                {
                    r.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    r.Type.ToString(),
                    CsvEscape(user),
                    r.StartDate?.ToString("yyyy-MM-dd") ?? "",
                    r.EndDate?.ToString("yyyy-MM-dd") ?? "",
                    CsvEscape(r.UploadedFileName ?? ""),
                    r.CompletedAt != null ? "Yes" : "No"
                }));
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"document_requests_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        // === PDF Export
        public async Task<IActionResult> ExportPdf(DocumentRequestType? type, string status = "all",
                                                   DateTime? from = null, DateTime? to = null, string? q = null)
        {
            var list = await GetFiltered(type, status, from, to, q).ToListAsync();
            var userMap = await _ctx.Users.ToDictionaryAsync(u => u.Id, u => (u.UserName ?? u.Email ?? u.Id));

            byte[] pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Evrak Talepleri").FontSize(18).SemiBold();
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); // Date
                            cols.RelativeColumn(2); // Type
                            cols.RelativeColumn(3); // User
                            cols.RelativeColumn(2); // Start
                            cols.RelativeColumn(2); // End
                            cols.RelativeColumn(3); // File
                            cols.RelativeColumn(2); // Status
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Tarih");
                            header.Cell().Text("Tür");
                            header.Cell().Text("Kullanıcı");
                            header.Cell().Text("Başlangıç");
                            header.Cell().Text("Bitiş");
                            header.Cell().Text("Dosya");
                            header.Cell().Text("Durum");
                        });

                        foreach (var r in list)
                        {
                            var user = userMap.TryGetValue(r.UserId, out var name) ? name : r.UserId;
                            table.Cell().Text(r.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                            table.Cell().Text(r.Type.ToString());
                            table.Cell().Text(user);
                            table.Cell().Text(r.StartDate?.ToString("dd.MM.yyyy") ?? "—");
                            table.Cell().Text(r.EndDate?.ToString("dd.MM.yyyy") ?? "—");
                            table.Cell().Text(r.UploadedFileName ?? "—");
                            table.Cell().Text(r.CompletedAt != null ? "Tamamlandı" : "Eksik");
                        }
                    });
                    page.Footer().AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"document_requests_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        // === Yazdır görünümü
        public async Task<IActionResult> Print(DocumentRequestType? type, string status = "all",
                                               DateTime? from = null, DateTime? to = null, string? q = null)
        {
            return View(await GetFiltered(type, status, from, to, q).ToListAsync());
        }

        private IQueryable<DocumentRequest> GetFiltered(DocumentRequestType? type, string status, DateTime? from, DateTime? to, string? q)
        {
            var qset = _ctx.DocumentRequests.Include(r => r.AttendanceRef).AsQueryable();

            if (type.HasValue) qset = qset.Where(r => r.Type == type.Value);
            if (status == "incomplete") qset = qset.Where(r => r.CompletedAt == null);
            else if (status == "completed") qset = qset.Where(r => r.CompletedAt != null);

            if (from.HasValue) qset = qset.Where(r => r.CreatedAt >= from.Value.Date);
            if (to.HasValue)   qset = qset.Where(r => r.CreatedAt < to.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.Trim().ToLower();
                var userIds = _ctx.Users
                    .Where(u => (u.Email ?? "").ToLower().Contains(qLower) || (u.UserName ?? "").ToLower().Contains(qLower))
                    .Select(u => u.Id);

                qset = qset.Where(r =>
                    (r.UploadedFileName ?? "").ToLower().Contains(qLower) ||
                    userIds.Contains(r.UserId));
            }

            return qset.OrderByDescending(r => r.CreatedAt);
        }
    }
}