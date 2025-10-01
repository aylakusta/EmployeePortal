using System.Threading.Tasks;
using WebUI.Models.Settings;

namespace WebUI.Services.Settings
{
    public interface ISettingsService
    {
        Task<SmtpSettings> GetSmtpSettingsAsync();
        Task UpdateSmtpSettingsAsync(SmtpSettings settings);
    }
}
