using System;
using System.Collections.Generic;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Overall validation status for the model
    /// </summary>
    public enum ValidationStatus
    {
        Pass,
        Warning,
        Fail
    }

    /// <summary>
    /// Device type classifications for validation
    /// </summary>
    public enum DeviceType
    {
        Speaker,
        Strobe,
        Horn,
        Combination,
        Control,
        Unknown
    }

    /// <summary>
    /// Confidence level for device classification
    /// </summary>
    public enum ConfidenceLevel
    {
        High,
        Medium,
        Low
    }

    /// <summary>
    /// Summary of model validation results
    /// </summary>
    public class ModelValidationSummary
    {
        public ValidationStatus OverallStatus { get; set; }
        public int TotalDevicesFound { get; set; }
        public int ValidDevicesCount { get; set; }
        public int MissingParametersCount { get; set; }
        public List<ValidationCategoryResult> ValidationDetails { get; set; } = new List<ValidationCategoryResult>();
        public bool AnalysisReadiness { get; set; }
        public List<string> RequiredActions { get; set; } = new List<string>();
        public double ReadinessPercentage { get; set; }
        public string AnalysisAccuracy { get; set; }
    }

    /// <summary>
    /// Validation results for a specific category (e.g., Fire Alarm Families, Parameters, etc.)
    /// </summary>
    public class ValidationCategoryResult
    {
        public string CategoryName { get; set; }
        public ValidationStatus Status { get; set; }
        public int TotalItems { get; set; }
        public int ValidItems { get; set; }
        public int WarningItems { get; set; }
        public int ErrorItems { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Validation results for an individual device
    /// </summary>
    public class DeviceValidationResult
    {
        public int ElementId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public DeviceType DeviceType { get; set; }
        public ConfidenceLevel ClassificationConfidence { get; set; }
        public string Level { get; set; }
        public ValidationStatus ValidationStatus { get; set; }
        public List<string> MissingParameters { get; set; } = new List<string>();
        public List<ParameterValidationResult> ParameterIssues { get; set; } = new List<ParameterValidationResult>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<string> DetectedParameters { get; set; } = new List<string>();
        public string ClassificationReasoning { get; set; }
    }

    /// <summary>
    /// Validation results for a specific parameter
    /// </summary>
    public class ParameterValidationResult
    {
        public string ParameterName { get; set; }
        public object ParameterValue { get; set; }
        public string ExpectedRange { get; set; }
        public ValidationStatus ValidationStatus { get; set; }
        public string IssueDescription { get; set; }
        public object SuggestedValue { get; set; }
        public bool IsEnriched { get; set; }
        public ConfidenceLevel EnrichmentConfidence { get; set; }
    }

    /// <summary>
    /// Model readiness report for analysis
    /// </summary>
    public class ModelReadinessReport
    {
        public double ReadinessPercentage { get; set; }
        public int CriticalIssuesCount { get; set; }
        public int MinorIssuesCount { get; set; }
        public Dictionary<string, double> LevelReadiness { get; set; } = new Dictionary<string, double>();
        public Dictionary<DeviceType, double> DeviceTypeReadiness { get; set; } = new Dictionary<DeviceType, double>();
        public string EstimatedAnalysisAccuracy { get; set; }
        public List<string> AnalysisLimitations { get; set; } = new List<string>();
        public DateTime ValidationTimestamp { get; set; }
    }

    /// <summary>
    /// Issue severity levels for validation problems
    /// </summary>
    public enum IssueSeverity
    {
        Critical,   // Blocks analysis
        Error,      // Impacts accuracy significantly
        Warning,    // Minor impact
        Info        // Quality improvement opportunity
    }

    /// <summary>
    /// Individual validation issue
    /// </summary>
    public class ValidationIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<int> AffectedElementIds { get; set; } = new List<int>();
        public string Resolution { get; set; }
        public string Impact { get; set; }
        public int EstimatedFixTimeMinutes { get; set; }
        public bool CanAutoFix { get; set; }
    }

    /// <summary>
    /// Device classification result
    /// </summary>
    public class DeviceClassificationResult
    {
        public DeviceType DeviceType { get; set; }
        public ConfidenceLevel ConfidenceLevel { get; set; }
        public List<string> DetectedParameters { get; set; } = new List<string>();
        public string ClassificationReasoning { get; set; }
        public Dictionary<string, object> ParameterValues { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Level-specific validation summary
    /// </summary>
    public class LevelValidationSummary
    {
        public string LevelName { get; set; }
        public int DeviceCount { get; set; }
        public int ValidDevices { get; set; }
        public double ReadinessPercentage { get; set; }
        public Dictionary<DeviceType, int> DeviceTypeCounts { get; set; } = new Dictionary<DeviceType, int>();
        public double TotalCurrentDraw { get; set; }
        public double TotalWattage { get; set; }
        public int TotalUnitLoads { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }
}