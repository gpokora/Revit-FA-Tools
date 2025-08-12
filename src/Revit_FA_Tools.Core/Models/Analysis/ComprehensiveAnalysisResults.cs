using System;
using System.Collections.Generic;
using Revit_FA_Tools.Core.Models.Electrical;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Core.Models.Recommendations;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Comprehensive results from fire alarm system analysis
    /// </summary>
    public class ComprehensiveAnalysisResults
    {
        public DateTime AnalysisTime { get; set; } = DateTime.Now;
        public TimeSpan ElapsedTime { get; set; }
        public string Scope { get; set; } = string.Empty;
        
        // Core analysis results
        public ElectricalResults ElectricalResults { get; set; }
        public IDNACSystemResults IDNACResults { get; set; }
        public IDNETSystemResults IDNETResults { get; set; }
        public AmplifierRequirements AmplifierResults { get; set; }
        public AddressingResults AddressingResults { get; set; }
        
        // Panel recommendations
        public List<PanelPlacementRecommendation> PanelRecommendations { get; set; } = new List<PanelPlacementRecommendation>();
        
        // System summary
        public SystemSummary SystemSummary { get; set; }
        
        // Validation results
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public bool IsValid => Errors?.Count == 0;
        
        // Device information
        public int TotalDevices { get; set; }
        public Dictionary<string, int> DevicesByType { get; set; } = new Dictionary<string, int>();
        
        // Analysis metrics
        public int TotalElementsAnalyzed { get; set; }
        public TimeSpan TotalAnalysisTime { get; set; }
    }

    /// <summary>
    /// Results from addressing analysis
    /// </summary>
    public class AddressingResults
    {
        public List<DeviceAssignment> Assignments { get; set; } = new List<DeviceAssignment>();
        public List<AddressingCircuit> Circuits { get; set; } = new List<AddressingCircuit>();
        public int TotalAddresses { get; set; }
        public int AvailableAddresses { get; set; }
        public double UtilizationPercent => TotalAddresses > 0 ? (double)(TotalAddresses - AvailableAddresses) / TotalAddresses * 100 : 0;
    }

    /// <summary>
    /// System-wide analysis summary
    /// </summary>
    public class SystemSummary
    {
        public double TotalPowerConsumption { get; set; }
        public double TotalCurrent { get; set; }
        public bool MeetsNFPARequirements { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}