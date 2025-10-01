using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;
using WebUI.Models.Auth;
using WebUI.Services.Notifications;
using WebUI.Data;

namespace WebUI.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        private readonly ApplicationDbContext _db;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            INotificationService notifications,
            ApplicationDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _notifications = notifications;
            _db = db;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            ApplicationUser? user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null && model.UserName.Contains("@"))
                user = await _userManager.FindByEmailAsync(model.UserName);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi.");
                return View(model);
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> LogoutGet()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            ApplicationUser? user = await _userManager.FindByNameAsync(model.UserIdentifier);
            if (user == null && model.UserIdentifier.Contains("@"))
                user = await _userManager.FindByEmailAsync(model.UserIdentifier);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                TempData["Success"] = "Eğer kayıtlı bir e-posta mevcutsa sıfırlama bağlantısı gönderildi.";
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebUtility.UrlEncode(token);
            var callbackUrl = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token = encoded }, Request.Scheme);

            var body = new StringBuilder();
            body.AppendLine("Merhaba,");
            body.AppendLine();
            body.AppendLine("Portal parolanızı sıfırlamak için aşağıdaki bağlantıya tıklayın:");
            body.AppendLine(callbackUrl);
            body.AppendLine();
            body.AppendLine("Bu talebi siz oluşturmadıysanız lütfen destek ekibine bildiriniz.");

            await _notifications.SendEmailAsync(user.Email!, "Parola sıfırlama", body.ToString());

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
                return BadRequest();

            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var decodedToken = WebUtility.UrlDecode(model.Token);
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
            if (result.Succeeded)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id);
            var vm = new MyAccountVm
            {
                UserId = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FullName = emp != null ? $"{emp.FirstName} {emp.LastName}" : ""
            };
            return View(vm);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MyAccount(MyAccountVm model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = model.Email;
                var upd = await _userManager.UpdateAsync(user);
                if (!upd.Succeeded)
                {
                    foreach (var e in upd.Errors) ModelState.AddModelError(string.Empty, e.Description);
                    return View(model);
                }

                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (emp != null)
                {
                    emp.Email = model.Email;
                    await _db.SaveChangesAsync();
                }
            }

            if (!string.Equals(user.PhoneNumber, model.PhoneNumber))
            {
                await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
            }

            TempData["Success"] = "Bilgiler güncellendi.";
            return RedirectToAction(nameof(MyAccount));
        }

        // Simple VM for MyAccount page
        public class MyAccountVm
        {
            public string UserId { get; set; } = "";
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? FullName { get; set; }
        }
    }
}
