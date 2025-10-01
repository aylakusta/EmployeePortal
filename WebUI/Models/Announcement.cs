using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebUI.Models
{
    public class Announcement
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string? Title { get; set; }

        // Eski ad (Body) kalsın ama View'lar için Content alias'ı verelim
        public string? Body { get; set; }

        [NotMapped]
        public string? Content
        {
            get => Body;
            set => Body = value;
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // View'ların beklediği Date alias'ı
        [NotMapped]
        public DateTime Date
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }

        public Status Status { get; set; } = Status.Active;
    }
}
