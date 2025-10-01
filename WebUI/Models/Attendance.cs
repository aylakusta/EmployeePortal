using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebUI.Models
{
    public enum AttendanceReason
    {
        AnnualLeave = 1,    // Yıllık izin
        Report = 2,        // Rapor
        Bereavement = 3,   // Yakın Vefatı
        Birth = 4,         // Doğum
        Excuse = 5         // Mazeret
    }

    public class Attendance
    {
        public int Id { get; set; }

        [ValidateNever]  // Microsoft.AspNetCore.Mvc.ModelBinding.Validation
        public string UserId { get; set; } = default!; // Identity kullanıcı Id

        [Required]
        public AttendanceReason Reason { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Talep ilişkisi (Documents flow)
        public int? DocumentRequestId { get; set; }
        public DocumentRequest? DocumentRequestRef { get; set; }

        // Süreç tamamlandı mı?
        public bool IsResolved { get; set; } = false;


    }
}
