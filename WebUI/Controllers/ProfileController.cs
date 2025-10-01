using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileController(
            ApplicationDbContext ctx,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _ctx = ctx;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Challenge();

            var user = await _userManager.FindByIdAsync(uid);
            if (user == null) return Challenge();

            // Employee'ı departmanla beraber çek
            var emp = await _ctx.Employees
                .Include(e => e.DepartmentRef)
                .FirstOrDefaultAsync(e => e.UserId == uid);

            // yoksa email ile bul ve bağ kur
            if (emp == null && !string.IsNullOrWhiteSpace(user.Email))
            {
                emp = await _ctx.Employees
                    .Include(e => e.DepartmentRef)
                    .FirstOrDefaultAsync(e => e.Email == user.Email);
                if (emp != null && string.IsNullOrEmpty(emp.UserId))
                {
                    emp.UserId = uid;
                    await _ctx.SaveChangesAsync();
                }
            }

            var vm = new ProfileVm
            {
                FirstName   = emp?.FirstName ?? "",
                LastName    = emp?.LastName ?? "",
                Department  = emp?.DepartmentRef?.Name ?? "",
                Category    = emp != null ? GetDisplayName(emp.Category) : "",
                Email       = emp?.Email ?? user.Email ?? "",
                PhoneNumber = emp?.PhoneNumber ?? user.PhoneNumber ?? ""
            };

            return View(vm);
        }

        // POST: /Profile  (Email + Telefon güncelleme)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileVm model)
        {
            if (!ModelState.IsValid) return View(model);

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Challenge();

            var user = await _userManager.FindByIdAsync(uid);
            if (user == null) return Challenge();

            // Employee'ı çek (dept gerekmediği için Include yok)
            var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.UserId == uid);
            if (emp == null && !string.IsNullOrWhiteSpace(user.Email))
            {
                emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
                if (emp != null) emp.UserId = uid; // kalıcı bağ
            }

            var previousEmail = (emp?.Email ?? user.Email ?? "").Trim();
            var newEmail      = (model.Email ?? "").Trim();

            // Ad/Soyad KESİNLİKLE GÜNCELLENMEZ (sadece görüntü)
            // Sadece email + telefon güncellenir
            if (emp != null)
            {
                emp.Email       = newEmail;
                emp.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            }

            // Identity tarafında telefon
            if (!string.Equals(user.PhoneNumber ?? "", model.PhoneNumber ?? "", StringComparison.Ordinal))
            {
                var setPhone = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber ?? "");
                if (!setPhone.Succeeded)
                {
                    ModelState.AddModelError(nameof(ProfileVm.PhoneNumber),
                        string.Join(" ", setPhone.Errors.Select(e => e.Description)));
                    return View(model);
                }
            }

            // E-posta senkron
            if (!string.Equals(previousEmail, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var (ok, error) = await SyncIdentityEmailAsync(emp, user, previousEmail, newEmail);
                if (!ok)
                {
                    ModelState.AddModelError(nameof(ProfileVm.Email), error ?? "E-posta senkronizasyonu başarısız.");
                    return View(model);
                }
            }

            await _ctx.SaveChangesAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Bilgileriniz güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/ChangePassword  (Parola değiştir)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVm model)
        {
            if (!ModelState.IsValid)
            {
                // aynı sayfaya model hatalarıyla dönmek için Index VM'ini de dolduralım
                await FillIndexTempDataAsync();
                return View("Index", await BuildIndexVmAsync());
            }

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Challenge();

            var user = await _userManager.FindByIdAsync(uid);
            if (user == null) return Challenge();

            IdentityResult result;

            // Kullanıcının zaten bir parolası var mı?
            var hasPw = await _userManager.HasPasswordAsync(user);
            if (hasPw)
            {
                result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword!);
            }
            else
            {
                // hiç parolası yoksa CurrentPassword aramayalım
                result = await _userManager.AddPasswordAsync(user, model.NewPassword!);
            }

            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                await FillIndexTempDataAsync();
                return View("Index", await BuildIndexVmAsync());
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Parolanız güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // Yardımcılar
        private async Task<ProfileVm> BuildIndexVmAsync()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(uid!);
            var emp = await _ctx.Employees.Include(e => e.DepartmentRef).FirstOrDefaultAsync(e => e.UserId == uid);
            return new ProfileVm
            {
                FirstName   = emp?.FirstName ?? "",
                LastName    = emp?.LastName ?? "",
                Department  = emp?.DepartmentRef?.Name ?? "",
                Category    = emp != null ? GetDisplayName(emp.Category) : "",
                Email       = emp?.Email ?? user?.Email ?? "",
                PhoneNumber = emp?.PhoneNumber ?? user?.PhoneNumber ?? ""
            };
        }

        private Task FillIndexTempDataAsync()
        {
            // Gerekirse ilerde bildirim/menü verileri taşımak için yer
            return Task.CompletedTask;
        }

        private async Task<(bool ok, string? error)> SyncIdentityEmailAsync(
            Employee? emp,
            ApplicationUser user,
            string? previousEmail,
            string newEmail)
        {
            if (string.IsNullOrWhiteSpace(newEmail))
                return (false, "E-posta boş olamaz.");

            var dupe = await _userManager.FindByEmailAsync(newEmail);
            if (dupe != null && dupe.Id != user.Id)
                return (false, "Bu e-posta başka bir kullanıcıya ait.");

            var r1 = await _userManager.SetEmailAsync(user, newEmail);
            if (!r1.Succeeded)
                return (false, string.Join(" ", r1.Errors.Select(e => e.Description)));

            if (!string.IsNullOrEmpty(previousEmail) &&
                string.Equals(user.UserName, previousEmail, StringComparison.OrdinalIgnoreCase))
            {
                var r2 = await _userManager.SetUserNameAsync(user, newEmail);
                if (!r2.Succeeded)
                    return (false, string.Join(" ", r2.Errors.Select(e => e.Description)));
            }

            var r3 = await _userManager.UpdateAsync(user);
            if (!r3.Succeeded)
                return (false, string.Join(" ", r3.Errors.Select(e => e.Description)));

            return (true, null);
        }

        private static string GetDisplayName(Enum value)
        {
            var type = value.GetType();
            var mem  = type.GetMember(value.ToString());
            if (mem != null && mem.Length > 0)
            {
                var attr = mem[0].GetCustomAttributes(typeof(DisplayAttribute), false)
                                .OfType<DisplayAttribute>()
                                .FirstOrDefault();
                if (attr?.GetName() is string name) return name;
            }
            return value.ToString();
        }
    }

    // ViewModels
    public class ProfileVm
    {
        // Sadece GÖRÜNTÜLEME – Required değil (disabled input post etmez)
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";

        public string? Department { get; set; }
        public string? Category   { get; set; }

        [Required, EmailAddress, Display(Name = "E-posta")]
        public string Email { get; set; } = "";

        [Phone, Display(Name = "Telefon")]
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordVm
    {
        [Display(Name = "Mevcut Parola")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }  // parolası yoksa boş bırakılacak

        [Required, DataType(DataType.Password)]
        [Display(Name = "Yeni Parola")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Parola en az 6 karakter olmalı.")]
        public string? NewPassword { get; set; }

        [Required, DataType(DataType.Password)]
        [Display(Name = "Yeni Parola (Tekrar)")]
        [Compare(nameof(NewPassword), ErrorMessage = "Parolalar eşleşmiyor.")]
        public string? ConfirmPassword { get; set; }
    }
}
