using System;
using System.ComponentModel.DataAnnotations;

namespace WebUI.Models
{
    public enum DocumentRequestType
    {
        MedicalReport = 1,  // Rapor yükleme
        AnnualLeave = 2     // Yıllık izin formu
    }

    public class DocumentRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = default!;

        [Required]
        public DocumentRequestType Type { get; set; }

        // Attendance bağlantısı (hangi bildirim için)
        [Required]
        public int AttendanceId { get; set; }
        public Attendance AttendanceRef { get; set; } = default!;

        // Talep edilen tarih aralığı (izin / rapor kapsamı)
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        // Rapor dosyası depolama (opsiyonel)
        [MaxLength(260)]
        public string? UploadedFilePath { get; set; }

        [MaxLength(150)]
        public string? UploadedFileName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
