using System;
using System.Collections.Generic;

namespace Revit_FA_Tools.Core.Models.Reporting
{
    /// <summary>
    /// Report section for structured reporting
    /// </summary>
    public class ReportSection
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<ReportTable> Tables { get; set; } = new List<ReportTable>();
        public List<ReportChart> Charts { get; set; } = new List<ReportChart>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Report table for data display
    /// </summary>
    public class ReportTable
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
        public Dictionary<string, string> Formatting { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Report chart for data visualization
    /// </summary>
    public class ReportChart
    {
        public string Title { get; set; } = string.Empty;
        public string ChartType { get; set; } = string.Empty; // Bar, Line, Pie, etc.
        public Dictionary<string, List<double>> Data { get; set; } = new Dictionary<string, List<double>>();
        public List<string> Labels { get; set; } = new List<string>();
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// System overview data for reporting
    /// </summary>
    public class SystemOverviewData
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectNumber { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; } = DateTime.Now;
        public string Engineer { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        
        // System metrics
        public int TotalDevices { get; set; }
        public double TotalCurrent { get; set; }
        public double TotalWattage { get; set; }
        public int TotalLevels { get; set; }
        public int TotalCircuits { get; set; }
        
        // Analysis results
        public string OverallStatus { get; set; } = string.Empty;
        public double SystemReliability { get; set; }
        public double ComplianceScore { get; set; }
        public int CriticalIssues { get; set; }
        public int Warnings { get; set; }
        
        // System breakdown
        public Dictionary<string, int> DevicesByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> LoadByLevel { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, string> SystemComponents { get; set; } = new Dictionary<string, string>();
        
        // Recommendations
        public List<string> KeyRecommendations { get; set; } = new List<string>();
        public List<string> ComplianceNotes { get; set; } = new List<string>();
        public string ExecutiveSummary { get; set; } = string.Empty;
        
        // Additional metadata
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }
}