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
    /// IDNAC (Intelligent Notification Appliance Circuit) analyzer
    /// </summary>
    public class IDNACAnalyzer : ICircuitAnalyzer
    {
        private readonly object _logger;

        public CircuitType SupportedCircuitType => CircuitType.IDNAC;

        public IDNACAnalyzer(object logger = null)
        {
            _logger = logger;
        }

        public async Task<IAnalysisResult> AnalyzeAsync(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var result = new Revit_FA_Tools.Core.Models.Analysis.Results.IDNACAnalysisResult
            {
                AnalysisTimestamp = DateTime.Now,
                Status = AnalysisStatus.InProgress,
                Devices = devices
            };

            try
            {
                context.ReportProgress("IDNAC Analysis", "Analyzing notification circuits...", 70);

                // Perform IDNAC-specific analysis
                result.ElectricalCalculations = await CalculateElectricalRequirements(devices, context);
                result.CircuitRequirements = await CalculateCircuitRequirements(devices, context);
                result.LevelBreakdown = await AnalyzeLevelBreakdown(devices, context);

                // Calculate totals
                CalculateTotals(result);

                // Optional advanced analysis based on settings
                if (context.Request.Settings.IncludeTTapingAnalysis)
                {
                    result.TTapingAnalysis = await AnalyzeTTaping(devices, context);
                }

                if (context.Request.Settings.CalculateAmplifierRequirements)
                {
                    result.AmplifierRequirements = await CalculateAmplifierRequirements(devices, context);
                }

                if (context.Request.Settings.CalculateVoltageDrops)
                {
                    result.VoltageDropAnalysis = await CalculateVoltageDrops(devices, context);
                }

                result.PowerDistribution = await AnalyzePowerDistribution(devices, context);

                // Set metrics
                result.Metrics["AnalysisType"] = "IDNAC";
                result.Metrics["DeviceCount"] = devices.Count;
                result.Metrics["NotificationDevices"] = devices.Count(d => d.IsNotificationDevice);
                result.Metrics["TotalCurrent"] = result.TotalCurrentDraw;
                result.Metrics["TotalWattage"] = result.TotalWattage;

                result.Status = AnalysisStatus.Completed;
                context.ReportProgress("IDNAC Analysis", "IDNAC analysis completed successfully", 90);

                return result;
            }
            catch (Exception ex)
            {
                result.Status = AnalysisStatus.Failed;
                result.Errors.Add($"IDNAC analysis failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"IDNAC Analysis Error: {ex}");
                return result;
            }
        }

        public bool CanAnalyze(CircuitType circuitType)
        {
            return circuitType == CircuitType.IDNAC;
        }

        /// <summary>
        /// Calculates electrical requirements for IDNAC devices
        /// </summary>
        private async Task<ElectricalCalculationResult> CalculateElectricalRequirements(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var result = new ElectricalCalculationResult();

            // Calculate total current and wattage
            result.TotalCurrent = devices.Sum(d => d.CurrentDraw);
            result.TotalWattage = devices.Sum(d => d.PowerConsumption);

            // Get spare capacity from settings
            result.SpareCapacity = context.Request.Settings.SpareCapacityPercent;

            // Check against typical IDNAC limits (these could be configurable)
            result.MaxCircuitCurrent = 2.0; // Typical 2A limit per circuit
            result.ExceedsCapacity = result.TotalCurrent > result.MaxCircuitCurrent;

            if (result.ExceedsCapacity)
            {
                result.CapacityWarnings.Add($"Total current {result.TotalCurrent:F2}A exceeds circuit limit of {result.MaxCircuitCurrent}A");
            }

            // Calculate power supply requirements
            result.PowerSupplyRequirements = new PowerSupplyRequirements
            {
                TotalPowerRequired = result.TotalWattage,
                RecommendedPowerSupplySize = result.TotalWattage * (1 + result.SpareCapacity / 100),
                PowerSupplyType = "24VDC",
                RequiresBackup = true
            };

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Calculates circuit requirements and organization
        /// </summary>
        private async Task<List<IDNACCircuitRequirement>> CalculateCircuitRequirements(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var circuits = new List<IDNACCircuitRequirement>();
            var circuitCapacity = 2.0; // 2A per circuit

            // Group devices by level for circuit organization
            var devicesByLevel = devices.GroupBy(d => d.LevelName ?? "Unknown").ToList();

            int circuitCounter = 1;
            foreach (var levelGroup in devicesByLevel)
            {
                var levelDevices = levelGroup.ToList();
                var currentCurrent = 0.0;
                var currentDevices = new List<DeviceSpecification>();

                foreach (var device in levelDevices)
                {
                    if (currentCurrent + device.CurrentDraw <= circuitCapacity)
                    {
                        // Add to current circuit
                        currentDevices.Add(device);
                        currentCurrent += device.CurrentDraw;
                    }
                    else
                    {
                        // Create new circuit with current devices
                        if (currentDevices.Any())
                        {
                            circuits.Add(CreateCircuitRequirement(circuitCounter++, currentDevices, levelGroup.Key));
                        }

                        // Start new circuit with current device
                        currentDevices = new List<DeviceSpecification> { device };
                        currentCurrent = device.CurrentDraw;
                    }
                }

                // Add final circuit if any devices remain
                if (currentDevices.Any())
                {
                    circuits.Add(CreateCircuitRequirement(circuitCounter++, currentDevices, levelGroup.Key));
                }
            }

            return await Task.FromResult(circuits);
        }

        /// <summary>
        /// Creates a circuit requirement from a list of devices
        /// </summary>
        private IDNACCircuitRequirement CreateCircuitRequirement(int circuitId, List<DeviceSpecification> devices, string level)
        {
            return new IDNACCircuitRequirement
            {
                CircuitId = $"IDNAC-{circuitId:D2}",
                CircuitName = $"IDNAC Circuit {circuitId} - {level}",
                AssignedDevices = devices,
                CircuitCurrent = devices.Sum(d => d.CurrentDraw),
                CircuitWattage = devices.Sum(d => d.PowerConsumption),
                Utilization = devices.Sum(d => d.CurrentDraw) / 2.0 * 100, // Assuming 2A capacity
                PrimaryLevel = level,
                RequiresTTaping = devices.Any(d => d.IsTTapCompatible)
            };
        }

        /// <summary>
        /// Analyzes device distribution by building level
        /// </summary>
        private async Task<List<LevelAnalysis>> AnalyzeLevelBreakdown(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var levels = devices.GroupBy(d => d.LevelName ?? "Unknown")
                               .Select(g => new LevelAnalysis
                               {
                                   LevelName = g.Key,
                                   DeviceCount = g.Count(),
                                   TotalCurrent = g.Sum(d => d.CurrentDraw),
                                   TotalWattage = g.Sum(d => d.PowerConsumption),
                                   DeviceTypeBreakdown = g.GroupBy(d => d.DeviceType)
                                                        .ToDictionary(dt => dt.Key, dt => dt.Count())
                               }).ToList();

            return await Task.FromResult(levels);
        }

        /// <summary>
        /// Calculates totals for the analysis result
        /// </summary>
        private void CalculateTotals(Revit_FA_Tools.Core.Models.Analysis.Results.IDNACAnalysisResult result)
        {
            result.TotalCurrentDraw = result.Devices.Sum(d => d.CurrentDraw);
            result.TotalWattage = result.Devices.Sum(d => d.PowerConsumption);
            result.TotalIdnacsNeeded = result.CircuitRequirements.Count;
            
            result.TotalSpeakers = result.Devices.Count(d => d.HasSpeaker && !d.HasStrobe);
            result.TotalStrobes = result.Devices.Count(d => d.HasStrobe && !d.HasSpeaker);
            result.TotalCombos = result.Devices.Count(d => d.HasStrobe && d.HasSpeaker);
        }

        // Placeholder methods for advanced analysis (to be implemented later)
        private async Task<TTapingAnalysisResult> AnalyzeTTaping(List<DeviceSpecification> devices, AnalysisContext context)
        {
            return await Task.FromResult(new TTapingAnalysisResult
            {
                IsRecommended = devices.Any(d => d.IsTTapCompatible),
                TTapCompatibleDevices = devices.Where(d => d.IsTTapCompatible).ToList()
            });
        }

        private async Task<AmplifierRequirementResult> CalculateAmplifierRequirements(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var speakers = devices.Where(d => d.HasSpeaker).ToList();
            
            return await Task.FromResult(new AmplifierRequirementResult
            {
                SpeakerCount = speakers.Count,
                AmplifiersNeeded = speakers.Count > 0 ? 1 : 0,
                AmplifierType = speakers.Count > 50 ? "Flex-100" : "Flex-35"
            });
        }

        private async Task<VoltageDropAnalysisResult> CalculateVoltageDrops(List<DeviceSpecification> devices, AnalysisContext context)
        {
            return await Task.FromResult(new VoltageDropAnalysisResult
            {
                IsWithinLimits = true,
                MaxVoltageDropPercent = 5.0 // Placeholder
            });
        }

        private async Task<PowerDistributionResult> AnalyzePowerDistribution(List<DeviceSpecification> devices, AnalysisContext context)
        {
            var powerByLevel = devices.GroupBy(d => d.LevelName ?? "Unknown")
                                    .Select(g => new LevelPowerDistribution
                                    {
                                        LevelName = g.Key,
                                        PowerRequired = g.Sum(d => d.PowerConsumption),
                                        CurrentRequired = g.Sum(d => d.CurrentDraw),
                                        CircuitsRequired = (int)Math.Ceiling(g.Sum(d => d.CurrentDraw) / 2.0)
                                    }).ToList();

            return await Task.FromResult(new PowerDistributionResult
            {
                TotalPowerRequired = devices.Sum(d => d.PowerConsumption),
                PowerByLevel = powerByLevel,
                PowerSupplyRequirements = new PowerSupplyRequirements
                {
                    TotalPowerRequired = devices.Sum(d => d.PowerConsumption),
                    PowerSupplyType = "24VDC",
                    RequiresBackup = true
                }
            });
        }
    }
}