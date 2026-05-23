using System.Collections.Generic;

namespace SmartOffice.Web.Models
{
    public class DashboardViewModel
    {
        public int TotalCount { get; set; }
        public double UtilizationRate { get; set; }
        public double HoursSaved { get; set; }
        public int AutoCancelledCount { get; set; }
        public int CheckedInCount { get; set; }
        public int ActiveCount { get; set; }
        public int CancelledByUserCount { get; set; }

		public List<DailyTrendModel> DailyTrend { get; set; } = new();
        public List<TopWorkspaceModel> TopWorkspaces { get; set; } = new();
        public List<PeakHourModel> PeakHours { get; set; } = new();
        public double TotalHoursBooked { get; set; }
        public double ActualHoursUsed { get; set; }
        public double EnergySavedKwh { get; set; }
        public double NoShowRate { get; set; }
        public string SelectedPeriod { get; set; }
        public List<string> AvailablePeriods { get; set; } = new List<string>();
        public List<dynamic> SavedHoursTrend { get; set; } = new List<dynamic>();
    }

    public class DailyTrendModel
    {
        public string Date { get; set; }
        public double Rate { get; set; }
    }

    public class TopWorkspaceModel
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public class PeakHourModel
    {
        public string Hour { get; set; }
        public int Count { get; set; }
    }
}