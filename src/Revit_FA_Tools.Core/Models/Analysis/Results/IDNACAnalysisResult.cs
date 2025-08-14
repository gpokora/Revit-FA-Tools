using System;
using System.Collections.Generic;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Models.Analysis;

namespace Revit_FA_Tools.Core.Models.Analysis.Results
{
    /// <summary>
    /// Analysis result structure specific to IDNAC (Intelligent Notification Appliance Circuit) systems
    /// </summary>
    public class IDNACAnalysisResult : IAnalysisResult
    {
        public CircuitType CircuitType => CircuitType.IDNAC;
        public DateTime AnalysisTimestamp { get; set; }
        public AnalysisStatus Status { get; set; }
        public List<DeviceSpecification> Devices { get; set; } = new List<DeviceSpecification>();
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        // IDNAC-specific results
        /// <summary>
        /// Gets or sets the electrical calculation results
        /// </summary>
        public ElectricalCalculationResult ElectricalCalculations { get; set; }

        /// <summary>
        /// Gets or sets the circuit requirements breakdown
        /// </summary>
        public List<IDNACCircuitRequirement> CircuitRequirements { get; set; } = new List<IDNACCircuitRequirement>();

        /// <summary>
        /// Gets or sets the T-Taping analysis results
        /// </summary>
        public TTapingAnalysisResult TTapingAnalysis { get; set; }

        /// <summary>
        /// Gets or sets the amplifier requirements for speaker circuits
        /// </summary>
        public AmplifierRequirementResult AmplifierRequirements { get; set; }

        /// <summary>
        /// Gets or sets the voltage drop analysis results
        /// </summary>
        public VoltageDropAnalysisResult VoltageDropAnalysis { get; set; }

        /// <summary>
        /// Gets or sets the power distribution analysis
        /// </summary>
        public PowerDistributionResult PowerDistribution { get; set; }

        // Calculated totals (matching existing IDNACSystemResults for compatibility)
        /// <summary>
        /// Gets or sets the total number of IDNAC circuits needed
        /// </summary>
        public int TotalIdnacsNeeded { get; set; }

        /// <summary>
        /// Gets or sets the total current draw in amperes
        /// </summary>
        public double TotalCurrentDraw { get; set; }

        /// <summary>
        /// Gets or sets the total wattage consumption
        /// </summary>
        public double TotalWattage { get; set; }

        /// <summary>
        /// Gets or sets the total number of speaker devices
        /// </summary>
        public int TotalSpeakers { get; set; }

        /// <summary>
        /// Gets or sets the total number of strobe devices
        /// </summary>
        public int TotalStrobes { get; set; }

        /// <summary>
        /// Gets or sets the total number of combination devices
        /// </summary>
        public int TotalCombos { get; set; }

        /// <summary>
        /// Gets or sets the breakdown by building levels
        /// </summary>
        public List<LevelAnalysis> LevelBreakdown { get; set; } = new List<LevelAnalysis>();

        /// <summary>
        /// Gets or sets additional IDNAC-specific metrics
        /// </summary>
        public IDNACMetrics IDNACMetrics { get; set; } = new IDNACMetrics();
    }

    /// <summary>
    /// Electrical calculation results for IDNAC systems
    /// </summary>
    public class ElectricalCalculationResult
    {
        /// <summary>
        /// Gets or sets the total current consumption in amperes
        /// </summary>
        public double TotalCurrent { get; set; }

        /// <summary>
        /// Gets or sets the total wattage consumption
        /// </summary>
        public double TotalWattage { get; set; }

        /// <summary>
        /// Gets or sets the spare capacity percentage
        /// </summary>
        public double SpareCapacity { get; set; }

        /// <summary>
        /// Gets or sets whether the system exceeds capacity limits
        /// </summary>
        public bool ExceedsCapacity { get; set; }

        /// <summary>
        /// Gets or sets capacity warning messages
        /// </summary>
        public List<string> CapacityWarnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the maximum circuit current allowed
        /// </summary>
        public double MaxCircuitCurrent { get; set; }

        /// <summary>
        /// Gets or sets the power supply requirements
        /// </summary>
        public PowerSupplyRequirements PowerSupplyRequirements { get; set; }
    }

    /// <summary>
    /// Circuit requirement specification for IDNAC
    /// </summary>
    public class IDNACCircuitRequirement
    {
        /// <summary>
        /// Gets or sets the circuit identifier
        /// </summary>
        public string CircuitId { get; set; }

        /// <summary>
        /// Gets or sets the circuit name
        /// </summary>
        public string CircuitName { get; set; }

        /// <summary>
        /// Gets or sets the devices assigned to this circuit
        /// </summary>
        public List<DeviceSpecification> AssignedDevices { get; set; } = new List<DeviceSpecification>();

        /// <summary>
        /// Gets or sets the total current for this circuit
        /// </summary>
        public double CircuitCurrent { get; set; }

        /// <summary>
        /// Gets or sets the total wattage for this circuit
        /// </summary>
        public double CircuitWattage { get; set; }

        /// <summary>
        /// Gets or sets the circuit utilization percentage
        /// </summary>
        public double Utilization { get; set; }

        /// <summary>
        /// Gets or sets the primary building level for this circuit
        /// </summary>
        public string PrimaryLevel { get; set; }

        /// <summary>
        /// Gets or sets whether this circuit requires T-Taping
        /// </summary>
        public bool RequiresTTaping { get; set; }
    }

    /// <summary>
    /// T-Taping analysis results
    /// </summary>
    public class TTapingAnalysisResult
    {
        /// <summary>
        /// Gets or sets whether T-Taping is recommended
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Gets or sets devices that can use T-Taping
        /// </summary>
        public List<DeviceSpecification> TTapCompatibleDevices { get; set; } = new List<DeviceSpecification>();

        /// <summary>
        /// Gets or sets the potential wire savings from T-Taping
        /// </summary>
        public double EstimatedWireSavings { get; set; }

        /// <summary>
        /// Gets or sets T-Taping recommendations by level
        /// </summary>
        public List<TTapingRecommendation> Recommendations { get; set; } = new List<TTapingRecommendation>();
    }

    /// <summary>
    /// Amplifier requirements for speaker circuits
    /// </summary>
    public class AmplifierRequirementResult
    {
        /// <summary>
        /// Gets or sets the number of amplifiers needed
        /// </summary>
        public int AmplifiersNeeded { get; set; }

        /// <summary>
        /// Gets or sets the amplifier type recommendation
        /// </summary>
        public string AmplifierType { get; set; }

        /// <summary>
        /// Gets or sets the total amplifier blocks required
        /// </summary>
        public int AmplifierBlocks { get; set; }

        /// <summary>
        /// Gets or sets the usable amplifier power
        /// </summary>
        public double AmplifierPowerUsable { get; set; }

        /// <summary>
        /// Gets or sets the maximum amplifier power
        /// </summary>
        public double AmplifierPowerMax { get; set; }

        /// <summary>
        /// Gets or sets the amplifier current requirements
        /// </summary>
        public double AmplifierCurrent { get; set; }

        /// <summary>
        /// Gets or sets the total speaker count driving the requirement
        /// </summary>
        public int SpeakerCount { get; set; }
    }

    /// <summary>
    /// Voltage drop analysis results
    /// </summary>
    public class VoltageDropAnalysisResult
    {
        /// <summary>
        /// Gets or sets whether voltage drop is within acceptable limits
        /// </summary>
        public bool IsWithinLimits { get; set; }

        /// <summary>
        /// Gets or sets the maximum voltage drop percentage
        /// </summary>
        public double MaxVoltageDropPercent { get; set; }

        /// <summary>
        /// Gets or sets devices with voltage drop concerns
        /// </summary>
        public List<VoltageDropConcern> VoltageDropConcerns { get; set; } = new List<VoltageDropConcern>();

        /// <summary>
        /// Gets or sets wire gauge recommendations
        /// </summary>
        public List<WireGaugeRecommendation> WireRecommendations { get; set; } = new List<WireGaugeRecommendation>();
    }

    /// <summary>
    /// Power distribution analysis
    /// </summary>
    public class PowerDistributionResult
    {
        /// <summary>
        /// Gets or sets the total power requirements
        /// </summary>
        public double TotalPowerRequired { get; set; }

        /// <summary>
        /// Gets or sets the power distribution by level
        /// </summary>
        public List<LevelPowerDistribution> PowerByLevel { get; set; } = new List<LevelPowerDistribution>();

        /// <summary>
        /// Gets or sets power supply sizing recommendations
        /// </summary>
        public PowerSupplyRequirements PowerSupplyRequirements { get; set; }
    }

    /// <summary>
    /// Analysis breakdown by building level
    /// </summary>
    public class LevelAnalysis
    {
        /// <summary>
        /// Gets or sets the level name
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// Gets or sets the device count on this level
        /// </summary>
        public int DeviceCount { get; set; }

        /// <summary>
        /// Gets or sets the total current on this level
        /// </summary>
        public double TotalCurrent { get; set; }

        /// <summary>
        /// Gets or sets the total wattage on this level
        /// </summary>
        public double TotalWattage { get; set; }

        /// <summary>
        /// Gets or sets the device breakdown by type
        /// </summary>
        public Dictionary<string, int> DeviceTypeBreakdown { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// IDNAC-specific metrics and statistics
    /// </summary>
    public class IDNACMetrics
    {
        /// <summary>
        /// Gets or sets the analysis duration
        /// </summary>
        public TimeSpan AnalysisDuration { get; set; }

        /// <summary>
        /// Gets or sets the device processing rate
        /// </summary>
        public double DevicesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets circuit balancing efficiency
        /// </summary>
        public double CircuitBalancingEfficiency { get; set; }

        /// <summary>
        /// Gets or sets power efficiency rating
        /// </summary>
        public double PowerEfficiencyRating { get; set; }
    }

    /// <summary>
    /// Supporting data structures
    /// </summary>
    public class TTapingRecommendation
    {
        public string LevelName { get; set; }
        public int DeviceCount { get; set; }
        public double EstimatedSavings { get; set; }
        public string Recommendation { get; set; }
    }

    public class VoltageDropConcern
    {
        public DeviceSpecification Device { get; set; }
        public double VoltageDropPercent { get; set; }
        public string Concern { get; set; }
        public string Recommendation { get; set; }
    }

    public class WireGaugeRecommendation
    {
        public string CircuitId { get; set; }
        public string RecommendedGauge { get; set; }
        public double MaxDistance { get; set; }
        public string Reasoning { get; set; }
    }

    public class LevelPowerDistribution
    {
        public string LevelName { get; set; }
        public double PowerRequired { get; set; }
        public double CurrentRequired { get; set; }
        public int CircuitsRequired { get; set; }
    }

    public class PowerSupplyRequirements
    {
        public double TotalPowerRequired { get; set; }
        public double RecommendedPowerSupplySize { get; set; }
        public string PowerSupplyType { get; set; }
        public bool RequiresBackup { get; set; }
        public List<string> Requirements { get; set; } = new List<string>();
    }
}