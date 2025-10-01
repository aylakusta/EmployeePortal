using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebUI.Models
{
    public class Employee
    {
        public enum EmployeeCategory
        {
            [Display(Name = "Beyaz yaka")] WhiteCollar = 1,
            [Display(Name = "Mavi yaka")]  BlueCollar  = 2
        }

        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [DataType(DataType.Date)]
        public DateTime? HireDate { get; set; }

        [Range(0, 1_000_000)]
        public decimal Salary { get; set; }

        // Departman
        [Required(ErrorMessage = "Lütfen departman seçiniz.")]
        public int? DepartmentId { get; set; }
        public Department? DepartmentRef { get; set; }

        // Unvan
        public int? JobTitleId { get; set; }
        public JobTitle? JobTitleRef { get; set; }

        // Üst yönetici
        public int? SupervisorId { get; set; }
        public Employee? SupervisorRef { get; set; }
        public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

        // Kullanıcı eşlemesi
        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(450)]
        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        // Kategori ve servis/araç kuralları
        [Range(1, 2, ErrorMessage = "Lütfen bir kategori seçiniz.")]
        public EmployeeCategory Category { get; set; }

        public bool UsesTransport { get; set; } = true;
        public bool HasCompanyCar { get; set; } = false;
        public bool IsFlexibleWhiteCollar { get; set; } = false;

        [Range(1, 3)]
        public int? DefaultShift { get; set; }

        [Phone, MaxLength(15)]
        public string? PhoneNumber { get; set; }

        [NotMapped] public string FullName => $"{FirstName} {LastName}";
        [NotMapped] public string Department => DepartmentRef?.Name ?? string.Empty;
        [NotMapped] public string Title => JobTitleRef?.Name ?? string.Empty;
    }
}
