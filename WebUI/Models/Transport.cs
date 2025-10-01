using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebUI.Models
{
    public class Transport
    {
        public int Id { get; set; }

        [Required]
        public string? UserId { get; set; }

        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; } = DateTime.UtcNow.Date;

        [NotMapped]
        public DateTime Date
        {
            get => TravelDate;
            set => TravelDate = value;
        }

        // >>> Yeni alanlar: Admin Transports Controller/View’ların istediği <<<
        [MaxLength(150)]
        public string? From { get; set; }

        [MaxLength(150)]
        public string? To { get; set; }

        public bool WillUse { get; set; } = false;
        public Status Status { get; set; } = Status.Active;
        public string? Notes { get; set; }
    }
}
