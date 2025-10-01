using System;
using System.ComponentModel.DataAnnotations;

namespace WebUI.Models
{
    public class ServiceAssignment
    {
        public int Id { get; set; }

        [Required]
        public int ServiceId { get; set; }
        public Service? Service { get; set; }

        [Required]
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true; // Unassign için false yapılır
    }
}
