using System.Threading.Tasks;

namespace WebUI.Services.Notifications
{
    public interface INotificationService
    {
        Task NotifyAdminsAsync(string subject, string message);
        Task SendEmailAsync(string to, string subject, string message);
    }
}
