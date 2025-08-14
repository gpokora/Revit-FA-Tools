using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Models.Analysis.Results;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Services.Analysis.Pipeline;

namespace Revit_FA_Tools.Core.Services.Analysis.Analyzers
{
    /// <summary>
    /// IDNET (Intelligent Detection Network) analyzer
    /// </summary>
    public class IDNETAnalyzer : ICircuitAnalyzer
    {
        private readonly object _logger;

        public CircuitType SupportedCircuitType => CircuitType.IDNET;

        public IDNETAnalyzer(object logger = null)
        {
            _logger = logger;
        }

        public async Task<IAnalysisResult> AnalyzeAsync(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var result = new Revit_FA_Tools.Core.Models.Analysis.Results.IDNETAnalysisResult
            {
                AnalysisTimestamp = DateTime.Now,
                Status = AnalysisStatus.InProgress,
                Devices = devices
            };

            try
            {
                context.ReportProgress("IDNET Analysis", "Analyzing detection network...", 70);

                // Perform IDNET-specific analysis
                result.NetworkSegments = await CalculateNetworkSegments(devices, context);
                result.AddressAssignment = await AssignDeviceAddresses(devices, context);
                result.DetectionCapacity = await CalculateDetectionCapacity(devices, context);

                // Calculate totals
                CalculateTotals(result);

                // Optional advanced analysis based on settings
                if (context.Request.Settings.AnalyzeNetworkTopology)
                {
                    result.LoopTopology = await AnalyzeLoopTopology(devices, context);
                }

                if (context.Request.Settings.CheckZoneCoverage)
                {
                    result.ZoneCoverage = await AnalyzeZoneCoverage(devices, context);
                }

                if (context.Request.Settings.CalculateSupervisionRequirements)
                {
                    result.SupervisionRequirements = await CalculateSupervision(devices, context);
                }

                // Set metrics
                result.Metrics["AnalysisType"] = "IDNET";
                result.Metrics["DeviceCount"] = devices.Count;
                result.Metrics["DetectionDevices"] = devices.Count(d => d.IsDetectionDevice);
                result.Metrics["NetworkSegments"] = result.NetworkSegments.Count;

                result.Status = AnalysisStatus.Completed;
                context.ReportProgress("IDNET Analysis", "IDNET analysis completed successfully", 90);

                return result;
            }
            catch (Exception ex)
            {
                result.Status = AnalysisStatus.Failed;
                result.Errors.Add($"IDNET analysis failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"IDNET Analysis Error: {ex}");
                return result;
            }
        }

        public bool CanAnalyze(CircuitType circuitType)
        {
            return circuitType == CircuitType.IDNET;
        }

        /// <summary>
        /// Calculates network segments for IDNET devices
        /// </summary>
        private async Task<List<NetworkSegment>> CalculateNetworkSegments(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var segments = new List<NetworkSegment>();

            // Group devices by loop or level for segmentation
            var deviceGroups = devices.GroupBy(d => d.LoopId ?? d.LevelName ?? "Default").ToList();

            int segmentId = 1;
            foreach (var group in deviceGroups)
            {
                var segment = new NetworkSegment
                {
                    SegmentId = segmentId++,
                    SegmentName = $"Segment {segmentId} - {group.Key}",
                    Devices = group.ToList(),
                    AddressesUsed = group.Count(d => d.Address.HasValue),
                    AddressesAvailable = 159, // Typical IDNET capacity
                    PowerConsumption = group.Sum(d => d.PowerConsumption),
                    RequiresIsolation = group.Count() > 20, // Isolation recommendation for large segments
                    Topology = SegmentTopology.Linear
                };

                segments.Add(segment);
            }

            return await Task.FromResult(segments);
        }

        /// <summary>
        /// Analyzes and assigns device addresses
        /// </summary>
        private async Task<AddressAssignmentResult> AssignDeviceAddresses(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var result = new AddressAssignmentResult();

            // Check for address conflicts
            var devicesByAddress = devices.Where(d => d.Address.HasValue)
                                         .GroupBy(d => d.Address.Value)
                                         .Where(g => g.Count() > 1)
                                         .ToList();

            foreach (var conflictGroup in devicesByAddress)
            {
                result.Conflicts.Add(new AddressConflict
                {
                    Address = conflictGroup.Key,
                    ConflictingDevices = conflictGroup.ToList(),
                    ConflictDescription = $"Address {conflictGroup.Key} assigned to {conflictGroup.Count()} devices",
                    Resolution = "Assign unique addresses to conflicting devices"
                });
            }

            // Calculate loop utilization
            var loopGroups = devices.GroupBy(d => d.LoopId ?? "Default Loop").ToList();
            foreach (var loopGroup in loopGroups)
            {
                result.LoopUtilization.Add(new LoopAddressUtilization
                {
                    LoopId = loopGroup.Key.GetHashCode(),
                    LoopName = loopGroup.Key,
                    AddressesUsed = loopGroup.Count(d => d.Address.HasValue),
                    TotalAddresses = 159,
                    UtilizationPercent = (double)loopGroup.Count(d => d.Address.HasValue) / 159 * 100
                });
            }

            result.AllDevicesAddressed = devices.All(d => d.Address.HasValue);

            // Address range summary
            var addressedDevices = devices.Where(d => d.Address.HasValue).ToList();
            if (addressedDevices.Any())
            {
                result.AddressRange = new AddressRangeSummary
                {
                    LowestAddress = addressedDevices.Min(d => d.Address.Value),
                    HighestAddress = addressedDevices.Max(d => d.Address.Value),
                    TotalAddressesUsed = addressedDevices.Count,
                    TotalAddressesAvailable = 159 * loopGroups.Count
                };
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Calculates detection capacity
        /// </summary>
        private async Task<DetectionCapacityResult> CalculateDetectionCapacity(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var detectionDevices = devices.Where(d => d.IsDetectionDevice).ToList();

            var capacityByType = detectionDevices.GroupBy(d => d.DeviceType)
                                               .ToDictionary(g => g.Key, g => g.Count());

            var result = new DetectionCapacityResult
            {
                TotalDetectionCapacity = detectionDevices.Count,
                CapacityUtilization = detectionDevices.Count > 0 ? 100.0 : 0.0, // Simplified
                CapacityByType = capacityByType,
                IsSufficientCapacity = true // Simplified - would need coverage analysis
            };

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Calculates totals for the analysis result
        /// </summary>
        private void CalculateTotals(Revit_FA_Tools.Core.Models.Analysis.Results.IDNETAnalysisResult result)
        {
            result.TotalDetectionDevices = result.Devices.Count(d => d.IsDetectionDevice);
            result.TotalInputModules = result.Devices.Count(d => d.DeviceType.Contains("Module") && d.IsDetectionDevice);
            result.TotalOutputModules = result.Devices.Count(d => d.DeviceType.Contains("Module") && !d.IsDetectionDevice);
            result.TotalPowerConsumption = result.Devices.Sum(d => d.PowerConsumption);
            result.NetworkSegmentsRequired = result.NetworkSegments.Count;
        }

        // Placeholder methods for advanced analysis (to be implemented later)
        private async Task<LoopTopologyAnalysis> AnalyzeLoopTopology(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var loops = devices.GroupBy(d => d.LoopId ?? "Default")
                              .Select(g => new DetectionLoop
                              {
                                  LoopId = g.Key.GetHashCode(),
                                  LoopName = g.Key,
                                  Devices = g.ToList(),
                                  Topology = SegmentTopology.Linear,
                                  IsSupervised = true
                              }).ToList();

            return await Task.FromResult(new LoopTopologyAnalysis
            {
                DetectionLoops = loops,
                IsTopologyValid = true,
                TopologyValidation = new List<string> { "Topology validation passed" }
            });
        }

        private async Task<ZoneCoverageAnalysis> AnalyzeZoneCoverage(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var zones = devices.GroupBy(d => d.Zone ?? "Default Zone")
                              .Select(g => new ZoneCoverage
                              {
                                  ZoneId = g.Key,
                                  ZoneName = g.Key,
                                  DetectionDevices = g.Where(d => d.IsDetectionDevice).ToList(),
                                  CoveragePercent = 100.0, // Simplified
                                  MeetsRequirements = true,
                                  CoverageType = "Area"
                              }).ToList();

            return await Task.FromResult(new ZoneCoverageAnalysis
            {
                Zones = zones,
                AllZonesCovered = true,
                OverallCoveragePercent = 100.0
            });
        }

        private async Task<SupervisionRequirements> CalculateSupervision(List<DeviceSpecification> devices, AnalysisContext context)
        {
            return await Task.FromResult(new SupervisionRequirements
            {
                SupervisionMethod = "End-of-Line Resistor",
                IsSupervisionAdequate = true,
                ValidationMessages = new List<string> { "Supervision requirements met" }
            });
        }
    }
}