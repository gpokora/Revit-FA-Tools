using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools
{

    public class ElectricalData
    {
        public double Wattage { get; set; }
        public double Current { get; set; }
        public double Voltage { get; set; } = 24.0; // Fire alarm system nominal voltage
        public double ApparentPower { get; set; }
        public double PowerFactor { get; set; } = 1.0;
        public List<string> FoundParams { get; set; } = new List<string>();
        public List<string> CalculatedParams { get; set; } = new List<string>();
    }

    public class ElementData
    {
        public long Id { get; set; }
        public FamilyInstance? Element { get; set; }
        public string? FamilyName { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Wattage { get; set; }
        public double Current { get; set; }
        public double Voltage { get; set; }
        public List<string> FoundParams { get; set; } = new List<string>();
        public List<string> CalculatedParams { get; set; } = new List<string>();
        public string LevelName { get; set; } = "Unknown Level";
        
        // Additional property for grid display
        public long ElementId => Id;
    }

    public class FamilyData
    {
        public int Count { get; set; }
        public double Wattage { get; set; }
        public double Current { get; set; }
    }

    public class ElectricalResults
    {
        public List<ElementData> Elements { get; set; } = new List<ElementData>();
        public Dictionary<string, double> Totals { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, FamilyData> ByFamily { get; set; } = new Dictionary<string, FamilyData>();
        public Dictionary<string, LevelData> ByLevel { get; set; } = new Dictionary<string, LevelData>();
        
        /// <summary>
        /// Alias for ByLevel for backward compatibility
        /// </summary>
        public Dictionary<string, LevelData> LevelData => ByLevel;
        
        /// <summary>
        /// Total current across all devices
        /// </summary>
        public double TotalCurrent => Elements?.Sum(e => e.Current) ?? 0.0;
        
        /// <summary>
        /// Total wattage across all devices
        /// </summary>
        public double TotalWattage => Elements?.Sum(e => e.Wattage) ?? 0.0;
        
        /// <summary>
        /// Total number of devices
        /// </summary>
        public int TotalDevices => Elements?.Count ?? 0;
        
        /// <summary>
        /// Total unit loads across all devices
        /// </summary>
        public int TotalUnitLoads => Elements?.Sum(e => (int)(e.Current * 1.25)) ?? 0; // Estimate UL from current
    }

    public class LevelData
    {
        public int Devices { get; set; }
        public double Current { get; set; }
        public double Wattage { get; set; }
        public Dictionary<string, int> Families { get; set; } = new Dictionary<string, int>();
        public bool Combined { get; set; }
        public bool RequiresIsolators { get; set; }
        public List<string> OriginalFloors { get; set; } = new List<string>();
        public double UtilizationPercent { get; set; }
    }

    // IDNAC Analysis Models
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
        public bool IsCombined { get; set; }
        public List<string> OriginalFloors { get; set; } = new List<string>();
        public bool RequiresIsolators { get; set; }
    }

    public class SpareCapacityInfo
    {
        public double SpareCurrent { get; set; }
        public int SpareDevices { get; set; }
        public double CurrentUtilization { get; set; }
        public double DeviceUtilization { get; set; }
    }

    public class IDNACWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    public class IDNACSystemResults
    {
        public Dictionary<string, IDNACAnalysisResult> LevelAnalysis { get; set; } = new Dictionary<string, IDNACAnalysisResult>();
        public int TotalIdnacsNeeded { get; set; }
        public List<IDNACWarning> Warnings { get; set; } = new List<IDNACWarning>();
        public Dictionary<string, FamilyData> FireAlarmFamilies { get; set; } = new Dictionary<string, FamilyData>();
        public Dictionary<string, FamilyData> OtherFamilies { get; set; } = new Dictionary<string, FamilyData>();
        public OptimizationSummary? OptimizationSummary { get; set; }
        
        /// <summary>
        /// Alias for LevelAnalysis for backward compatibility
        /// </summary>
        public Dictionary<string, IDNACAnalysisResult> LevelResults => LevelAnalysis;
        
        /// <summary>
        /// Total current across all IDNAC circuits
        /// </summary>
        public double TotalCurrent => LevelAnalysis?.Values.Sum(l => l.Current) ?? 0.0;
        
        /// <summary>
        /// Total number of devices in the IDNAC system
        /// </summary>
        public int TotalDevices => LevelAnalysis?.Values.Sum(l => l.Devices) ?? 0;
        
        /// <summary>
        /// Total unit loads across all IDNAC circuits
        /// </summary>
        public int TotalUnitLoads => LevelAnalysis?.Values.Sum(l => l.UnitLoads) ?? 0;
    }

    public class OptimizationSummary
    {
        public int OriginalFloors { get; set; }
        public int OptimizedFloors { get; set; }
        public int Reduction { get; set; }
        public double ReductionPercent { get; set; }
        public List<Tuple<string, LevelData>> CombinedFloors { get; set; } = new List<Tuple<string, LevelData>>();
    }

    // Amplifier Models
    public class DeviceTypeAnalysis
    {
        public Dictionary<string, DeviceTypeData> DeviceTypes { get; set; } = new Dictionary<string, DeviceTypeData>();
    }

    public class DeviceTypeData
    {
        public int Count { get; set; }
        public double Current { get; set; }
        public double Wattage { get; set; }
        public Dictionary<string, int> Families { get; set; } = new Dictionary<string, int>();
    }

    public class AmplifierRequirements
    {
        public int AmplifiersNeeded { get; set; }
        public string AmplifierType { get; set; } = string.Empty;
        public int AmplifierBlocks { get; set; }
        public double AmplifierPowerUsable { get; set; }
        public double AmplifierPowerMax { get; set; }
        public double AmplifierCurrent { get; set; }
        public int SpeakerCount { get; set; }
        public int SpareCapacityPercent { get; set; } = 20;
    }

    // Panel Placement Models
    public class PanelPlacementRecommendation
    {
        public string Strategy { get; set; } = string.Empty;
        public int PanelCount { get; set; }
        public Tuple<string, string>? Location { get; set; } // Location, Reasoning
        public string Reasoning { get; set; } = string.Empty;
        public CabinetConfiguration? Equipment { get; set; }
        public AmplifierRequirements? AmplifierInfo { get; set; }
        public List<string> Advantages { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();
        public List<PanelInfo> Panels { get; set; } = new List<PanelInfo>();
        public string AmplifierStrategy { get; set; } = string.Empty;
        public Dictionary<string, object> SystemTotals { get; set; } = new Dictionary<string, object>();
        public int MinPanelsNeeded { get; set; }
    }

    public class PanelInfo
    {
        public string PanelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<string> ServesLevels { get; set; } = new List<string>();
        public int IdnacsRequired { get; set; }
        public int SpeakersEstimated { get; set; }
        public int AmplifiersNeeded { get; set; }
        public string AmplifierType { get; set; } = string.Empty;
        public int AmplifierBlocks { get; set; }
        public double AmplifierCurrent { get; set; }
        public string AmplifierInfo { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }

    public class CabinetConfiguration
    {
        public string CabinetType { get; set; } = string.Empty;
        public int PowerSupplies { get; set; }
        public int TotalIdnacs { get; set; }
        public int AvailableBlocks { get; set; }
        public int AmplifierBlocksUsed { get; set; }
        public int RemainingBlocks { get; set; }
        public double AmplifierCurrent { get; set; }
        public double EsPsCapacity { get; set; }
        public double PowerMargin { get; set; }
        public bool BatteryChargerAvailable { get; set; }
        public List<string> ModelConfig { get; set; } = new List<string>();
    }

    // UI Data Models for Results Display
    public class LevelDetailItem
    {
        public string Level { get; set; } = string.Empty;
        public int Devices { get; set; }
        public string Current { get; set; } = string.Empty;
        public string Wattage { get; set; } = string.Empty;
        public int IdnacsRequired { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LimitingFactor { get; set; } = string.Empty;
    }

    public class FamilyDetailItem
    {
        public string? FamilyName { get; set; }
        public int Count { get; set; }
        public string TotalCurrent { get; set; } = string.Empty;
        public string TotalWattage { get; set; } = string.Empty;
        public string AvgCurrent { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    // Data model for IDNAC Analysis Grid

    public class IDNACAnalysisGridItem
    {
        public string Level { get; set; } = string.Empty;
        public int Devices { get; set; }
        public string Current { get; set; } = string.Empty;
        public string Wattage { get; set; } = string.Empty;
        public int IdnacsRequired { get; set; }
        public string UtilizationPercent { get; set; } = string.Empty;
        public string UtilizationCategory { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LimitingFactor { get; set; } = string.Empty;
        public string SpareCurrent { get; set; } = string.Empty;
        public string SpareDevices { get; set; } = string.Empty;
        public string IsolatorsRequired { get; set; } = string.Empty;
        public string OriginalFloors { get; set; } = string.Empty;
        public bool IsCombined { get; set; }

        // Enhanced properties for visual styling
        public System.Windows.Media.Brush UtilizationBrush
        {
            get
            {
                if (UtilizationCategory == "Optimized")
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                if (UtilizationCategory == "Excellent")
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
                if (UtilizationCategory == "Underutilized")
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
            }
        }

    }

    // Enhanced Visual Metric Cards
    public class SystemMetricCard
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? IconColor { get; set; }
        public System.Windows.Media.Brush? ValueColor { get; set; }
        public string Subtitle { get; set; } = string.Empty;
        public double ProgressValue { get; set; }
        public System.Windows.Media.Brush? ProgressColor { get; set; }
    }

    // Enhanced Device Category Display
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
        public System.Windows.Media.Brush? CategoryColor { get; set; }
        public string PowerRequirement { get; set; } = string.Empty;
        public bool RequiresAmplifier { get; set; }
        public Dictionary<string, int> Families { get; set; } = new Dictionary<string, int>();

        // Visual properties
        public string StatusIcon => RequiresAmplifier ? "🔊⚡" : "⚡";
        public System.Windows.Media.Brush StatusColor => RequiresAmplifier ?
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 39, 176)) :
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
    }


    // Enhanced Warning Display
    public class SystemWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? SeverityColor { get; set; }
        public System.Windows.Media.Brush? BackgroundColor { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        // Visual styling based on severity
        public static SystemWarning FromIDNACWarning(IDNACWarning warning)
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
                    systemWarning.SeverityColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
                    systemWarning.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 238));
                    break;
                case "MEDIUM":
                    systemWarning.Icon = "⚠️";
                    systemWarning.SeverityColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                    systemWarning.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 243, 224));
                    break;
                default:
                    systemWarning.Icon = "ℹ️";
                    systemWarning.SeverityColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
                    systemWarning.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 242, 253));
                    break;
            }

            return systemWarning;
        }
    }

    // Enhanced Amplifier Display
    public class AmplifierAnalysisCard
    {
        public string RequirementType { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? IconColor { get; set; }
        public bool IsHighlight { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double NumericValue { get; set; }
        public string Status { get; set; } = string.Empty;

        // Progress indicator for requirements
        public double ProgressPercent { get; set; }
        public System.Windows.Media.Brush? ProgressColor { get; set; }
    }

    // Action Items and Recommendations
    public class SystemRecommendation
    {
        public string Action { get; set; } = string.Empty;
        public string TabReference { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public Dictionary<string, object> TechnicalDetails { get; set; } = new Dictionary<string, object>();

        // Visual properties
        public System.Windows.Media.Brush? PriorityColor { get; set; }
        public string PriorityIcon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? CategoryColor { get; set; }
        public string ActionRequired { get; set; } = string.Empty;
        public bool IsUrgent { get; set; }
    }

    // Panel Placement Analysis
    public class PanelPlacementCard
    {
        public string LocationName { get; set; } = string.Empty;
        public string FloorLocation { get; set; } = string.Empty;
        public int RecommendedPanels { get; set; }
        public List<string> FloorsCovered { get; set; } = new List<string>();
        public double CoverageRadius { get; set; }
        public string AccessibilityRating { get; set; } = string.Empty;
        public string MaintenanceRating { get; set; } = string.Empty;
        public List<string> Advantages { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();

        // Technical requirements
        public int IdnacsSupported { get; set; }
        public int AmplifiersRequired { get; set; }
        public double PowerRequirement { get; set; }
        public string CabinetConfiguration { get; set; } = string.Empty;
        public bool BatteryBackupRequired { get; set; }

        // Visual properties
        public System.Windows.Media.Brush? LocationColor { get; set; }
        public string LocationIcon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? RatingColor { get; set; }
        public double OverallScore { get; set; }
        public string RecommendationLevel { get; set; } = string.Empty;
    }

    // Optimization Result Display
    public class OptimizationResultCard
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int OriginalFloors { get; set; }
        public int OptimizedFloors { get; set; }
        public int Reduction { get; set; }
        public double ReductionPercent { get; set; }
        public double EfficiencyGain { get; set; }
        public string SummaryText { get; set; } = string.Empty;
        public List<string> CombinedFloors { get; set; } = new List<string>();
        public string ImpactLevel { get; set; } = string.Empty;

        // Visual properties
        public System.Windows.Media.Brush? ImpactColor { get; set; }
        public string ImpactIcon { get; set; } = string.Empty;
        public double ProgressValue => ReductionPercent;
        public System.Windows.Media.Brush? ProgressColor { get; set; }
        public string SuccessLevel { get; set; } = string.Empty;
    }

    // Enhanced Level Analysis Display
    public class LevelAnalysisCard
    {
        public string LevelName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public double Current { get; set; }
        public double Wattage { get; set; }
        public int IdnacsRequired { get; set; }
        public double UtilizationPercent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LimitingFactor { get; set; } = string.Empty;
        public bool IsCombined { get; set; }
        public bool RequiresIsolators { get; set; }
        public List<string> OriginalFloors { get; set; } = new List<string>();
        public Dictionary<string, int> DeviceFamilies { get; set; } = new Dictionary<string, int>();

        // Spare capacity details
        public double SpareCurrent { get; set; }
        public int SpareDevices { get; set; }
        public double CurrentUtilization { get; set; }
        public double DeviceUtilization { get; set; }

        // Visual styling
        public System.Windows.Media.Brush? UtilizationColor { get; set; }
        public System.Windows.Media.Brush? StatusColor { get; set; }
        public string StatusIcon { get; set; } = string.Empty;
        public bool IsOptimized { get; set; }
        public string OptimizationBadge { get; set; } = string.Empty;
    }

    // System Summary Dashboard
    public class SystemSummaryDashboard
    {
        public SystemMetricCard? TotalDevices { get; set; }
        public SystemMetricCard? TotalCurrent { get; set; }
        public SystemMetricCard? IdnacsRequired { get; set; }
        public SystemMetricCard? SpeakerSystem { get; set; }
        public SystemMetricCard? SystemEfficiency { get; set; }
        public SystemMetricCard? PowerSupplies { get; set; }
        public SystemMetricCard? EstimatedPanels { get; set; }
        public SystemMetricCard? ComplianceStatus { get; set; }

        public OptimizationResultCard? OptimizationResults { get; set; }
        public List<SystemWarning> SystemWarnings { get; set; } = new List<SystemWarning>();
        public List<string> KeyRecommendations { get; set; } = new List<string>();
        public List<string> NextSteps { get; set; } = new List<string>();

        // Overall system health
        public string SystemHealth { get; set; } = string.Empty;
        public System.Windows.Media.Brush? HealthColor { get; set; }
        public string HealthIcon { get; set; } = string.Empty;
        public double OverallScore { get; set; }
    }

    // Action Items and Recommendations
    public class ActionItem
    {
        public int Priority { get; set; }
        public string PriorityDisplay => $"{Priority}️⃣";
        public string Action { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TabReference { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty;
        public System.Windows.Media.Brush? UrgencyColor { get; set; }
        public string Icon { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string EstimatedEffort { get; set; } = string.Empty;
        public string ExpectedBenefit { get; set; } = string.Empty;
    }

    // Export and Reporting Models
    public class ReportSection
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new List<string>();
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        public List<ReportTable> Tables { get; set; } = new List<ReportTable>();
        public List<ReportChart> Charts { get; set; } = new List<ReportChart>();
    }

    public class ReportTable
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }

    public class ReportChart
    {
        public string Title { get; set; } = string.Empty;
        public string ChartType { get; set; } = string.Empty;
        public Dictionary<string, double> Data { get; set; } = new Dictionary<string, double>();
    }

    #region IDNET Data Models

    public class IDNETDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? FamilyName { get; set; }
        public string DeviceType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public double PowerConsumption { get; set; }
        public int UnitLoads { get; set; }
        public string Zone { get; set; } = string.Empty;
        public XYZ? Position { get; set; }
        public int SuggestedAddress { get; set; }
        public string NetworkSegment { get; set; } = string.Empty;
        
        // ENHANCED: Additional IDNET parameter extraction
        public string Address { get; set; } = string.Empty;              // Any parameter containing "ADDRESS"
        public string Function { get; set; } = string.Empty;             // Any parameter containing "FUNCTION"  
        public string Area { get; set; } = string.Empty;                 // Any parameter containing "AREA"
        
        // Additional extracted parameters for comprehensive analysis
        public Dictionary<string, string> AddressParameters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> FunctionParameters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> AreaParameters { get; set; } = new Dictionary<string, string>();
        
        // Summary of all extracted parameters for debugging/analysis
        public List<string> ExtractedParameters { get; set; } = new List<string>();
    }

    public class IDNETLevelAnalysis
    {
        public string LevelName { get; set; } = string.Empty;
        public int TotalDevices { get; set; }
        public Dictionary<string, int> DeviceTypeCount { get; set; } = new Dictionary<string, int>();
        public double TotalPowerConsumption { get; set; }
        public int TotalUnitLoads { get; set; }
        public int SuggestedNetworkSegments { get; set; }
        public List<IDNETDevice> Devices { get; set; } = new List<IDNETDevice>();
    }

    public class IDNETNetworkSegment
    {
        public string SegmentId { get; set; } = string.Empty;
        public List<IDNETDevice> Devices { get; set; } = new List<IDNETDevice>();
        public double EstimatedWireLength { get; set; }
        public int DeviceCount { get; set; }
        public bool RequiresRepeater { get; set; }
        public string StartingAddress { get; set; } = string.Empty;
        public string EndingAddress { get; set; } = string.Empty;
        public List<string> CoveredLevels { get; set; } = new List<string>();
    }

    public class IDNETSystemResults
    {
        public Dictionary<string, IDNETLevelAnalysis> LevelAnalysis { get; set; } = new Dictionary<string, IDNETLevelAnalysis>();
        public List<IDNETDevice> AllDevices { get; set; } = new List<IDNETDevice>();
        public int TotalDevices { get; set; }
        public double TotalPowerConsumption { get; set; }
        public int TotalUnitLoads { get; set; }
        public List<IDNETNetworkSegment> NetworkSegments { get; set; } = new List<IDNETNetworkSegment>();
        public IDNETSystemSummary? SystemSummary { get; set; }
        public DateTime AnalysisTimestamp { get; set; }
        
        /// <summary>
        /// Number of channels required for the IDNET system
        /// </summary>
        public int ChannelsRequired => SystemSummary?.RecommendedNetworkChannels ?? 0;
        
        /// <summary>
        /// Maximum number of devices per channel
        /// </summary>
        public int MaxDevicesPerChannel => 159; // NFPA standard for IDNET
    }

    public class IDNETSystemSummary
    {
        public int RecommendedNetworkChannels { get; set; }
        public int RepeatersRequired { get; set; }
        public double TotalWireLength { get; set; }
        public List<string> SystemRecommendations { get; set; } = new List<string>();
        public bool IntegrationWithIDNAC { get; set; }
        public string PowerSupplyRequirements { get; set; } = string.Empty;
    }

    public class IDNETAnalysisGridItem
    {
        public string Level { get; set; } = string.Empty;
        public string SmokeDetectors { get; set; } = string.Empty;
        public string HeatDetectors { get; set; } = string.Empty;
        public string ManualStations { get; set; } = string.Empty;
        public string Modules { get; set; } = string.Empty;
        public string TotalDevices { get; set; } = string.Empty;
        public string PowerConsumption { get; set; } = string.Empty;
        public string NetworkSegments { get; set; } = string.Empty;
        public string AddressRange { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data model for the System Overview grid showing combined IDNAC and IDNET analysis by level
    /// </summary>
    public class SystemOverviewData
    {
        public string Level { get; set; } = string.Empty;
        public int IDNACDevices { get; set; }
        public int IDNETDevices { get; set; }
        public int TotalDevices { get; set; }
        public double IDNACCurrent { get; set; }
        public string IDNETCurrent { get; set; } = string.Empty;
        public int IDNACCircuits { get; set; }
        public int IDNETSegments { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Additional properties for compatibility
        public double IDNACWattage { get; set; }
        public int IDNETPoints { get; set; }
        public int IDNETUnitLoads { get; set; }
        public int IDNETChannels { get; set; }
        public double UtilizationPercent { get; set; }
        public string LimitingFactor { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
    }

    #endregion

}