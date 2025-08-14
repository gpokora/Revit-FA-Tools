using System;
using System.Collections.Generic;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Models.Analysis;

namespace Revit_FA_Tools.Core.Models.Analysis.Results
{
    /// <summary>
    /// Analysis result structure specific to IDNET (Intelligent Detection Network) systems
    /// </summary>
    public class IDNETAnalysisResult : IAnalysisResult
    {
        public CircuitType CircuitType => CircuitType.IDNET;
        public DateTime AnalysisTimestamp { get; set; }
        public AnalysisStatus Status { get; set; }
        public List<DeviceSpecification> Devices { get; set; } = new List<DeviceSpecification>();
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        // IDNET-specific results
        /// <summary>
        /// Gets or sets the network segments analysis
        /// </summary>
        public List<NetworkSegment> NetworkSegments { get; set; } = new List<NetworkSegment>();

        /// <summary>
        /// Gets or sets the loop topology analysis
        /// </summary>
        public LoopTopologyAnalysis LoopTopology { get; set; }

        /// <summary>
        /// Gets or sets the address assignment results
        /// </summary>
        public AddressAssignmentResult AddressAssignment { get; set; }

        /// <summary>
        /// Gets or sets the zone coverage analysis
        /// </summary>
        public ZoneCoverageAnalysis ZoneCoverage { get; set; }

        /// <summary>
        /// Gets or sets the detection capacity analysis
        /// </summary>
        public DetectionCapacityResult DetectionCapacity { get; set; }

        /// <summary>
        /// Gets or sets the supervision requirements
        /// </summary>
        public SupervisionRequirements SupervisionRequirements { get; set; }

        // Calculated totals
        /// <summary>
        /// Gets or sets the total number of detection devices
        /// </summary>
        public int TotalDetectionDevices { get; set; }

        /// <summary>
        /// Gets or sets the total number of input modules
        /// </summary>
        public int TotalInputModules { get; set; }

        /// <summary>
        /// Gets or sets the total number of output modules
        /// </summary>
        public int TotalOutputModules { get; set; }

        /// <summary>
        /// Gets or sets the total power consumption
        /// </summary>
        public double TotalPowerConsumption { get; set; }

        /// <summary>
        /// Gets or sets the number of network segments required
        /// </summary>
        public int NetworkSegmentsRequired { get; set; }

        /// <summary>
        /// Gets or sets IDNET-specific metrics
        /// </summary>
        public IDNETMetrics IDNETMetrics { get; set; } = new IDNETMetrics();
    }

    /// <summary>
    /// Network segment information for IDNET systems
    /// </summary>
    public class NetworkSegment
    {
        /// <summary>
        /// Gets or sets the segment identifier
        /// </summary>
        public int SegmentId { get; set; }

        /// <summary>
        /// Gets or sets the segment name
        /// </summary>
        public string SegmentName { get; set; }

        /// <summary>
        /// Gets or sets the devices on this segment
        /// </summary>
        public List<DeviceSpecification> Devices { get; set; } = new List<DeviceSpecification>();

        /// <summary>
        /// Gets or sets the number of addresses used
        /// </summary>
        public int AddressesUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of addresses available
        /// </summary>
        public int AddressesAvailable { get; set; }

        /// <summary>
        /// Gets or sets the power consumption for this segment
        /// </summary>
        public double PowerConsumption { get; set; }

        /// <summary>
        /// Gets or sets the segment utilization percentage
        /// </summary>
        public double Utilization => AddressesAvailable > 0 ? (double)AddressesUsed / AddressesAvailable * 100 : 0;

        /// <summary>
        /// Gets or sets whether this segment requires isolation
        /// </summary>
        public bool RequiresIsolation { get; set; }

        /// <summary>
        /// Gets or sets the segment topology type
        /// </summary>
        public SegmentTopology Topology { get; set; }
    }

    /// <summary>
    /// Loop topology analysis for IDNET systems
    /// </summary>
    public class LoopTopologyAnalysis
    {
        /// <summary>
        /// Gets or sets the detected loops in the system
        /// </summary>
        public List<DetectionLoop> DetectionLoops { get; set; } = new List<DetectionLoop>();

        /// <summary>
        /// Gets or sets whether the topology is valid
        /// </summary>
        public bool IsTopologyValid { get; set; }

        /// <summary>
        /// Gets or sets topology validation messages
        /// </summary>
        public List<string> TopologyValidation { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets recommended topology improvements
        /// </summary>
        public List<TopologyRecommendation> Recommendations { get; set; } = new List<TopologyRecommendation>();

        /// <summary>
        /// Gets or sets isolation module requirements
        /// </summary>
        public List<IsolationRequirement> IsolationRequirements { get; set; } = new List<IsolationRequirement>();
    }

    /// <summary>
    /// Address assignment results for IDNET devices
    /// </summary>
    public class AddressAssignmentResult
    {
        /// <summary>
        /// Gets or sets whether all devices have valid addresses
        /// </summary>
        public bool AllDevicesAddressed { get; set; }

        /// <summary>
        /// Gets or sets the address conflicts found
        /// </summary>
        public List<AddressConflict> Conflicts { get; set; } = new List<AddressConflict>();

        /// <summary>
        /// Gets or sets the address utilization by loop
        /// </summary>
        public List<LoopAddressUtilization> LoopUtilization { get; set; } = new List<LoopAddressUtilization>();

        /// <summary>
        /// Gets or sets recommended address assignments
        /// </summary>
        public List<AddressRecommendation> AddressRecommendations { get; set; } = new List<AddressRecommendation>();

        /// <summary>
        /// Gets or sets the address range summary
        /// </summary>
        public AddressRangeSummary AddressRange { get; set; }
    }

    /// <summary>
    /// Zone coverage analysis for detection systems
    /// </summary>
    public class ZoneCoverageAnalysis
    {
        /// <summary>
        /// Gets or sets the zones analyzed
        /// </summary>
        public List<ZoneCoverage> Zones { get; set; } = new List<ZoneCoverage>();

        /// <summary>
        /// Gets or sets whether all zones have adequate coverage
        /// </summary>
        public bool AllZonesCovered { get; set; }

        /// <summary>
        /// Gets or sets coverage gaps identified
        /// </summary>
        public List<CoverageGap> CoverageGaps { get; set; } = new List<CoverageGap>();

        /// <summary>
        /// Gets or sets overall coverage percentage
        /// </summary>
        public double OverallCoveragePercent { get; set; }

        /// <summary>
        /// Gets or sets coverage recommendations
        /// </summary>
        public List<CoverageRecommendation> Recommendations { get; set; } = new List<CoverageRecommendation>();
    }

    /// <summary>
    /// Detection capacity analysis results
    /// </summary>
    public class DetectionCapacityResult
    {
        /// <summary>
        /// Gets or sets the total detection capacity
        /// </summary>
        public int TotalDetectionCapacity { get; set; }

        /// <summary>
        /// Gets or sets the current capacity utilization
        /// </summary>
        public double CapacityUtilization { get; set; }

        /// <summary>
        /// Gets or sets capacity by device type
        /// </summary>
        public Dictionary<string, int> CapacityByType { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets or sets whether capacity is sufficient
        /// </summary>
        public bool IsSufficientCapacity { get; set; }

        /// <summary>
        /// Gets or sets capacity expansion recommendations
        /// </summary>
        public List<CapacityRecommendation> ExpansionRecommendations { get; set; } = new List<CapacityRecommendation>();
    }

    /// <summary>
    /// Supervision requirements for IDNET systems
    /// </summary>
    public class SupervisionRequirements
    {
        /// <summary>
        /// Gets or sets the supervision method
        /// </summary>
        public string SupervisionMethod { get; set; }

        /// <summary>
        /// Gets or sets supervision modules required
        /// </summary>
        public List<SupervisionModule> RequiredModules { get; set; } = new List<SupervisionModule>();

        /// <summary>
        /// Gets or sets end-of-line resistor requirements
        /// </summary>
        public List<EOLRequirement> EOLRequirements { get; set; } = new List<EOLRequirement>();

        /// <summary>
        /// Gets or sets whether supervision is adequate
        /// </summary>
        public bool IsSupervisionAdequate { get; set; }

        /// <summary>
        /// Gets or sets supervision validation messages
        /// </summary>
        public List<string> ValidationMessages { get; set; } = new List<string>();
    }

    /// <summary>
    /// IDNET-specific metrics and statistics
    /// </summary>
    public class IDNETMetrics
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
        /// Gets or sets the network efficiency rating
        /// </summary>
        public double NetworkEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the topology complexity score
        /// </summary>
        public double TopologyComplexity { get; set; }

        /// <summary>
        /// Gets or sets the address optimization score
        /// </summary>
        public double AddressOptimization { get; set; }
    }

    // Supporting data structures
    public enum SegmentTopology
    {
        Linear,
        Star,
        Ring,
        Tree,
        Mesh
    }

    public class DetectionLoop
    {
        public int LoopId { get; set; }
        public string LoopName { get; set; }
        public List<DeviceSpecification> Devices { get; set; } = new List<DeviceSpecification>();
        public SegmentTopology Topology { get; set; }
        public bool IsSupervised { get; set; }
        public double LoopResistance { get; set; }
    }

    public class TopologyRecommendation
    {
        public string Recommendation { get; set; }
        public string Reasoning { get; set; }
        public string Priority { get; set; }
        public List<DeviceSpecification> AffectedDevices { get; set; } = new List<DeviceSpecification>();
    }

    public class IsolationRequirement
    {
        public string Location { get; set; }
        public string IsolationType { get; set; }
        public string Reasoning { get; set; }
        public List<DeviceSpecification> ProtectedDevices { get; set; } = new List<DeviceSpecification>();
    }

    public class AddressConflict
    {
        public int Address { get; set; }
        public List<DeviceSpecification> ConflictingDevices { get; set; } = new List<DeviceSpecification>();
        public string ConflictDescription { get; set; }
        public string Resolution { get; set; }
    }

    public class LoopAddressUtilization
    {
        public int LoopId { get; set; }
        public string LoopName { get; set; }
        public int AddressesUsed { get; set; }
        public int TotalAddresses { get; set; }
        public double UtilizationPercent { get; set; }
    }

    public class AddressRecommendation
    {
        public DeviceSpecification Device { get; set; }
        public int RecommendedAddress { get; set; }
        public string Reasoning { get; set; }
    }

    public class AddressRangeSummary
    {
        public int LowestAddress { get; set; }
        public int HighestAddress { get; set; }
        public int TotalAddressesUsed { get; set; }
        public int TotalAddressesAvailable { get; set; }
    }

    public class ZoneCoverage
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public List<DeviceSpecification> DetectionDevices { get; set; } = new List<DeviceSpecification>();
        public double CoveragePercent { get; set; }
        public bool MeetsRequirements { get; set; }
        public string CoverageType { get; set; }
    }

    public class CoverageGap
    {
        public string Location { get; set; }
        public string GapType { get; set; }
        public double GapSize { get; set; }
        public string Recommendation { get; set; }
    }

    public class CoverageRecommendation
    {
        public string ZoneId { get; set; }
        public string Recommendation { get; set; }
        public string DeviceType { get; set; }
        public string Location { get; set; }
        public string Reasoning { get; set; }
    }

    public class CapacityRecommendation
    {
        public string RecommendationType { get; set; }
        public string Description { get; set; }
        public int AdditionalCapacityNeeded { get; set; }
        public string Priority { get; set; }
    }

    public class SupervisionModule
    {
        public string ModuleType { get; set; }
        public string Location { get; set; }
        public List<DeviceSpecification> SupervisedDevices { get; set; } = new List<DeviceSpecification>();
        public string SupervisionMethod { get; set; }
    }

    public class EOLRequirement
    {
        public string CircuitId { get; set; }
        public double ResistanceValue { get; set; }
        public string ResistorType { get; set; }
        public string Location { get; set; }
    }
}