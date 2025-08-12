using System;
using System.Collections.Generic;

namespace Revit_FA_Tools.Core.Models.Recommendations
{
    /// <summary>
    /// Panel placement recommendation
    /// </summary>
    public class PanelPlacementRecommendation
    {
        public string PanelType { get; set; } = string.Empty;
        public string RecommendedLocation { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public List<string> Advantages { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Panel information for configuration
    /// </summary>
    public class PanelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public double MaxCurrent { get; set; }
        public List<string> Features { get; set; } = new List<string>();
        public Dictionary<string, object> Specifications { get; set; } = new Dictionary<string, object>();
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public double EstimatedCost { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cabinet configuration recommendation
    /// </summary>
    public class CabinetConfiguration
    {
        public string CabinetType { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public List<string> RequiredComponents { get; set; } = new List<string>();
        public Dictionary<string, int> ComponentQuantities { get; set; } = new Dictionary<string, int>();
        public double EstimatedCost { get; set; }
        public string InstallationRequirements { get; set; } = string.Empty;
        public List<string> AccessoryRecommendations { get; set; } = new List<string>();
        public Dictionary<string, object> TechnicalSpecs { get; set; } = new Dictionary<string, object>();
        public string ConfigurationNotes { get; set; } = string.Empty;
        public List<string> ComplianceNotes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Action item for system improvements
    /// </summary>
    public class ActionItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Priority { get; set; } // 1-5 scale
        public string Status { get; set; } = "Pending";
        public DateTime DueDate { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new List<string>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public double EstimatedHours { get; set; }
        public double EstimatedCost { get; set; }
    }

    /// <summary>
    /// System summary dashboard data
    /// </summary>
    public class SystemSummaryDashboard
    {
        public string ProjectName { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; } = DateTime.Now;
        public string OverallStatus { get; set; } = string.Empty;
        public int TotalDevices { get; set; }
        public double TotalCurrent { get; set; }
        public double TotalWattage { get; set; }
        public int TotalIDNACs { get; set; }
        public int CriticalIssues { get; set; }
        public int WarningCount { get; set; }
        public List<ActionItem> TopPriorityActions { get; set; } = new List<ActionItem>();
        public Dictionary<string, object> KeyMetrics { get; set; } = new Dictionary<string, object>();
        public List<string> RecentChanges { get; set; } = new List<string>();
        public string Recommendations { get; set; } = string.Empty;
        public Dictionary<string, double> ComplianceScore { get; set; } = new Dictionary<string, double>();
    }
}