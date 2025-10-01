using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebUI.Hubs;

namespace WebUI.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<NotificationService> _log;
        private readonly IHubContext<AdminHub>? _hub;

        public NotificationService(IConfiguration cfg, ILogger<NotificationService> log, IHubContext<AdminHub>? hub = null)
        {
            _cfg = cfg;
            _log = log;
            _hub = hub;
        }

        public async Task NotifyAdminsAsync(string subject, string message)
        {
            var admins = _cfg.GetSection("Notifications:AdminEmails").Get<string[]>() ?? Array.Empty<string>();
            await SendEmailsAsync(admins, subject, message);

            try
            {
                if (_hub != null)
                {
                    await _hub.Clients.All.SendAsync("Notify", subject, message);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "NotifyAdmins SignalR block failed");
            }
        }


        public Task SendEmailAsync(string to, string subject, string message)
        {
            return SendEmailsAsync(new[] { to }, subject, message);
        }

        private async Task SendEmailsAsync(IEnumerable<string> recipients, string subject, string message)
        {
            var toList = (recipients ?? Array.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (toList.Count == 0)
                return;

            try
            {
                var smtpSection = _cfg.GetSection("Notifications:Smtp");
                var host = smtpSection["Host"];
                if (string.IsNullOrWhiteSpace(host))
                {
                    _log.LogWarning("SMTP host missing; email not sent.");
                    return;
                }

                var port = smtpSection.GetValue<int?>("Port") ?? 587;
                var enableSsl = smtpSection.GetValue<bool?>("EnableSsl") ?? true;
                var user = smtpSection["User"];
                var pass = smtpSection["Password"];
                var from = smtpSection["From"] ?? user;
                if (string.IsNullOrWhiteSpace(from))
                {
                    _log.LogWarning("SMTP from address missing; email not sent.");
                    return;
                }

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(user, pass)
                };

                foreach (var to in toList)
                {
                    try
                    {
                        using var mail = new MailMessage(from!, to)
                        {
                            Subject = subject,
                            Body = message,
                            IsBodyHtml = false
                        };
                        await client.SendMailAsync(mail);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Email send failed to {Recipient}", to);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SendEmailsAsync failed");
            }
        }

    }
}
