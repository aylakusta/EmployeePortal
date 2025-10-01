using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebUI.Models.Settings;

namespace WebUI.Services.Settings
{
    public class SettingsService : ISettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsService> _logger;
        private readonly string _configPath;
        private readonly IConfigurationRoot? _configurationRoot;

        public SettingsService(IConfiguration configuration, IWebHostEnvironment env, ILogger<SettingsService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
            _configurationRoot = configuration as IConfigurationRoot;
        }

        public Task<SmtpSettings> GetSmtpSettingsAsync()
        {
            var section = _configuration.GetSection("Notifications:Smtp");
            var admins = _configuration.GetSection("Notifications:AdminEmails").Get<string[]>() ?? Array.Empty<string>();

            var dto = new SmtpSettings
            {
                Host = section["Host"] ?? string.Empty,
                Port = section.GetValue<int?>("Port") ?? 587,
                EnableSsl = section.GetValue<bool?>("EnableSsl") ?? true,
                User = section["User"],
                From = section["From"],
                AdminEmails = string.Join(Environment.NewLine, admins)
            };

            return Task.FromResult(dto);
        }

        public async Task UpdateSmtpSettingsAsync(SmtpSettings settings)
        {
            if (!File.Exists(_configPath))
                throw new FileNotFoundException($"appsettings.json bulunamadı: {_configPath}");

            JsonNode root;
            using (var stream = File.OpenRead(_configPath))
            {
                root = JsonNode.Parse(stream) ?? new JsonObject();
            }

            var notifications = root["Notifications"] as JsonObject ?? new JsonObject();
            var smtp = notifications["Smtp"] as JsonObject ?? new JsonObject();

            smtp["Host"] = settings.Host ?? string.Empty;
            smtp["Port"] = settings.Port;
            smtp["EnableSsl"] = settings.EnableSsl;
            smtp["User"] = settings.User ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(settings.Password))
            {
                smtp["Password"] = settings.Password;
            }
            smtp["From"] = settings.From ?? string.Empty;

            notifications["Smtp"] = smtp;

            var adminsArray = new JsonArray();
            var admins = (settings.AdminEmails ?? string.Empty)
                .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var admin in admins)
            {
                adminsArray.Add(admin);
            }
            notifications["AdminEmails"] = adminsArray;

            root["Notifications"] = notifications;

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);

            try
            {
                _configurationRoot?.Reload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Konfigürasyon yeniden yüklenemedi.");
            }
        }
    }
}

