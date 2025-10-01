using System.ComponentModel.DataAnnotations;

namespace WebUI.Models.Auth
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [Display(Name = "Kullanıcı adı veya e-posta")]
        public string UserIdentifier { get; set; } = string.Empty;
    }
}
