using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebUI.Models
{
    public class Document
    {
        public int Id { get; set; }

        [StringLength(255)]
        public string? FileName { get; set; }

        public string? UserId { get; set; }

        // Asıl kolon
        public string? Path { get; set; }

        // Asıl kolon
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // === KÖPRÜ/ALIAS PROPERTY'LER (Controller eski adları kullanıyorsa) ===

        [NotMapped]
        public string? FilePath
        {
            get => Path;
            set => Path = value;
        }

         public string FileType { get; set; } = "application/pdf";

        [NotMapped]
        public DateTime UploadDate
        {
            get => UploadedAt;
            set => UploadedAt = value;
        }
    }
}
