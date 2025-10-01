using System.ComponentModel.DataAnnotations;

namespace WebUI.Models.Auth
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Kullanıcı adı")]
        public string UserName { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;

        public string? ReturnUrl { get; set; }
    }
}
