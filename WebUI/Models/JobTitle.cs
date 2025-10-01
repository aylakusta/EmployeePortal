using System.ComponentModel.DataAnnotations;

namespace WebUI.Models
{
    public class JobTitle
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        // İstersen kalsın:
        public bool IsActive { get; set; } = true;

        // ❌ Hiyerarşi alanlarını kaldırdık:
        // public int? ParentJobTitleId { get; set; }
        // public JobTitle? ParentJobTitle { get; set; }
        // public ICollection<JobTitle> Children { get; set; } = new List<JobTitle>();
        // public int? Level { get; set; }
    }
}
