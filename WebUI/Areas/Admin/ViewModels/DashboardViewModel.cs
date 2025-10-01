using System.Collections.Generic;

namespace WebUI.Areas.Admin.ViewModels
{
    public class DashboardKpi
    {
        public string Title { get; set; } = "";
        public string Icon { get; set; } = "bi-speedometer2";
        public string Value { get; set; } = "0";
        public string? Subtext { get; set; }
        public string? Badge { get; set; }
        public string? BadgeClass { get; set; }
        public string? Url { get; set; }
    }

    public class DashboardServiceCapacity
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = "";
        public int SeatCount { get; set; }
        public int ActiveCount { get; set; }
        public double FillRate => SeatCount == 0 ? 0 : (double)ActiveCount / SeatCount;
    }

    public class DashboardUnassigned
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = "";
        public string? Department { get; set; }
        public string? Email { get; set; }
    }

    public class DashboardViewModel
    {
        public List<DashboardKpi> Kpis { get; set; } = new();
        public int TotalSeats { get; set; }
        public int TotalUsedSeats { get; set; }
        public List<DashboardServiceCapacity> HotServices { get; set; } = new();
        public List<DashboardUnassigned> Unassigned { get; set; } = new();
        public int DoughnutUsed => TotalUsedSeats;
        public int DoughnutFree => TotalSeats - TotalUsedSeats;
    }
}
