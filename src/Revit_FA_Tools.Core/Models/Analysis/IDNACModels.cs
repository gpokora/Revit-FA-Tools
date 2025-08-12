using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// IDNAC analysis result data
    /// </summary>
    public class IDNACAnalysisResult
    {
        public int IdnacsRequired { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LimitingFactor { get; set; } = string.Empty;
        public double Current { get; set; }
        public double Wattage { get; set; }
        public int Devices { get; set; }
        public int UnitLoads { get; set; }
        public SpareCapacityInfo SpareInfo { get; set; } = new SpareCapacityInfo();
    }

    /// <summary>
    /// Spare capacity information
    /// </summary>
    public class SpareCapacityInfo
    {
        public double SparePercent { get; set; }
        public double SpareAmps { get; set; }
        public int SpareDevices { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// IDNAC warning information
    /// </summary>
    public class IDNACWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Complete IDNAC system analysis results
    /// </summary>
    public class IDNACSystemResults
    {
        public double TotalCurrent { get; set; }
        public double TotalWattage { get; set; }
        public int TotalDevices { get; set; }
        public int TotalUnitLoads { get; set; }
        public List<IDNACAnalysisResult> LevelAnalysis { get; set; } = new List<IDNACAnalysisResult>();
        public List<IDNACWarning> Warnings { get; set; } = new List<IDNACWarning>();
        public AmplifierRequirements AmplifierNeeds { get; set; } = new AmplifierRequirements();
        public OptimizationSummary Optimization { get; set; } = new OptimizationSummary();
        public DeviceTypeAnalysis DeviceAnalysis { get; set; } = new DeviceTypeAnalysis();
        public List<PanelPlacementRecommendation> PanelRecommendations { get; set; } = new List<PanelPlacementRecommendation>();

        // Status properties
        public string OverallStatus => Warnings?.Any(w => w.Severity == "HIGH") == true ? "Critical" : 
                                     Warnings?.Any(w => w.Severity == "MEDIUM") == true ? "Warning" : "Good";

        // Summary metrics
        public double AverageCurrentPerDevice => TotalDevices > 0 ? TotalCurrent / TotalDevices : 0;
        public double AverageWattagePerDevice => TotalDevices > 0 ? TotalWattage / TotalDevices : 0;
        public int TotalIDNACsRequired => LevelAnalysis?.Sum(l => l.IdnacsRequired) ?? 0;
        
        // Alias for backward compatibility with ReportBuilder
        public int TotalIdnacsNeeded => TotalIDNACsRequired;
        
        // Level results for detailed reporting
        public Dictionary<string, IDNACAnalysisResult> LevelResults => 
            LevelAnalysis?.GroupBy(r => r.Status)
                        .ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<string, IDNACAnalysisResult>();
        
        // Additional properties for reporting compatibility
        public int CircuitsCreated => TotalIDNACsRequired;
        public int DevicesAddressed => TotalDevices;
        public double CapacityUsedPercent => TotalIDNACsRequired > 0 ? (TotalCurrent / (TotalIDNACsRequired * 3.0)) * 100 : 0;
    }

    /// <summary>
    /// Optimization summary information
    /// </summary>
    public class OptimizationSummary
    {
        public List<string> Recommendations { get; set; } = new List<string>();
        public double PotentialSavings { get; set; }
        public int OptimizationPriority { get; set; } // 1-5 scale
        public string OptimizationStatus { get; set; } = string.Empty;
        public Dictionary<string, string> OptimizationDetails { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// IDNAC analysis grid display item
    /// </summary>
    public class IDNACAnalysisGridItem
    {
        public string Level { get; set; } = string.Empty;
        public int Devices { get; set; }
        public double Current { get; set; }
        public double Wattage { get; set; }
        public int UnitLoads { get; set; }
        public int IDNACsRequired { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LimitingFactor { get; set; } = string.Empty;
        public double Utilization { get; set; }
        public string UtilizationDisplay => $"{Utilization:P1}";
        public string CurrentDisplay => $"{Current:F2}A";
        public string WattageDisplay => $"{Wattage:F1}W";
        
        // Visual properties for grid display
        public System.Windows.Media.Brush? StatusColor { get; set; }
        public string StatusIcon { get; set; } = string.Empty;
    }
}