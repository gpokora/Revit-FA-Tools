using System;
using System.Collections.Generic;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Complete IDNET system analysis results
    /// </summary>
    public class IDNETSystemResults
    {
        public int ChannelsUsed { get; set; }
        public int TotalChannels { get; set; } = 256; // Standard IDNET capacity
        public double NetworkLoad { get; set; }
        public bool IsValid { get; set; } = true;
        public List<IDNETDevice> Devices { get; set; } = new List<IDNETDevice>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        
        /// <summary>
        /// Network performance metrics
        /// </summary>
        public double AverageResponseTime { get; set; }
        public double NetworkUtilization => TotalChannels > 0 ? (double)ChannelsUsed / TotalChannels * 100 : 0;
        public DateTime AnalysisTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// System recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
        
        /// <summary>
        /// Total number of devices in the IDNET system
        /// </summary>
        public int TotalDevices { get; set; }
        
        /// <summary>
        /// Number of channels required for the IDNET system
        /// </summary>
        public int ChannelsRequired => ChannelsUsed;
        
        /// <summary>
        /// Total unit loads for all IDNET devices
        /// </summary>
        public int TotalUnitLoads { get; set; }
        
        /// <summary>
        /// Maximum number of devices per channel (NFPA standard)
        /// </summary>
        public int MaxDevicesPerChannel => 159; // NFPA standard for IDNET
        
        /// <summary>
        /// All devices in the IDNET system - for compatibility
        /// </summary>
        public List<Revit_FA_Tools.IDNETDevice> AllDevices { get; set; } = new List<Revit_FA_Tools.IDNETDevice>();
        
        /// <summary>
        /// Level analysis results - for compatibility
        /// </summary>
        public Dictionary<string, Revit_FA_Tools.IDNETLevelAnalysis> LevelAnalysis { get; set; } = new Dictionary<string, Revit_FA_Tools.IDNETLevelAnalysis>();
        
        /// <summary>
        /// Network segments information
        /// </summary>
        public List<Revit_FA_Tools.IDNETNetworkSegment> NetworkSegments { get; set; } = new List<Revit_FA_Tools.IDNETNetworkSegment>();
        
        /// <summary>
        /// System summary information
        /// </summary>
        public Revit_FA_Tools.Core.Models.Analysis.IDNETSystemSummary SystemSummary { get; set; } = new Revit_FA_Tools.Core.Models.Analysis.IDNETSystemSummary();
        
        /// <summary>
        /// Analysis timestamp
        /// </summary>
        public DateTime AnalysisTimestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Total power consumption across all devices
        /// </summary>
        public double TotalPowerConsumption { get; set; }
    }

    /// <summary>
    /// IDNET system summary information
    /// </summary>
    public class IDNETSystemSummary
    {
        public int TotalDevices { get; set; }
        public int TotalSegments { get; set; }
        public double AverageUtilization { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> CriticalIssues { get; set; } = new List<string>();
        public Dictionary<string, int> DevicesByType { get; set; } = new Dictionary<string, int>();
        
        // Additional properties for compatibility
        public int RecommendedNetworkChannels { get; set; } = 4;
        public int RepeatersRequired { get; set; }
        public double TotalWireLength { get; set; }
        public List<string> SystemRecommendations { get; set; } = new List<string>();
        public bool IntegrationWithIDNAC { get; set; }
        public string PowerSupplyRequirements { get; set; } = string.Empty;
    }

}