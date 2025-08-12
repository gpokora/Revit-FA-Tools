using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Core.Models.Electrical
{
    /// <summary>
    /// Electrical data for fire alarm devices
    /// </summary>
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

    /// <summary>
    /// Element electrical data for analysis
    /// </summary>
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

    /// <summary>
    /// Family aggregated data
    /// </summary>
    public class FamilyData
    {
        public int Count { get; set; }
        public double Wattage { get; set; }
        public double Current { get; set; }
    }

    /// <summary>
    /// Comprehensive electrical analysis results
    /// </summary>
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
        
        /// <summary>
        /// Alias for TotalWattage for backward compatibility
        /// </summary>
        public double TotalPower => TotalWattage;
        
        /// <summary>
        /// Validation status - true if analysis completed without critical errors
        /// </summary>
        public bool IsValid => Elements?.Any() == true && TotalCurrent > 0;
    }

    /// <summary>
    /// Level-based electrical data analysis
    /// </summary>
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

    /// <summary>
    /// Device type analysis results
    /// </summary>
    public class DeviceTypeAnalysis
    {
        public Dictionary<string, DeviceTypeData> DeviceTypes { get; set; } = new Dictionary<string, DeviceTypeData>();
    }

    /// <summary>
    /// Data for a specific device type
    /// </summary>
    public class DeviceTypeData
    {
        public int Count { get; set; }
        public double TotalCurrent { get; set; }
        public double TotalWattage { get; set; }
        public double AverageCurrent => Count > 0 ? TotalCurrent / Count : 0;
        public double AverageWattage => Count > 0 ? TotalWattage / Count : 0;
    }

    /// <summary>
    /// Amplifier requirements analysis
    /// </summary>
    public class AmplifierRequirements
    {
        public int AmplifiersRequired { get; set; }
        public double TotalWatts { get; set; }
        public double SpeakerWatts { get; set; }
        public double StrobeWatts { get; set; }
        public string RecommendedModel { get; set; } = string.Empty;
        public List<string> RequirementDetails { get; set; } = new List<string>();
        public bool MultiplePanelsNeeded { get; set; }
        public Dictionary<string, double> RequirementsByFloor { get; set; } = new Dictionary<string, double>();
    }
}