using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebUI.Models
{
    public class Shift
    {
        public int Id { get; set; }

        [Required]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        public ShiftType ShiftType { get; set; } = ShiftType.Morning;

        [DataType(DataType.Time)]
        public TimeSpan? StartTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? EndTime { get; set; }

        // >>> View’larda “Start/End” kullanılmışsa derleme hatasını keser
        [NotMapped]
        public TimeSpan? Start { get => StartTime; set => StartTime = value; }

        [NotMapped]
        public TimeSpan? End { get => EndTime; set => EndTime = value; }

        public Meal Meal { get; set; } = Meal.None;
        public Status Status { get; set; } = Status.Active;
        public string? Notes { get; set; }
    }
}
