using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Infrastructure;
using WebUI.Models;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ImportsController : Controller
    {
        private readonly ApplicationDbContext _ctx;

        public ImportsController(ApplicationDbContext ctx)
        {
            _ctx = ctx;
        }

        [HttpGet]
        public IActionResult EmployeesCsv()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmployeesCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Dosya seçilmedi.";
                return View();
            }

            string csv;
            try
            {
                csv = await EncodingHelpers.ReadAllTextSmartAsync(file);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Dosya okunamadı: " + ex.Message;
                return View();
            }

            // Satırlara ayır
            var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                TempData["Error"] = "Veri satırı bulunamadı.";
                return View();
            }

            // Başlık sütunları
            var headers = lines[0].Split(new[] { ';', ',' }, StringSplitOptions.None);
            int ixFirst = Array.FindIndex(headers, h => h.Equals("FirstName", StringComparison.OrdinalIgnoreCase) || h.Equals("Ad", StringComparison.OrdinalIgnoreCase));
            int ixLast = Array.FindIndex(headers, h => h.Equals("LastName", StringComparison.OrdinalIgnoreCase) || h.Equals("Soyad", StringComparison.OrdinalIgnoreCase));
            int ixMail = Array.FindIndex(headers, h => h.Equals("Email", StringComparison.OrdinalIgnoreCase) || h.Equals("Eposta", StringComparison.OrdinalIgnoreCase) || h.Equals("E-Posta", StringComparison.OrdinalIgnoreCase));
            int ixDept = Array.FindIndex(headers, h => h.Equals("Department", StringComparison.OrdinalIgnoreCase) || h.Equals("Departman", StringComparison.OrdinalIgnoreCase));

            if (ixFirst < 0 || ixLast < 0 || ixMail < 0 || ixDept < 0)
            {
                TempData["Error"] = "Başlık satırı için gerekli alanlar bulunamadı. Gerekli sütunlar: FirstName, LastName, Email, Department";
                return View();
            }

            // Departman isimlerini tek seferde cache’leyelim (büyük importlarda hızlı olur)
            var depDict = await _ctx.Departments
                .AsNoTracking()
                .ToDictionaryAsync(d => d.Name.Trim().ToUpperInvariant(), d => d.Id);

            int imported = 0;
            int createdDepartments = 0;
            var errors = new List<string>();

            // Transaksiyon (hepsi ya da hiçbiri)
            using var tx = await _ctx.Database.BeginTransactionAsync();

            try
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var row = lines[i].Split(new[] { ';', ',' }, StringSplitOptions.None);
                    if (row.All(string.IsNullOrWhiteSpace)) continue;

                    try
                    {
                        var fn = row.Length > ixFirst ? row[ixFirst] : null;
                        var ln = row.Length > ixLast ? row[ixLast] : null;
                        var mailRaw = row.Length > ixMail ? row[ixMail] : null;
                        var deptRaw = row.Length > ixDept ? row[ixDept] : null;

                        if (string.IsNullOrWhiteSpace(fn) || string.IsNullOrWhiteSpace(ln) || string.IsNullOrWhiteSpace(mailRaw))
                        {
                            errors.Add($"Satır {i + 1}: Zorunlu alanlar boş (FirstName/LastName/Email).");
                            continue;
                        }

                        // Departman upsert
                        var deptName = (deptRaw ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(deptName))
                        {
                            errors.Add($"Satır {i + 1}: Departman boş olamaz.");
                            continue;
                        }

                        var key = deptName.ToUpperInvariant();
                        int deptId;
                        if (!depDict.TryGetValue(key, out deptId))
                        {
                            var newDep = new Department { Name = deptName };
                            _ctx.Departments.Add(newDep);
                            await _ctx.SaveChangesAsync();   // Id almak için
                            deptId = newDep.Id;
                            depDict[key] = deptId;
                            createdDepartments++;
                        }

                        // Aynı email varsa güncellemek istiyorsan burada kontrolü yapabilirsin
                        // var existing = await _ctx.Employees.FirstOrDefaultAsync(e => e.Email == mailRaw.Trim());
                        // if (existing != null) { existing.FirstName=..; existing.LastName=..; existing.DepartmentId=deptId; continue; }

                        var emp = new Employee
                        {
                            FirstName = TurkishText.KeepTurkish(fn)?.Trim(),
                            LastName = TurkishText.KeepTurkish(ln)?.Trim(),
                            Email = mailRaw?.Trim(),
                            DepartmentId = deptId
                        };

                        _ctx.Employees.Add(emp);
                        imported++;
                    }
                    catch (Exception exRow)
                    {
                        errors.Add($"Satır {i + 1}: {exRow.Message}");
                    }
                }

                await _ctx.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception dbEx)
            {
                await tx.RollbackAsync();
                errors.Add("Veritabanı hatası: " + dbEx.Message);
            }

            TempData["Ok"] = $"{imported} personel eklendi. {createdDepartments} yeni departman oluşturuldu.";
            if (errors.Any())
                TempData["Warn"] = string.Join("<br>", errors);

            return View();
        }
    }
}
