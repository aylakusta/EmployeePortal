using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebUI.Services.Auditing
{
    public class FileAuditLogger : IAuditLogger
    {
        private readonly ILogger<FileAuditLogger> _log;
        private readonly string _dir;

        public FileAuditLogger(ILogger<FileAuditLogger> log)
        {
            _log = log;
            _dir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "logs");
            Directory.CreateDirectory(_dir);
        }

        public async Task LogAsync(string action, string entityType, string entityId, string? data = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                var file = Path.Combine(_dir, $"audit-{now:yyyyMMdd}.log");
                var obj = new
                {
                    ts = now,
                    action,
                    entityType,
                    entityId,
                    data
                };
                var line = JsonSerializer.Serialize(obj);
                await File.AppendAllTextAsync(file, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log write failed");
            }
        }
    }
}