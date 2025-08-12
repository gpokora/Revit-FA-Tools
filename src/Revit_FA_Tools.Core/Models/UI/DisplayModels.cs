using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Revit_FA_Tools.Core.Models.UI
{
    /// <summary>
    /// Level detail item for UI display
    /// </summary>
    public class LevelDetailItem
    {
        public string Level { get; set; } = string.Empty;
        public int Devices { get; set; }
        public double Current { get; set; }
        public string CurrentDisplay => $"{Current:F2}A";
        public double Wattage { get; set; }
        public string WattageDisplay => $"{Wattage:F1}W";
        public Dictionary<string, int> Families { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Family detail item for UI display
    /// </summary>
    public class FamilyDetailItem
    {
        public string Family { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Current { get; set; }
        public string CurrentDisplay => $"{Current:F2}A";
        public double Wattage { get; set; }
        public string WattageDisplay => $"{Wattage:F1}W";
        public double AverageCurrent => Count > 0 ? Current / Count : 0;
        public string AverageCurrentDisplay => $"{AverageCurrent:F3}A";
    }

    /// <summary>
    /// System metric card for dashboard display
    /// </summary>
    public class SystemMetricCard
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Brush? IconColor { get; set; }
        public Brush? ValueColor { get; set; }
        public string Subtitle { get; set; } = string.Empty;
        public double ProgressValue { get; set; }
        public Brush? ProgressColor { get; set; }
    }

    /// <summary>
    /// Device category card for enhanced display
    /// </summary>
    public class DeviceCategoryCard
    {
        public string CategoryName { get; set; } = string.Empty;
        public string DisplayName => CategoryName?.ToUpper();
        public int Count { get; set; }
        public string CountDisplay => Count.ToString("N0");
        public double Current { get; set; }
        public string CurrentDisplay => $"{Current:F2}A";
        public double Wattage { get; set; }
        public string WattageDisplay => $"{Wattage:F1}W";
        public string AvgCurrent => Count > 0 ? $"{(Current / Count):F3}A" : "0A";
        public string Icon { get; set; } = string.Empty;
        public Brush? CategoryColor { get; set; }
        public string PowerRequirement { get; set; } = string.Empty;
        public bool RequiresAmplifier { get; set; }
        public Dictionary<string, int> Families { get; set; } = new Dictionary<string, int>();

        // Visual properties
        public string StatusIcon => RequiresAmplifier ? "🔊⚡" : "⚡";
        public Brush StatusColor => RequiresAmplifier ?
            new SolidColorBrush(Color.FromRgb(156, 39, 176)) :
            new SolidColorBrush(Color.FromRgb(76, 175, 80));
    }

    /// <summary>
    /// Enhanced warning display
    /// </summary>
    public class SystemWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Brush? SeverityColor { get; set; }
        public Brush? BackgroundColor { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        // Visual styling based on severity
        public static SystemWarning FromIDNACWarning(Analysis.IDNACWarning warning)
        {
            var systemWarning = new SystemWarning
            {
                Type = warning.Type,
                Severity = warning.Severity,
                Message = warning.Message,
                Recommendation = warning.Recommendation,
                Details = warning.Details
            };

            switch (warning.Severity?.ToUpper())
            {
                case "HIGH":
                    systemWarning.Icon = "🚨";
                    systemWarning.SeverityColor = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    systemWarning.BackgroundColor = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                    break;
                case "MEDIUM":
                    systemWarning.Icon = "⚠️";
                    systemWarning.SeverityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    systemWarning.BackgroundColor = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                    break;
                default:
                    systemWarning.Icon = "ℹ️";
                    systemWarning.SeverityColor = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    systemWarning.BackgroundColor = new SolidColorBrush(Color.FromRgb(227, 242, 253));
                    break;
            }

            return systemWarning;
        }
    }

    /// <summary>
    /// Enhanced amplifier display
    /// </summary>
    public class AmplifierAnalysisCard
    {
        public string RequirementType { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Brush? IconColor { get; set; }
        public bool IsHighlight { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double NumericValue { get; set; }
        public string Status { get; set; } = string.Empty;

        // Progress indicator for requirements
        public double ProgressPercent { get; set; }
        public Brush? ProgressColor { get; set; }
    }

    /// <summary>
    /// System recommendation display
    /// </summary>
    public class SystemRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Priority { get; set; } // 1-5 scale
        public string Impact { get; set; } = string.Empty;
        public string EstimatedCost { get; set; } = string.Empty;
        public List<string> ActionItems { get; set; } = new List<string>();
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        // Visual properties
        public string PriorityDisplay => Priority switch
        {
            1 => "Low",
            2 => "Medium-Low", 
            3 => "Medium",
            4 => "High",
            5 => "Critical",
            _ => "Unknown"
        };

        public Brush PriorityColor => Priority switch
        {
            >= 4 => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
            3 => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange  
            2 => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow
            _ => new SolidColorBrush(Color.FromRgb(76, 175, 80)) // Green
        };
    }

    /// <summary>
    /// Panel placement card display
    /// </summary>
    public class PanelPlacementCard
    {
        public string Location { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double ConfidenceScore { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public List<string> Advantages { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();

        // Visual properties
        public string ConfidenceDisplay => $"{ConfidenceScore:P0}";
        public Brush ConfidenceColor => ConfidenceScore switch
        {
            >= 0.8 => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
            >= 0.6 => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow
            >= 0.4 => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
            _ => new SolidColorBrush(Color.FromRgb(244, 67, 54)) // Red
        };

        public string StatusIcon => Status switch
        {
            "Recommended" => "✅",
            "Alternative" => "🔄", 
            "NotRecommended" => "❌",
            _ => "❓"
        };
    }

    /// <summary>
    /// Optimization result card
    /// </summary>
    public class OptimizationResultCard
    {
        public string OptimizationType { get; set; } = string.Empty;
        public string CurrentValue { get; set; } = string.Empty;
        public string OptimizedValue { get; set; } = string.Empty;
        public string Improvement { get; set; } = string.Empty;
        public double ImprovementPercent { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ActionItems { get; set; } = new List<string>();
        public string Icon { get; set; } = string.Empty;

        // Visual properties
        public string ImprovementDisplay => $"{ImprovementPercent:+0.0;-0.0;0}%";
        public Brush ImprovementColor => ImprovementPercent switch
        {
            > 0 => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green - positive improvement
            < 0 => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red - negative impact
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Gray - no change
        };
    }

    /// <summary>
    /// Level analysis card for detailed display
    /// </summary>
    public class LevelAnalysisCard
    {
        public string Level { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public double CurrentDraw { get; set; }
        public double Wattage { get; set; }
        public int RequiredIDNACs { get; set; }
        public double Utilization { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> DeviceTypes { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new Dictionary<string, object>();

        // Display properties
        public string DeviceCountDisplay => DeviceCount.ToString("N0");
        public string CurrentDisplay => $"{CurrentDraw:F2}A";
        public string WattageDisplay => $"{Wattage:F1}W";
        public string UtilizationDisplay => $"{Utilization:P1}";
        public string DeviceTypesDisplay => string.Join(", ", DeviceTypes.Take(3)) + 
            (DeviceTypes.Count > 3 ? $" +{DeviceTypes.Count - 3} more" : "");

        // Visual properties
        public Brush StatusColor => Status switch
        {
            "Good" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            "Warning" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            "Critical" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };

        public string StatusIcon => Status switch
        {
            "Good" => "✅",
            "Warning" => "⚠️",
            "Critical" => "🚨",
            _ => "❓"
        };
    }
}