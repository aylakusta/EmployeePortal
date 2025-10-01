using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebUI.Data;
using WebUI.Models;
using WebUI.Services.Notifications;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(
            ApplicationDbContext ctx,
            UserManager<ApplicationUser> userManager,
            INotificationService notifications,
            ILogger<EmployeesController> logger)
        {
            _ctx = ctx;
            _userManager = userManager;
            _notifications = notifications;
            _logger = logger;
        }

        // ========== LIST ==========
        public async Task<IActionResult> Index()
        {
            var list = await _ctx.Employees
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .ToListAsync();

            return View(list);
        }

        // ========== CREATE ==========
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLookupsAsync();
            return View(new Employee());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee model)
        {
            // Zorunlu seçimler
            if (model.DepartmentId is null or 0)
                ModelState.AddModelError(nameof(model.DepartmentId), "Departman seçiniz.");
            if (model.JobTitleId is null or 0)
                ModelState.AddModelError(nameof(model.JobTitleId), "Unvan seçiniz.");

            // E-posta doğrulama
            if (string.IsNullOrWhiteSpace(model.Email) || !new EmailAddressAttribute().IsValid(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Geçerli bir e-posta giriniz.");

            // Mükerrer Employee kontrolü
            if (ModelState.IsValid)
            {
                var emailLower = model.Email!.Trim().ToLower();
                var existsEmp = await _ctx.Employees.AsNoTracking()
                    .AnyAsync(e => e.Email.ToLower() == emailLower);
                if (existsEmp)
                    ModelState.AddModelError(nameof(model.Email), "Bu e-posta ile kayıtlı bir personel zaten var.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLookupsAsync(model);
                return View(model);
            }

            // Identity kullanıcı oluştur/bağla
            var email = model.Email!.Trim();
            var existing = await _userManager.FindByEmailAsync(email);
            string? tempPassword = null;
            ApplicationUser user;

            if (existing == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = false
                };

                tempPassword = "P@" + Guid.NewGuid().ToString("N").Substring(0, 8) + "1a!";
                var createRes = await _userManager.CreateAsync(user, tempPassword);
                if (!createRes.Succeeded)
                {
                    foreach (var e in createRes.Errors)
                        ModelState.AddModelError("", $"{e.Code}: {e.Description}");
                    await LoadLookupsAsync(model);
                    return View(model);
                }
                try { await _userManager.AddToRoleAsync(user, "User"); } catch { }
            }
            else
            {
                user = existing;
            }

            // Employee kaydı
            model.UserId = user.Id;
            model.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            _ctx.Employees.Add(model);
            await _ctx.SaveChangesAsync();

            // Mail (bulk ile aynı şablon + portal adresi)
            try
            {
                var portalUrl = $"{Request.Scheme}://{Request.Host.Value}";
                var subject = "Portal Hesabınız Oluşturuldu";

                var sb = new StringBuilder();
                sb.AppendLine($"Merhaba {model.FirstName} {model.LastName},");
                sb.AppendLine();
                sb.AppendLine("Personel portal hesabınız oluşturuldu.");
                sb.AppendLine();
                sb.AppendLine($"Portal Adresi: {portalUrl}");
                sb.AppendLine($"E-posta: {email}");
                if (!string.IsNullOrWhiteSpace(tempPassword))
                    sb.AppendLine($"Geçici Parola: {tempPassword}");
                sb.AppendLine();
                sb.AppendLine("Lütfen ilk girişte şifrenizi değiştiriniz.");

                await _notifications.SendEmailAsync(email, subject, sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "E-posta gönderimi başarısız: {Email}", model.Email);
            }

            TempData["Success"] = "Personel eklendi ve portal hesabı tanımlandı.";
            return RedirectToAction(nameof(Index));
        }

        // ========== EDIT ==========
        // GET: /Admin/Employees/Edit/5
        [HttpGet]
        [Route("/Admin/Employees/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null)
            {
                TempData["Warn"] = "Personel bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Görünümden önce mevcut e-posta ile portal kullanıcısını otomatik ilişkilendir (varsa)
            await TryBacklinkUserAsync(emp);
            await _ctx.SaveChangesAsync(); // sadece emp.UserId set edildiyse yazacaktır

            await LoadLookupsAsync(emp);
            return View(emp); // View: Areas/Admin/Views/Employees/Edit.cshtml
        }

        // POST: /Admin/Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Admin/Employees/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, Employee input)
        {
            if (id != input.Id)
            {
                TempData["Error"] = "Geçersiz istek.";
                return RedirectToAction(nameof(Index));
            }

            var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null)
            {
                TempData["Warn"] = "Personel bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Önce mevcut e-posta ile portal kullanıcısını geri bağlamayı dene (UserId boşsa)
            var currentUser = await TryBacklinkUserAsync(emp);

            // Doğrulamalar
            if (input.DepartmentId is null or 0)
                ModelState.AddModelError(nameof(input.DepartmentId), "Departman seçiniz.");
            if (input.JobTitleId is null or 0)
                ModelState.AddModelError(nameof(input.JobTitleId), "Unvan seçiniz.");
            if (string.IsNullOrWhiteSpace(input.Email) || !new EmailAddressAttribute().IsValid(input.Email))
                ModelState.AddModelError(nameof(input.Email), "Geçerli bir e-posta giriniz.");

            if (ModelState.IsValid)
            {
                var emailLower = input.Email!.Trim().ToLower();
                var existsEmp = await _ctx.Employees.AsNoTracking()
                    .AnyAsync(e => e.Id != id && e.Email.ToLower() == emailLower);
                if (existsEmp)
                    ModelState.AddModelError(nameof(input.Email), "Bu e-posta başka bir personelde kayıtlı.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLookupsAsync(input);
                return View(input);
            }

            // Identity senkronu (e-posta değiştiyse)
            var newEmail = input.Email!.Trim();
            var emailChanged = !string.Equals(emp.Email?.Trim(), newEmail, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                var otherUserWithNewEmail = await _userManager.FindByEmailAsync(newEmail);

                if (otherUserWithNewEmail != null && (currentUser == null || otherUserWithNewEmail.Id != currentUser.Id))
                {
                    ModelState.AddModelError(nameof(input.Email), "Bu e-posta mevcut bir kullanıcı tarafından kullanılıyor.");
                    await LoadLookupsAsync(input);
                    return View(input);
                }

                if (currentUser != null)
                {
                    currentUser.Email = newEmail;
                    currentUser.UserName = newEmail;
                    var res = await _userManager.UpdateAsync(currentUser);
                    if (!res.Succeeded)
                    {
                        foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                        await LoadLookupsAsync(input);
                        return View(input);
                    }
                }
                else if (otherUserWithNewEmail != null)
                {
                    // Employee’nin UserId’si yoktu; yeni e-postaya sahip kullanıcı bulunursa ilişkilendir
                    emp.UserId = otherUserWithNewEmail.Id;
                }
            }

            // Alanları güncelle
            emp.FirstName = input.FirstName?.Trim();
            emp.LastName  = input.LastName?.Trim();
            emp.Email     = newEmail;
            emp.DepartmentId = input.DepartmentId;
            emp.JobTitleId   = input.JobTitleId;
            emp.SupervisorId = input.SupervisorId;
            emp.Category     = input.Category;
            emp.UsesTransport   = input.UsesTransport;
            emp.HasCompanyCar   = input.HasCompanyCar;
            emp.DefaultShift    = input.DefaultShift;
            emp.PhoneNumber     = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            emp.HireDate        = input.HireDate;
            emp.IsFlexibleWhiteCollar = input.IsFlexibleWhiteCollar;

            await _ctx.SaveChangesAsync();
            TempData["Success"] = "Personel bilgileri güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // ========== DELETE (Single) ==========
        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null)
            {
                TempData["Warn"] = "Personel bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 1) Employee’ye bağlı TÜM koleksiyonları temizle (servis/mesai/doküman vb.)
                await RemoveEmployeeDependentsAsync(emp);

                // 2) Employee sil
                _ctx.Employees.Remove(emp);
                await _ctx.SaveChangesAsync();

                // 3) Portal (Identity) kullanıcısını da sil
                if (!string.IsNullOrWhiteSpace(emp.UserId))
                {
                    var user = await _userManager.FindByIdAsync(emp.UserId);
                    if (user != null)
                    {
                        var res = await _userManager.DeleteAsync(user);
                        if (!res.Succeeded)
                        {
                            _logger.LogWarning("User silinemedi: {UserId} -> {Err}",
                                emp.UserId, string.Join("; ", res.Errors.Select(x => x.Description)));
                        }
                    }
                }

                TempData["Success"] = $"\"{emp.FirstName} {emp.LastName}\" ve ilişkili kayıtları silindi.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Silme hatası (EmployeeId: {Id})", id);
                TempData["Error"] = "Personel silinemedi. İlişkili kayıtlar nedeniyle engellenmiş olabilir.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== DELETE (Bulk) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Warn"] = "Silmek için en az bir personel seçiniz.";
                return RedirectToAction(nameof(Index));
            }

            int success = 0, fail = 0;

            foreach (var id in ids.Distinct())
            {
                var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == id);
                if (emp == null) { fail++; continue; }

                try
                {
                    // 1) Bağımlı koleksiyonları temizle
                    await RemoveEmployeeDependentsAsync(emp);

                    // 2) Employee sil
                    _ctx.Employees.Remove(emp);
                    await _ctx.SaveChangesAsync();

                    // 3) Identity kullanıcıyı sil
                    if (!string.IsNullOrWhiteSpace(emp.UserId))
                    {
                        var user = await _userManager.FindByIdAsync(emp.UserId);
                        if (user != null)
                        {
                            var res = await _userManager.DeleteAsync(user);
                            if (!res.Succeeded)
                                _logger.LogWarning("User silinemedi (Bulk): {UserId}", emp.UserId);
                        }
                    }

                    success++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Bulk silme hatası (EmployeeId: {Id})", id);
                    fail++;
                    _ctx.ChangeTracker.Clear();
                }
            }

            if (success > 0 && fail == 0)
                TempData["Success"] = $"{success} personel ve ilişkili kayıtları silindi.";
            else if (success > 0 && fail > 0)
                TempData["Warn"] = $"{success} personel silindi, {fail} personel silinemedi.";
            else
                TempData["Error"] = "Seçili personeller silinemedi.";

            return RedirectToAction(nameof(Index));
        }

        // ========== HELPERS ==========
        /// <summary>
        /// Employee'nin TÜM koleksiyon navigasyonlarını dinamik olarak yükler ve RemoveRange ile siler.
        /// (Attendance, Document, Service/Transport/Shift atamaları vs. fark etmez.)
        /// </summary>
        private async Task RemoveEmployeeDependentsAsync(Employee emp)
        {
            var entry = _ctx.Entry(emp);

            foreach (var col in entry.Collections)
            {
                await col.LoadAsync();

                if (col.CurrentValue is IEnumerable enumerable)
                {
                    var toRemove = new List<object>();
                    foreach (var item in enumerable)
                        if (item != null) toRemove.Add(item);

                    if (toRemove.Count > 0)
                    {
                        _ctx.RemoveRange(toRemove);
                    }
                }
            }
            // Eğer bire-bir navigation’lar varsa ve Restrict davranışı engelliyorsa,
            // burada ayrıca null’a çekme/silme mantığı eklenebilir.
        }

        private async Task LoadLookupsAsync(Employee? m = null)
        {
            var deptList = await _ctx.Departments
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync();

            var titleList = await _ctx.JobTitles
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            var supList = await _ctx.Employees
                .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
                .Select(x => new { x.Id, FullName = x.FirstName + " " + x.LastName })
                .ToListAsync();

            ViewBag.DepartmentId = new SelectList(deptList, "Id", "Name", m?.DepartmentId);
            ViewBag.JobTitleId = new SelectList(titleList, "Id", "Name", m?.JobTitleId);
            ViewBag.SupervisorId = new SelectList(supList, "Id", "FullName", m?.SupervisorId);

            // Geriye dönük anahtarlar
            ViewBag.Departments = ViewBag.DepartmentId;
            ViewBag.JobTitles = ViewBag.JobTitleId;
            ViewBag.Supervisors = ViewBag.SupervisorId;
        }

        /// <summary>
        /// Çalışanın UserId’si boşsa, e-postasıyla eşleşen mevcut Identity kullanıcısını bulup ilişkilendirir.
        /// Bir şey silmez/oluşturmaz; sadece yumuşak bağlama yapar.
        /// </summary>
        private async Task<ApplicationUser?> TryBacklinkUserAsync(Employee emp)
        {
            // Zaten bağlıysa ID’den oku
            if (!string.IsNullOrWhiteSpace(emp.UserId))
                return await _userManager.FindByIdAsync(emp.UserId);

            // UserId yoksa, e-posta üzerinden bul ve ilişkilendir
            var mail = emp.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(mail))
            {
                var byEmail = await _userManager.FindByEmailAsync(mail);
                if (byEmail != null)
                {
                    emp.UserId = byEmail.Id;   // sadece ilişkilendir; SaveChanges dışarıda çağrılır
                    return byEmail;
                }
            }
            return null;
        }
    }
}
