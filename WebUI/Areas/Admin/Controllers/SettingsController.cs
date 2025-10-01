using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebUI.Models.Settings;
using WebUI.Services.Notifications;
using WebUI.Services.Settings;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly INotificationService _notifications;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ISettingsService settingsService, INotificationService notifications, ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _notifications = notifications;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _settingsService.GetSmtpSettingsAsync();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SmtpSettings model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await _settingsService.UpdateSmtpSettingsAsync(model);
                TempData["Success"] = "SMTP ayarları güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP ayarları güncellenirken hata oluştu.");
                TempData["Error"] = "SMTP ayarları güncellenemedi.";
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestMail(string testEmail)
        {
            if (string.IsNullOrWhiteSpace(testEmail))
            {
                TempData["Error"] = "Lütfen test e-postası için geçerli bir adres giriniz.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _notifications.SendEmailAsync(testEmail, "SMTP Testi", "Bu e-posta sistem ayarları testidir.");
                TempData["Success"] = $"Test e-postası {testEmail} adresine gönderildi.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test e-postası gönderilemedi.");
                TempData["Error"] = "Test e-postası gönderilemedi. Ayrıntılar loglarda bulunmaktadır.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
