using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebUI.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;     // Servis Adı (TR etiket View'da)

        [Required, MaxLength(200)]
        public string StartPoint { get; set; } = string.Empty; // Başlangıç noktası

        [Required, MaxLength(200)]
        public string EndPoint { get; set; } = string.Empty;   // Bitiş noktası

        // Başlangıç saati (View’da "Başlangıç Saati" olarak etiketlenir)
        // SQLite’da string (HH:mm) olarak saklayacağız (converter ile)
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan? StartTime { get; set; }

        [Required, MaxLength(20)]
        public string PlateNumber { get; set; } = string.Empty; // Plaka

        [Range(1, 100)]
        public int SeatCount { get; set; } = 14;                 // Koltuk sayısı

        [MaxLength(50)]
        public string? FuelType { get; set; }                    // Yakıt tipi

        [MaxLength(80)]
        public string? Brand { get; set; }                       // Marka

        [MaxLength(80)]
        public string? Model { get; set; }                       // Model

        // Formda gösterilmeyecek, DB’de otomatik şimdi:
        [ValidateNever]              // validasyon yapma
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        [ValidateNever]              // koleksiyon/navigasyon doğrulamasını kapat
        public ICollection<ServiceAssignment> Assignments { get; set; } = new List<ServiceAssignment>();
    }
}
