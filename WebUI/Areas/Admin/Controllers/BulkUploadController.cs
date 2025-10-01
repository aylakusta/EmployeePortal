using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDataReader;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebUI.Data;
using WebUI.Models;
using WebUI.Areas.Admin.ViewModels;
using static WebUI.Models.Employee;
using WebUI.Services.Notifications;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,HR")]
    public class BulkUploadController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<BulkUploadController> _logger;

        public BulkUploadController(
            INotificationService notifications,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<BulkUploadController> logger
        )
        {
            _notifications = notifications;
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index() => View(new BulkUploadVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(BulkUploadVm vm)
        {
            // 0) Dosya kontrolü
            if (vm.CsvFile == null || vm.CsvFile.Length == 0)
            {
                TempData["Error"] = "Lütfen CSV veya XLSX dosyası seçiniz.";
                return View(vm);
            }

            var ext = Path.GetExtension(vm.CsvFile.FileName).ToLowerInvariant();
            if (ext != ".csv" && ext != ".xlsx" && ext != ".xls")
            {
                TempData["Error"] = "Yalnızca .csv / .xlsx / .xls dosyaları desteklenir.";
                return View(vm);
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var warnings = new List<string>();
            var createdEmployees = 0;
            var skippedEmployees = 0;

            try
            {
                // 1) Dosyayı oku
                List<Dictionary<string, string>> rows;
                using (var stream = vm.CsvFile.OpenReadStream())
                {
                    rows = ReadSheet(stream, ext);
                }

                if (rows.Count == 0)
                {
                    TempData["Warn"] = "Dosyada veri bulunamadı.";
                    return View(new BulkUploadVm());
                }

                // 2) Departman/Unvan isimlerini topla
                var deptNames = rows
                    .Select(r => Get(r, "Department"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                var titleNames = rows
                    .Select(r => Get(r, "JobTitle"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                // 3) Var olanları çek
                var existingDepts = await _db.Departments.AsNoTracking().ToListAsync();
                var existingTitles = await _db.JobTitles.AsNoTracking().ToListAsync();

                // 4) Eksikleri önce oluştur (tek SaveChanges)
                bool anyInserted = false;

                if (vm.AutoCreateDepartments && deptNames.Count > 0)
                {
                    var toCreate = deptNames
                        .Where(n => existingDepts.All(d => !StrEq(d.Name, n)))
                        .Select(n => new Department { Name = n })
                        .ToList();

                    if (toCreate.Count > 0)
                    {
                        _db.Departments.AddRange(toCreate);
                        anyInserted = true;
                    }
                }

                if (vm.AutoCreateJobTitles && titleNames.Count > 0)
                {
                    var toCreate = titleNames
                        .Where(n => existingTitles.All(t => !StrEq(t.Name, n)))
                        .Select(n => new JobTitle { Name = n })
                        .ToList();

                    if (toCreate.Count > 0)
                    {
                        _db.JobTitles.AddRange(toCreate);
                        anyInserted = true;
                    }
                }

                if (anyInserted)
                {
                    await _db.SaveChangesAsync(); // tek sefer
                    // güncel listeleri al
                    existingDepts = await _db.Departments.AsNoTracking().ToListAsync();
                    existingTitles = await _db.JobTitles.AsNoTracking().ToListAsync();
                }

                // 5) Hızlı lookup haritaları
                var deptMap = existingDepts
                    .GroupBy(d => d.Name.Trim(), StringComparer.InvariantCultureIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.InvariantCultureIgnoreCase);

                var titleMap = existingTitles
                    .GroupBy(t => t.Name.Trim(), StringComparer.InvariantCultureIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.InvariantCultureIgnoreCase);

                // 6) User rolü yoksa oluştur
                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));

                var creatorId = _userManager.GetUserId(User);

                // 7) Toplu ekleme
                var employeesToAdd = new List<Employee>();
                var emailNotified = new List<(string Email, string First, string Last, string Password)>();

                foreach (var r in rows)
                {
                    var first = Get(r, "FirstName");
                    var last  = Get(r, "LastName");
                    var email = Get(r, "Email");

                    if (string.IsNullOrWhiteSpace(first) ||
                        string.IsNullOrWhiteSpace(last)  ||
                        string.IsNullOrWhiteSpace(email))
                    {
                        warnings.Add("Zorunlu alan(lar) eksik: FirstName/LastName/Email");
                        skippedEmployees++;
                        continue;
                    }

                    email = email!.Trim();

                    // Var olan kayıt kontrolü
                    var empExists  = await _db.Employees.AsNoTracking().AnyAsync(e => e.Email.ToLower() == email.ToLower());
                    var userExists = await _userManager.FindByEmailAsync(email);

                    if ((empExists || userExists != null) && vm.SkipIfEmailExists)
                    {
                        warnings.Add($"E-posta var olduğu için atlandı: {email}");
                        skippedEmployees++;
                        continue;
                    }

                    // Departman/Unvan eşle
                    int? deptId = null;
                    var deptName = Get(r, "Department");
                    if (!string.IsNullOrWhiteSpace(deptName) && deptMap.TryGetValue(deptName!.Trim(), out var dId))
                        deptId = dId;
                    else if (!string.IsNullOrWhiteSpace(deptName))
                        warnings.Add($"Departman bulunamadı (boş bırakıldı): {deptName}");

                    int? titleId = null;
                    var titleName = Get(r, "JobTitle");
                    if (!string.IsNullOrWhiteSpace(titleName) && titleMap.TryGetValue(titleName!.Trim(), out var tId))
                        titleId = tId;
                    else if (!string.IsNullOrWhiteSpace(titleName))
                        warnings.Add($"Unvan bulunamadı (boş bırakıldı): {titleName}");

                    // Kategori
                    var cat = MapCategory(Get(r, "Category"));

                    // Bool alanlar
                    var usesTransport = ParseBool(Get(r, "UsesTransport"));
                    var hasCompanyCar = ParseBool(Get(r, "HasCompanyCar"));

                    // DefaultShift
                    int? defaultShift = ParseInt(Get(r, "DefaultShift"));

                    // Opsiyoneller
                    var phone = Get(r, "PhoneNumber");
                    var salary = ParseDecimal(Get(r, "Salary"));
                    var hireDate = ParseDate(Get(r, "HireDate"));
                    var isFlexWhite = ParseBool(Get(r, "IsFlexibleWhiteCollar"));

                    // Identity User oluştur/varsa kullan
                    ApplicationUser? user = userExists;
                    string? plainPassword = null;
                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            EmailConfirmed = false
                        };

                        plainPassword = GeneratePassword();
                        var cr = await _userManager.CreateAsync(user, plainPassword);
                        if (!cr.Succeeded)
                        {
                            warnings.Add($"Kullanıcı oluşturulamadı ({email}): {string.Join("; ", cr.Errors.Select(e => e.Description))}");
                            skippedEmployees++;
                            continue;
                        }

                        await _userManager.AddToRoleAsync(user, "User");
                    }

                    var employee = new Employee
                    {
                        FirstName = first!.Trim(),
                        LastName  = last!.Trim(),
                        Email     = email,
                        DepartmentId = deptId,
                        JobTitleId   = titleId,
                        Category = cat,
                        UsesTransport = usesTransport,
                        HasCompanyCar = hasCompanyCar,
                        DefaultShift  = defaultShift,
                        PhoneNumber   = string.IsNullOrWhiteSpace(phone) ? null : phone!.Trim(),
                        HireDate = hireDate ?? DateTime.UtcNow,
                        IsFlexibleWhiteCollar = isFlexWhite,
                        UserId = user.Id,
                        CreatedByUserId = creatorId
                    };

                    employeesToAdd.Add(employee);

                    if (!string.IsNullOrWhiteSpace(plainPassword))
                        emailNotified.Add((email, employee.FirstName, employee.LastName, plainPassword));
                }

                if (employeesToAdd.Count > 0)
                {
                    _db.Employees.AddRange(employeesToAdd);
                    await _db.SaveChangesAsync(); // tek sefer yaz
                    createdEmployees = employeesToAdd.Count;
                }

                // 8) Bilgi e-postaları
                foreach (var item in emailNotified)
                {
                    try
                    {
                        var subject = "Portal Hesabınız Oluşturuldu";
                        var body = $@"
Merhaba {item.First} {item.Last},

Personel portal hesabınız oluşturuldu.

E-posta: {item.Email}
Geçici Parola: {item.Password}

Lütfen ilk girişte şifrenizi değiştiriniz.";

                        await _notifications.SendEmailAsync(item.Email, subject, body);
                    }
                    catch (Exception mailEx)
                    {
                        _logger.LogWarning(mailEx, "E-posta gönderimi başarısız: {Email}", item.Email);
                        warnings.Add($"E-posta gönderilemedi: {item.Email}");
                    }
                }

                var msg = $"{createdEmployees} personel eklendi. {skippedEmployees} satır atlandı.";
                if (warnings.Count > 0)
                    msg += $" <ul class='mb-0'>{string.Join("", warnings.Select(w => $"<li>{w}</li>"))}</ul>";

                TempData["Success"] = msg;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu ekleme hatası");
                TempData["Error"] = "Toplu ekleme sırasında beklenmeyen bir hata oluştu.";
                return View(vm);
            }
        }

        [HttpGet]
        public IActionResult TemplateXlsx()
        {
            var csv =
@"FirstName,LastName,Email,Department,JobTitle,Category,UsesTransport,HasCompanyCar,DefaultShift
Ayşe,Yılmaz,ayse@example.com,Satış,Uzman,Beyaz,1,0,
Mehmet,Demir,mehmet@example.com,Üretim,Operatör,Mavi,1,0,2
";
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "BulkTemplate.csv");
        }

        // -------- Helpers --------

        private static List<Dictionary<string, string>> ReadSheet(Stream stream, string ext)
        {
            var rows = new List<Dictionary<string, string>>();
            IExcelDataReader reader =
                ext == ".csv"
                    ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.UTF8 })
                    : ExcelReaderFactory.CreateReader(stream);

            using (reader)
            {
                var header = new List<string>();
                var headerDetected = false;

                while (reader.Read())
                {
                    var values = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var v = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                        values.Add(v ?? "");
                    }

                    if (!headerDetected)
                    {
                        if (values.Any() && values[0].Trim().Equals("FirstName", StringComparison.InvariantCultureIgnoreCase))
                        {
                            header = values.Select(v => v?.Trim() ?? "").ToList();
                            headerDetected = true;
                            continue;
                        }
                        else
                        {
                            header = new()
                            {
                                "FirstName","LastName","Email","Department","JobTitle",
                                "Category","UsesTransport","HasCompanyCar","DefaultShift",
                                "PhoneNumber","Salary","HireDate","IsFlexibleWhiteCollar"
                            };
                            headerDetected = true;
                        }
                    }

                    var row = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    for (int i = 0; i < Math.Min(header.Count, values.Count); i++)
                        row[header[i]] = values[i];

                    if (row.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                        rows.Add(row);
                }
            }
            return rows;
        }

        private static string? Get(Dictionary<string, string> row, string key)
            => row.TryGetValue(key, out var v) ? v : null;

        private static bool StrEq(string a, string b)
            => a.Trim().Equals(b.Trim(), StringComparison.InvariantCultureIgnoreCase);

        private static bool ParseBool(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToLowerInvariant();
            return s is "1" or "true" or "evet" or "yes";
        }

        private static int? ParseInt(string? s)
            => int.TryParse((s ?? "").Trim(), out var i) ? i : null;

        private static decimal? ParseDecimal(string? s)
            => decimal.TryParse((s ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

        private static DateTime? ParseDate(string? s)
            => DateTime.TryParse((s ?? "").Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d : null;

        private static EmployeeCategory MapCategory(string? s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s switch
            {
                "beyaz" or "white" => EmployeeCategory.WhiteCollar,
                "mavi" or "blue"  => EmployeeCategory.BlueCollar,
                _ => EmployeeCategory.WhiteCollar
            };
        }

        private static string GeneratePassword()
            => "P@" + Guid.NewGuid().ToString("N").Substring(0, 8) + "1a!";
    }
}
