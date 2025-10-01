using System.ComponentModel.DataAnnotations;

namespace WebUI.Models.Settings
{
    public class SmtpSettings
    {
        [Required]
        [Display(Name = "Sunucu Adı")]
        public string Host { get; set; } = string.Empty;

        [Range(1, 65535)]
        [Display(Name = "Port")]
        public int Port { get; set; } = 587;

        [Display(Name = "SSL Kullan")] 
        public bool EnableSsl { get; set; } = true;

        [Display(Name = "Kullanıcı Adı")]
        public string? User { get; set; }

        [Display(Name = "Parola")]
        public string? Password { get; set; }

        [Display(Name = "Gönderen Adresi")]
        public string? From { get; set; }

        [Display(Name = "Bilgilendirme E-postaları")]
        public string? AdminEmails { get; set; }
    }
}
