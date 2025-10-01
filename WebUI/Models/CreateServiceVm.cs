using System.ComponentModel.DataAnnotations;

namespace WebUI.Models
{
    // Create form ViewModel (StartTime as HH:mm string from <input type="time">)
    public class CreateServiceVm
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string StartPoint { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string EndPoint { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PlateNumber { get; set; } = string.Empty;

        // Accepts "08:30" etc.
        [Display(Name = "Start Time")]
        public string? StartTime { get; set; }

        [Range(1, 100)]
        public int SeatCount { get; set; } = 20;

        [MaxLength(50)]
        public string? FuelType { get; set; }

        [MaxLength(80)]
        public string? Brand { get; set; }

        [MaxLength(80)]
        public string? Model { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
