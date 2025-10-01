using System.ComponentModel.DataAnnotations;

namespace WebUI.Models.Auth
{
    public class AdminResetPasswordViewModel
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }

        [Display(Name = "Yeni parola")]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Display(Name = "Parolayı e-posta ile gönder" )]
        public bool SendEmail { get; set; } = true;

        public string? GeneratedPassword { get; set; }
    }
}
