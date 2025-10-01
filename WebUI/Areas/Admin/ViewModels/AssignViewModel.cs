using System.Collections.Generic;

namespace WebUI.Areas.Admin.ViewModels
{
    public class AssignViewModel
    {
        public int? ServiceId { get; set; }
        public List<AssignCandidate> Candidates { get; set; } = new();
        public List<int>? SelectedEmployeeIds { get; set; }
    }

    public class AssignCandidate
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = "";
        public string? Department { get; set; }
        public string? Email { get; set; }
    }
}
