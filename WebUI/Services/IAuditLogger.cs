using System.Threading.Tasks;

namespace WebUI.Services.Auditing
{
    public interface IAuditLogger
    {
        Task LogAsync(string action, string entityType, string entityId, string? data = null);
    }
}