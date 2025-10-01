using System.ComponentModel.DataAnnotations;

namespace WebUI.Models.Auth
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni parola")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
        [Display(Name = "Parolayı doğrula")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
