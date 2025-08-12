using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Revit_FA_Tools.Core.Models.Analysis;

namespace Revit_FA_Tools
{
    public class PanelPlacementAnalyzer
    {
        // ES-PS Power Supply Specifications (from 4100ES Table 13) - now configurable
        private static double ES_PS_TOTAL_DC_OUTPUT => ConfigurationService.Current.PowerSupply.TotalDCOutput;
        private static double ES_PS_TOTAL_DC_OUTPUT_WITH_FAN => ConfigurationService.Current.PowerSupply.TotalDCOutputWithFan;
        private static double ES_PS_MAX_OUTPUT => ConfigurationService.Current.PowerSupply.MaxOutput;

        // IDNAC circuits per power supply - now configurable
        private static int IDNAC_CIRCUITS_PER_ES_PS => ConfigurationService.Current.PowerSupply.IDNACCircuitsPerPS;

        // Available blocks after accounting for Audio Controller (Blocks A+B) per fire alarm specs - now configurable
        private static int AVAILABLE_BLOCKS_SINGLE_BAY => ConfigurationService.Current.PowerSupply.AvailableBlocksSingleBay;
        private static int AVAILABLE_BLOCKS_TWO_BAY => ConfigurationService.Current.PowerSupply.AvailableBlocksTwoBay;
        private static int AVAILABLE_BLOCKS_THREE_BAY => ConfigurationService.Current.PowerSupply.AvailableBlocksThreeBay;

        // Amplifier specifications - now configurable
        private static double AMPLIFIER_CURRENT_FLEX100 => ConfigurationService.Current.Amplifiers.Flex100Current;

        public List<PanelPlacementRecommendation> RecommendPanelPlacement(
            IDNACSystemResults idnacResults,
            AmplifierRequirements amplifierRequirements,
            ElectricalResults electricalResults,
            IDNETSystemResults idnetResults = null)
        {
            var recommendations = new List<PanelPlacementRecommendation>();

            // FIXED: Add validation and error handling
            if (idnacResults == null)
            {
                throw new ArgumentException("IDNAC results cannot be null");
            }

            // Get basic system totals
            var totalIdnacs = idnacResults.TotalIdnacsNeeded;
            var totalSpeakers = amplifierRequirements?.SpeakerCount ?? 0;
            var totalAmplifiers = amplifierRequirements?.AmplifiersNeeded ?? 0;
            var amplifierBlocks = amplifierRequirements?.AmplifierBlocks ?? 0;
            var amplifierCurrent = amplifierRequirements?.AmplifierCurrent ?? 0;

            // ENHANCED: Add IDNET system analysis for combined fire alarm system design
            var totalIdnetDevices = idnetResults?.TotalDevices ?? 0;
            var idnetNetworkSegments = idnetResults?.NetworkSegments?.Count ?? 0;
            var totalIdnetCurrent = idnetResults?.TotalPowerConsumption ?? 0; // mA
            var totalSystemAddresses = totalIdnacs * 127 + totalIdnetDevices; // Max addresses needed

            // ENHANCED: Combined system debug output
            System.Diagnostics.Debug.WriteLine($"Combined Fire Alarm System Analysis Starting:");
            System.Diagnostics.Debug.WriteLine($"  IDNAC (Notification): {totalIdnacs} circuits");
            System.Diagnostics.Debug.WriteLine($"  IDNET (Detection): {totalIdnetDevices} devices, {idnetNetworkSegments} segments");
            System.Diagnostics.Debug.WriteLine($"  Speakers: {totalSpeakers} devices, {totalAmplifiers} amplifiers");
            System.Diagnostics.Debug.WriteLine($"  Amplifier Load: {amplifierBlocks} blocks, {amplifierCurrent:F2}A");
            System.Diagnostics.Debug.WriteLine($"  Total System Addresses: {totalSystemAddresses}");

            // ENHANCED: Calculate combined system panel requirements
            var powerSuppliesNeeded = (int)Math.Ceiling((double)totalIdnacs / IDNAC_CIRCUITS_PER_ES_PS);
            var amplifierPowerSuppliesNeeded = amplifierCurrent > 0 ?
                (int)Math.Ceiling(amplifierCurrent / ES_PS_TOTAL_DC_OUTPUT_WITH_FAN) : 0;
            
            // IDNET supervisory current requirements (convert mA to A)
            var idnetCurrentAmps = totalIdnetCurrent / 1000.0;
            var idnetPowerSuppliesNeeded = idnetCurrentAmps > 0 ?
                (int)Math.Ceiling(idnetCurrentAmps / ES_PS_TOTAL_DC_OUTPUT_WITH_FAN) : 0;
            
            // Combined power supply requirement
            var totalPowerSuppliesNeeded = Math.Max(Math.Max(powerSuppliesNeeded, amplifierPowerSuppliesNeeded), idnetPowerSuppliesNeeded);

            // ENHANCED: Calculate cabinet requirements for combined fire alarm system
            int minCabinetsForAmplifiers = 0;
            int minCabinetsForIDNACs = 0;
            int minCabinetsForIDNET = 0;

            if (amplifierBlocks > 0)
            {
                // Calculate minimum cabinets needed for amplifiers
                minCabinetsForAmplifiers = (int)Math.Ceiling((double)amplifierBlocks / AVAILABLE_BLOCKS_THREE_BAY);
            }

            if (totalIdnacs > 0)
            {
                // CORRECTED: Calculate minimum cabinets for IDNACs based on fire alarm specs
                // Each ES-PS supports 3 IDNAC circuits, max ES-PS per cabinet varies by type
                // Conservative assumption: Max 3 ES-PS per cabinet = 9 IDNACs per cabinet
                var maxIdnacsPerCabinet = 3 * IDNAC_CIRCUITS_PER_ES_PS; // 9 IDNACs max per cabinet
                minCabinetsForIDNACs = (int)Math.Ceiling((double)totalIdnacs / maxIdnacsPerCabinet);
            }

            if (totalIdnetDevices > 0)
            {
                // CORRECTED: Calculate minimum panels for IDNET based on fire alarm specs
                // 4100ES panel supports multiple IDNET channels, each with 250 device limit
                // Conservative assumption: 2 IDNET channels per panel (geographic/wire run limited)
                var maxDevicesPerIdnetChannel = 250; // Per fire alarm specification
                var maxIdnetChannelsPerPanel = 2; // Conservative for geographic distribution
                var maxIdnetPerPanel = maxDevicesPerIdnetChannel * maxIdnetChannelsPerPanel; // 500 devices
                minCabinetsForIDNET = (int)Math.Ceiling((double)totalIdnetDevices / maxIdnetPerPanel);
                
                // Network segments may require separate panels for geographic distribution
                // Wire run limits (12,500 ft per channel) often drive panel placement
                if (idnetNetworkSegments > minCabinetsForIDNET)
                {
                    minCabinetsForIDNET = idnetNetworkSegments;
                }
            }

            var minPanelsNeeded = Math.Max(Math.Max(Math.Max(minCabinetsForAmplifiers, minCabinetsForIDNACs), minCabinetsForIDNET), 1);

            System.Diagnostics.Debug.WriteLine($"Combined System Panel Requirements Analysis:");
            System.Diagnostics.Debug.WriteLine($"  Power Supplies - IDNAC: {powerSuppliesNeeded}, Amplifier: {amplifierPowerSuppliesNeeded}, IDNET: {idnetPowerSuppliesNeeded}");
            System.Diagnostics.Debug.WriteLine($"  Total Power Supplies Needed: {totalPowerSuppliesNeeded}");
            System.Diagnostics.Debug.WriteLine($"  Min Cabinets - Amplifiers: {minCabinetsForAmplifiers}, IDNACs: {minCabinetsForIDNACs}, IDNET: {minCabinetsForIDNET}");
            System.Diagnostics.Debug.WriteLine($"  Combined System Min Panels: {minPanelsNeeded}");

            // ENHANCED: Handle case where no level analysis is available - include IDNET data
            if (idnacResults.LevelAnalysis == null || !idnacResults.LevelAnalysis.Any())
            {
                recommendations.Add(CreateCombinedSystemWideRecommendation(totalIdnacs, totalSpeakers, totalAmplifiers,
                    amplifierBlocks, totalIdnetDevices, idnetNetworkSegments, minPanelsNeeded, 
                    totalPowerSuppliesNeeded, idnetResults));
                return recommendations;
            }

            // ENHANCED: Single panel feasibility check including IDNET considerations based on fire alarm specs
            var maxIdnacsPerSinglePanel = 3 * IDNAC_CIRCUITS_PER_ES_PS; // 9 IDNACs max per panel (3 ES-PS × 3 circuits)
            var maxIdnetPerSinglePanel = 250 * 2; // 500 IDNET devices max per panel (2 channels × 250 devices)
            
            var singlePanelFeasible = totalIdnacs <= maxIdnacsPerSinglePanel && 
                                    amplifierBlocks <= AVAILABLE_BLOCKS_THREE_BAY && 
                                    totalIdnetDevices <= maxIdnetPerSinglePanel && 
                                    minPanelsNeeded == 1;

            if (singlePanelFeasible)
            {
                // Single panel is feasible for combined system
                var cabinetConfig = CalculateCombinedCabinetRequirements(totalIdnacs, amplifierRequirements, totalIdnetDevices, idnetResults);
                var optimalFloor = FindOptimalSinglePanelFloor(idnacResults.LevelAnalysis);

                recommendations.Add(new PanelPlacementRecommendation
                {
                    Strategy = "Single Panel Configuration (Combined System)",
                    PanelCount = 1,
                    Location = optimalFloor,
                    Reasoning = $"Combined fire alarm system ({totalIdnacs} IDNACs, {totalIdnetDevices} IDNET devices, {amplifierBlocks} amplifier blocks) fits in single {cabinetConfig.CabinetType} cabinet",
                    Equipment = cabinetConfig,
                    AmplifierInfo = amplifierRequirements,
                    MinPanelsNeeded = 1,
                    SystemTotals = new Dictionary<string, object>
                    {
                        ["total_idnacs"] = totalIdnacs,
                        ["total_idnet_devices"] = totalIdnetDevices,
                        ["idnet_network_segments"] = idnetNetworkSegments,
                        ["total_speakers"] = totalSpeakers,
                        ["total_amplifiers"] = totalAmplifiers,
                        ["amplifier_blocks"] = amplifierBlocks,
                        ["total_power_supplies_needed"] = totalPowerSuppliesNeeded,
                        ["total_system_addresses"] = totalSystemAddresses
                    },
                    Advantages = new List<string>
                    {
                        "Centralized monitoring and control for both IDNAC and IDNET systems",
                        "Single amplifier location for audio system",
                        "Unified network topology for detection and notification",
                        "Lower installation cost and simplified maintenance",
                        "Single point of network connection and programming",
                        "Coordinated system-wide responses and testing"
                    },
                    Considerations = new List<string>
                    {
                        $"IDNAC wire runs: Verify distances under 4,000 ft to all notification devices",
                        $"IDNET wire runs: Verify distances under 12,500 ft to all detection devices",
                        $"Address management: {totalSystemAddresses} total addresses across both systems",
                        "Plan amplifier cooling and ventilation requirements",
                        "Consider audio cable routing for speaker circuits",
                        $"Power supply capacity: {totalPowerSuppliesNeeded} ES-PS units required",
                        $"Maintain {ConfigurationService.Current.Spare.SpareFractionDefault*100:F0}% spare capacity on all IDNACs",
                        "Coordinate IDNET network segments with panel location"
                    }
                });
            }
            else
            {
                // ENHANCED: Multi-panel analysis for large combined systems
                var multiPanelStrategy = CreateCombinedLargeSystemStrategy(idnacResults, amplifierRequirements, idnetResults,
                    minPanelsNeeded, totalPowerSuppliesNeeded, totalIdnetDevices, idnetNetworkSegments);
                recommendations.Add(multiPanelStrategy);
            }

            System.Diagnostics.Debug.WriteLine($"Generated {recommendations.Count} panel placement recommendations");
            return recommendations;
        }

        // ENHANCED: Combined system-wide analysis when no level data available
        private PanelPlacementRecommendation CreateCombinedSystemWideRecommendation(
            int totalIdnacs, int totalSpeakers, int totalAmplifiers, int amplifierBlocks,
            int totalIdnetDevices, int idnetNetworkSegments, int minPanelsNeeded, 
            int totalPowerSuppliesNeeded, IDNETSystemResults idnetResults)
        {
            // Enhanced calculation considering both IDNAC and IDNET requirements
            var panelsForIdnac = (int)Math.Ceiling((double)totalIdnacs / 9);
            var panelsForIdnet = totalIdnetDevices > 0 ? Math.Max(idnetNetworkSegments, (int)Math.Ceiling((double)totalIdnetDevices / 500)) : 0;
            var estimatedPanels = Math.Max(Math.Max(minPanelsNeeded, panelsForIdnac), panelsForIdnet);

            var combinedSystemDescription = $"Combined fire alarm system: {totalIdnacs} IDNACs, {totalIdnetDevices} IDNET devices";
            if (totalSpeakers > 0)
                combinedSystemDescription += $", {totalSpeakers} speakers";

            return new PanelPlacementRecommendation
            {
                Strategy = "Combined System Analysis (No Level Data)",
                PanelCount = estimatedPanels,
                MinPanelsNeeded = minPanelsNeeded,
                Reasoning = $"{combinedSystemDescription} require {estimatedPanels} panels minimum for comprehensive fire alarm coverage",
                AmplifierStrategy = totalSpeakers > 0 ?
                    $"Distributed amplifiers required - {totalAmplifiers} amplifiers across {estimatedPanels} panels" :
                    "No amplifiers required - IDNAC and IDNET systems only",
                SystemTotals = new Dictionary<string, object>
                {
                    ["total_idnacs"] = totalIdnacs,
                    ["total_idnet_devices"] = totalIdnetDevices,
                    ["idnet_network_segments"] = idnetNetworkSegments,
                    ["total_speakers"] = totalSpeakers,
                    ["total_amplifiers"] = totalAmplifiers,
                    ["amplifier_blocks_required"] = amplifierBlocks,
                    ["total_power_supplies_needed"] = totalPowerSuppliesNeeded,
                    ["estimated_panels"] = estimatedPanels,
                    ["min_panels_needed"] = minPanelsNeeded,
                    ["panels_for_idnac"] = panelsForIdnac,
                    ["panels_for_idnet"] = panelsForIdnet
                },
                Considerations = new List<string>
                {
                    $"COMBINED FIRE ALARM SYSTEM: {totalIdnacs} IDNACs + {totalIdnetDevices} IDNET devices + {totalSpeakers} speakers",
                    $"Minimum {estimatedPanels} panels required for combined system capacity",
                    $"IDNET network topology: {idnetNetworkSegments} network segments across {panelsForIdnet} minimum panels",
                    $"Amplifier space: {amplifierBlocks} blocks total across all panels",
                    $"Power requirements: {totalPowerSuppliesNeeded} ES-PS power supplies total",
                    "Level-by-level analysis recommended for optimal placement coordination",
                    "Professional fire alarm system design consultation REQUIRED for combined systems",
                    "Verify IDNAC wire runs under 4,000 ft from each panel location",
                    "Verify IDNET wire runs under 12,500 ft from each panel location",
                    "Coordinate address management across both IDNAC and IDNET systems"
                }
            };
        }

        // ENHANCED: Combined large system analysis including IDNET considerations
        private PanelPlacementRecommendation CreateCombinedLargeSystemStrategy(
            IDNACSystemResults idnacResults,
            AmplifierRequirements amplifierRequirements,
            IDNETSystemResults idnetResults,
            int minPanelsNeeded,
            int totalPowerSuppliesNeeded,
            int totalIdnetDevices,
            int idnetNetworkSegments)
        {
            var totalIdnacs = idnacResults.TotalIdnacsNeeded;
            var totalSpeakers = amplifierRequirements?.SpeakerCount ?? 0;
            var totalAmplifiers = amplifierRequirements?.AmplifiersNeeded ?? 0;
            var amplifierBlocks = amplifierRequirements?.AmplifierBlocks ?? 0;

            // ENHANCED: Calculate panels needed for combined system constraints
            var panelsForIDNACs = (int)Math.Ceiling((double)totalIdnacs / 9.0); // 9 IDNACs max per panel
            var panelsForAmplifiers = amplifierBlocks > 0 ? (int)Math.Ceiling((double)amplifierBlocks / AVAILABLE_BLOCKS_THREE_BAY) : 0;
            var panelsForSpeakers = totalSpeakers > 0 ? (int)Math.Ceiling((double)totalSpeakers / 100.0) : 0; // 100 speakers per amp max
            var panelsForIDNET = totalIdnetDevices > 0 ? Math.Max(idnetNetworkSegments, (int)Math.Ceiling((double)totalIdnetDevices / 500.0)) : 0; // 500 IDNET devices max per panel

            var recommendedPanels = Math.Max(Math.Max(Math.Max(panelsForIDNACs, panelsForAmplifiers), Math.Max(panelsForSpeakers, panelsForIDNET)), minPanelsNeeded);

            // Calculate distributed strategy for combined system
            var amplifiersPerPanel = recommendedPanels > 0 ? (int)Math.Ceiling((double)totalAmplifiers / recommendedPanels) : 0;
            var speakersPerPanel = recommendedPanels > 0 ? (int)Math.Ceiling((double)totalSpeakers / recommendedPanels) : 0;
            var idnetDevicesPerPanel = recommendedPanels > 0 ? (int)Math.Ceiling((double)totalIdnetDevices / recommendedPanels) : 0;
            var idnacsPerPanel = recommendedPanels > 0 ? (int)Math.Ceiling((double)totalIdnacs / recommendedPanels) : 0;

            return new PanelPlacementRecommendation
            {
                Strategy = "Multi-Panel Configuration (Combined Fire Alarm System)",
                PanelCount = recommendedPanels,
                MinPanelsNeeded = minPanelsNeeded,
                Reasoning = $"Combined fire alarm system requires {recommendedPanels} panels: {totalIdnacs} IDNACs + {totalIdnetDevices} IDNET devices + {totalSpeakers} speakers + {totalAmplifiers} amplifiers exceed single cabinet capacity",
                AmplifierStrategy = totalSpeakers > 0 ?
                    $"Distributed amplifiers - ~{amplifiersPerPanel} amplifiers per panel (~{speakersPerPanel} speakers per panel)" :
                    "No amplifiers required - IDNAC and IDNET systems only",
                SystemTotals = new Dictionary<string, object>
                {
                    ["total_idnacs"] = totalIdnacs,
                    ["total_idnet_devices"] = totalIdnetDevices,
                    ["idnet_network_segments"] = idnetNetworkSegments,
                    ["total_speakers"] = totalSpeakers,
                    ["total_amplifiers"] = totalAmplifiers,
                    ["amplifier_blocks_required"] = amplifierBlocks,
                    ["total_power_supplies_needed"] = totalPowerSuppliesNeeded,
                    ["panels_for_idnacs"] = panelsForIDNACs,
                    ["panels_for_idnet"] = panelsForIDNET,
                    ["panels_for_amplifiers"] = panelsForAmplifiers,
                    ["panels_for_speakers"] = panelsForSpeakers,
                    ["recommended_panels"] = recommendedPanels,
                    ["idnacs_per_panel"] = idnacsPerPanel,
                    ["idnet_devices_per_panel"] = idnetDevicesPerPanel,
                    ["amplifiers_per_panel"] = amplifiersPerPanel,
                    ["speakers_per_panel"] = speakersPerPanel
                },
                Advantages = new List<string>
                {
                    "Distributed combined system reliability and redundancy",
                    "Optimized wire runs: IDNAC <4,000 ft, IDNET <12,500 ft per panel",
                    "Zone-based control and fault isolation for both detection and notification",
                    "Easier service access and troubleshooting per building area",
                    $"Maintains {ConfigurationService.Current.Spare.SpareFractionDefault*100:F0}% spare capacity per IDNAC circuit",
                    "Distributed IDNET network segments reduce single points of failure",
                    "Localized amplifier troubleshooting and maintenance",
                    "Scalable design for future expansion of both systems",
                    "Geographic distribution improves overall system resilience"
                },
                Considerations = new List<string>
                {
                    $"CRITICAL: Combined fire alarm system requires {recommendedPanels} panels minimum",
                    $"IDNET NETWORK TOPOLOGY: {idnetNetworkSegments} network segments across {panelsForIDNET} panels",
                    $"IDNAC DISTRIBUTION: {idnacsPerPanel} circuits per panel, {idnetDevicesPerPanel} IDNET devices per panel",
                    $"AMPLIFIER REQUIREMENTS: {amplifierBlocks} total blocks across all panels",
                    $"Power requirements: {totalPowerSuppliesNeeded} ES-PS power supplies total",
                    $"Network coordination: {recommendedPanels} panels require IDNAC and IDNET connections",
                    "Professional fire alarm system design consultation REQUIRED for combined systems",
                    "Coordination required for system-wide responses between detection and notification",
                    "Address management across multiple panels and both systems",
                    "Plan amplifier ventilation at each panel location",
                    "Consider seismic bracing for multiple cabinet installations",
                    "Verify local fire code requirements for distributed fire alarm systems",
                    "Ensure proper network redundancy and failover capabilities"
                }
            };
        }

        // FIXED: Add method to find optimal floor for single panel
        private Tuple<string, string> FindOptimalSinglePanelFloor(Dictionary<string, IDNACAnalysisResult> levelAnalysis)
        {
            if (levelAnalysis == null || !levelAnalysis.Any())
                return new Tuple<string, string>("TBD", "Manual selection required - no level data available");

            try
            {
                // Find middle floor or ground level
                var mainLevels = levelAnalysis.Where(l =>
                    !l.Key.ToUpper().Contains("VILLA") &&
                    !l.Key.ToUpper().Contains("MECH") &&
                    !l.Key.ToUpper().Contains("PARKING"))
                    .OrderBy(l => GetLevelSortKey(l.Key))
                    .ToList();

                if (mainLevels.Any())
                {
                    var midIndex = mainLevels.Count / 2;
                    var levelName = mainLevels[midIndex].Key;
                    return new Tuple<string, string>(levelName, $"Central building location - optimal for system distribution to {mainLevels.Count} main floors");
                }

                // Fallback to any available level
                var firstLevel = levelAnalysis.First();
                return new Tuple<string, string>(firstLevel.Key, "Default level selection - verify optimal location");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding optimal floor: {ex.Message}");
                return new Tuple<string, string>("Level 1", "Default ground level location");
            }
        }

        /// <summary>
        /// Calculate spare-aware capacity requirements
        /// </summary>
        private (int spareAwareIdnacs, double spareAwareCurrent, int spareAwarePowerSupplies) CalculateSpareAwareRequirements(
            int totalIdnacs, double totalCurrent, int totalUnitLoads)
        {
            var sparePolicy = ConfigurationService.Current.Spare;
            var capacityPolicy = ConfigurationService.Current.Capacity;
            
            // Apply spare capacity multiplier
            var spareFactor = 1.0 / (1.0 - sparePolicy.SpareFractionDefault);
            
            var spareAwareIdnacs = totalIdnacs;
            var spareAwareCurrent = totalCurrent;
            var spareAwarePowerSupplies = (int)Math.Ceiling((double)totalIdnacs / IDNAC_CIRCUITS_PER_ES_PS);
            
            if (sparePolicy.EnforceOnCurrent || sparePolicy.EnforceOnUL || sparePolicy.EnforceOnPower)
            {
                // Calculate required capacity with spare margin
                var requiredCurrentCapacity = totalCurrent * spareFactor;
                var requiredULCapacity = totalUnitLoads * spareFactor;
                
                // Determine IDNACs needed with spare capacity
                var idnacsForCurrentWithSpare = (int)Math.Ceiling(requiredCurrentCapacity / capacityPolicy.IdnacAlarmCurrentLimitA);
                var idnacsForULWithSpare = (int)Math.Ceiling(requiredULCapacity / capacityPolicy.IdnacStandbyUnitLoadLimit);
                
                spareAwareIdnacs = Math.Max(idnacsForCurrentWithSpare, idnacsForULWithSpare);
                spareAwareCurrent = requiredCurrentCapacity;
                spareAwarePowerSupplies = (int)Math.Ceiling((double)spareAwareIdnacs / IDNAC_CIRCUITS_PER_ES_PS);
            }
            
            return (spareAwareIdnacs, spareAwareCurrent, spareAwarePowerSupplies);
        }

        // ENHANCED: Combined cabinet requirements calculation including IDNET considerations
        private CabinetConfiguration CalculateCombinedCabinetRequirements(int totalIdnacs, AmplifierRequirements amplifierRequirements, int totalIdnetDevices, IDNETSystemResults idnetResults)
        {
            // Apply spare-aware capacity calculations
            var totalCurrent = amplifierRequirements?.AmplifierCurrent ?? 0;
            var totalUnitLoads = totalIdnacs; // Simplified: assume 1 UL per IDNAC device for cabinet sizing
            
            var (spareAwareIdnacs, spareAwareCurrent, spareAwarePowerSupplies) = 
                CalculateSpareAwareRequirements(totalIdnacs, totalCurrent, totalUnitLoads);

            // Use spare-aware values for cabinet sizing
            var basePowerSupplies = spareAwarePowerSupplies;

            // Calculate amplifier blocks and current needed
            var amplifierBlocksNeeded = amplifierRequirements?.AmplifierBlocks ?? 0;
            var amplifierCurrent = amplifierRequirements?.AmplifierCurrent ?? 0;

            // ENHANCED: Calculate IDNET power requirements (convert mA to A)
            var idnetCurrentAmps = (idnetResults?.TotalPowerConsumption ?? 0) / 1000.0;
            
            // ENHANCED: Dynamic ES-PS capacity selection based on system configuration per fire alarm specs
            var hasIdnacCircuits = totalIdnacs > 0;
            var hasAmplifiers = amplifierCurrent > 0;
            
            // Select appropriate ES-PS capacity based on fire alarm specifications:
            // - 12.7A: Fan only (no IDNAC modules) - Maximum output
            // - 9.7A: Fan + IDNAC modules present - Standard IDNAC configuration  
            // - 9.5A: Without fan (not typically used)
            var esPsCapacity = hasIdnacCircuits ? ES_PS_TOTAL_DC_OUTPUT_WITH_FAN : ES_PS_MAX_OUTPUT; // 9.7A or 12.7A
            
            var idnetPowerSuppliesNeeded = idnetCurrentAmps > 0.1 ? (int)Math.Ceiling(idnetCurrentAmps / esPsCapacity) : 0;
            var powerSuppliesForAmplifiers = amplifierCurrent > 0 ? (int)Math.Ceiling(amplifierCurrent / esPsCapacity) : 0;

            // Combined power supply requirement (IDNAC + Amplifier + IDNET)
            var totalPowerSupplies = Math.Max(Math.Max(basePowerSupplies, powerSuppliesForAmplifiers), Math.Max(idnetPowerSuppliesNeeded, 1));
            var totalCurrentDraw = amplifierCurrent + idnetCurrentAmps;

            // ENHANCED: Consider IDNET network requirements for cabinet sizing
            // Large IDNET systems may require additional cabinet space for network infrastructure
            var networkInfrastructureBlocks = totalIdnetDevices > 100 ? 1 : 0; // Reserve space for network hubs/switches

            // Determine optimal cabinet configuration
            if (totalPowerSupplies <= 1 && amplifierBlocksNeeded + networkInfrastructureBlocks <= AVAILABLE_BLOCKS_SINGLE_BAY)
            {
                return new CabinetConfiguration
                {
                    CabinetType = "Single Bay (Combined System)",
                    PowerSupplies = totalPowerSupplies,
                    TotalIdnacs = Math.Min(3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_SINGLE_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = Math.Max(0, AVAILABLE_BLOCKS_SINGLE_BAY - amplifierBlocksNeeded - networkInfrastructureBlocks),
                    AmplifierCurrent = totalCurrentDraw,
                    EsPsCapacity = esPsCapacity * totalPowerSupplies,
                    PowerMargin = (esPsCapacity * totalPowerSupplies) - totalCurrentDraw,
                    BatteryChargerAvailable = totalCurrentDraw < (esPsCapacity * 0.8),
                    ModelConfig = new List<string> 
                    { 
                        "A100-9706 (ES-PS with Touch Screen)",
                        $"IDNET Support: {totalIdnetDevices} detection devices",
                        $"Spare Capacity: {ConfigurationService.Current.Spare.SpareFractionDefault*100:F0}% applied to sizing"
                    }
                };
            }
            else if (totalPowerSupplies <= 2 && amplifierBlocksNeeded + networkInfrastructureBlocks <= AVAILABLE_BLOCKS_TWO_BAY)
            {
                return new CabinetConfiguration
                {
                    CabinetType = "Two Bay (Combined System)",
                    PowerSupplies = totalPowerSupplies,
                    TotalIdnacs = Math.Min(totalPowerSupplies * 3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_TWO_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = AVAILABLE_BLOCKS_TWO_BAY - amplifierBlocksNeeded - networkInfrastructureBlocks,
                    AmplifierCurrent = totalCurrentDraw,
                    EsPsCapacity = esPsCapacity * totalPowerSupplies,
                    PowerMargin = (esPsCapacity * totalPowerSupplies) - totalCurrentDraw,
                    BatteryChargerAvailable = totalCurrentDraw < (esPsCapacity * totalPowerSupplies * 0.8),
                    ModelConfig = new List<string>
                    {
                        "A100-9706 (ES-PS with Touch Screen)",
                        totalPowerSupplies > 1 ? "A100-5401 (Additional ES-PS)" : null,
                        "A100-2300 (Expansion Bay)",
                        $"IDNET Support: {totalIdnetDevices} detection devices",
                        $"Spare Capacity: {ConfigurationService.Current.Spare.SpareFractionDefault*100:F0}% applied to sizing"
                    }.Where(x => x != null).ToList()
                };
            }
            else
            {
                return new CabinetConfiguration
                {
                    CabinetType = "Three Bay (Combined System)",
                    PowerSupplies = totalPowerSupplies,
                    TotalIdnacs = Math.Min(totalPowerSupplies * 3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_THREE_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = Math.Max(0, AVAILABLE_BLOCKS_THREE_BAY - amplifierBlocksNeeded - networkInfrastructureBlocks),
                    AmplifierCurrent = totalCurrentDraw,
                    EsPsCapacity = esPsCapacity * totalPowerSupplies,
                    PowerMargin = (esPsCapacity * totalPowerSupplies) - totalCurrentDraw,
                    BatteryChargerAvailable = totalCurrentDraw < (esPsCapacity * totalPowerSupplies * 0.8),
                    ModelConfig = new List<string>
                    {
                        "A100-9706 (ES-PS with Touch Screen)",
                        totalPowerSupplies > 1 ? $"A100-5401 x{totalPowerSupplies - 1} (Additional ES-PS)" : null,
                        "A100-2300 x2 (Expansion Bays)",
                        $"IDNET Support: {totalIdnetDevices} detection devices, {idnetResults?.NetworkSegments?.Count ?? 0} network segments",
                        $"Spare Capacity: {ConfigurationService.Current.Spare.SpareFractionDefault*100:F0}% applied to sizing"
                    }.Where(x => x != null).ToList()
                };
            }
        }

        private CabinetConfiguration CalculateCabinetRequirements(int totalIdnacs, AmplifierRequirements amplifierRequirements)
        {
            // Calculate base power supplies needed for IDNACs
            var basePowerSupplies = (int)Math.Ceiling((double)totalIdnacs / IDNAC_CIRCUITS_PER_ES_PS);

            // Calculate amplifier blocks and current needed
            var amplifierBlocksNeeded = amplifierRequirements?.AmplifierBlocks ?? 0;
            var amplifierCurrent = amplifierRequirements?.AmplifierCurrent ?? 0;

            // Check ES-PS power capacity (9.5A without fan, 9.7A with fan+IDNAC)
            var esPsCapacity = ES_PS_TOTAL_DC_OUTPUT_WITH_FAN; // 9.7A
            var powerSuppliesForAmplifiers = amplifierCurrent > 0 ? (int)Math.Ceiling(amplifierCurrent / esPsCapacity) : 0;

            // Total power supplies needed (IDNAC + Amplifier requirements)
            var totalPowerSupplies = Math.Max(basePowerSupplies, powerSuppliesForAmplifiers);

            // Determine optimal cabinet configuration
            if (totalPowerSupplies <= 1 && amplifierBlocksNeeded <= AVAILABLE_BLOCKS_SINGLE_BAY)
            {
                return new CabinetConfiguration
                {
                    CabinetType = "Single Bay",
                    PowerSupplies = Math.Max(1, totalPowerSupplies),
                    TotalIdnacs = Math.Min(3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_SINGLE_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = Math.Max(0, AVAILABLE_BLOCKS_SINGLE_BAY - amplifierBlocksNeeded),
                    AmplifierCurrent = amplifierCurrent,
                    EsPsCapacity = esPsCapacity,
                    PowerMargin = esPsCapacity - amplifierCurrent,
                    BatteryChargerAvailable = amplifierCurrent == 0,
                    ModelConfig = new List<string> { "A100-9706 (ES-PS with Touch Screen)" }
                };
            }
            else if (totalPowerSupplies <= 2 && amplifierBlocksNeeded <= AVAILABLE_BLOCKS_TWO_BAY)
            {
                var actualPowerSupplies = Math.Max(1, totalPowerSupplies);
                return new CabinetConfiguration
                {
                    CabinetType = "Two Bay",
                    PowerSupplies = actualPowerSupplies,
                    TotalIdnacs = Math.Min(actualPowerSupplies * 3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_TWO_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = AVAILABLE_BLOCKS_TWO_BAY - amplifierBlocksNeeded,
                    AmplifierCurrent = amplifierCurrent,
                    EsPsCapacity = esPsCapacity * actualPowerSupplies,
                    PowerMargin = (esPsCapacity * actualPowerSupplies) - amplifierCurrent,
                    BatteryChargerAvailable = amplifierCurrent == 0,
                    ModelConfig = new List<string>
                    {
                        "A100-9706 (ES-PS with Touch Screen)",
                        actualPowerSupplies > 1 ? "A100-5401 (Additional ES-PS)" : null,
                        "A100-2300 (Expansion Bay)"
                    }.Where(x => x != null).ToList()
                };
            }
            else
            {
                var actualPowerSupplies = Math.Max(1, totalPowerSupplies);
                return new CabinetConfiguration
                {
                    CabinetType = "Three Bay",
                    PowerSupplies = actualPowerSupplies,
                    TotalIdnacs = Math.Min(actualPowerSupplies * 3, totalIdnacs),
                    AvailableBlocks = AVAILABLE_BLOCKS_THREE_BAY,
                    AmplifierBlocksUsed = amplifierBlocksNeeded,
                    RemainingBlocks = Math.Max(0, AVAILABLE_BLOCKS_THREE_BAY - amplifierBlocksNeeded),
                    AmplifierCurrent = amplifierCurrent,
                    EsPsCapacity = esPsCapacity * actualPowerSupplies,
                    PowerMargin = (esPsCapacity * actualPowerSupplies) - amplifierCurrent,
                    BatteryChargerAvailable = amplifierCurrent == 0,
                    ModelConfig = new List<string>
                    {
                        "A100-9706 (ES-PS with Touch Screen)",
                        actualPowerSupplies > 1 ? $"A100-5401 x{actualPowerSupplies - 1} (Additional ES-PS)" : null,
                        "A100-2300 x2 (Expansion Bays)"
                    }.Where(x => x != null).ToList()
                };
            }
        }

        private double GetLevelSortKey(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return 5999;

            try
            {
                string levelUpper = levelName.ToUpper();

                // Handle parking levels (e.g., P1, P1.1)
                if (levelUpper.Contains("LEVEL P"))
                {
                    var match = Regex.Match(levelName, @"P(\d+)\.?(\d*)");
                    if (match.Success)
                    {
                        int mainNum = 0;
                        int subNum = 0;
                        int.TryParse(match.Groups[1].Value, out mainNum);
                        if (match.Groups.Count > 2 && match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
                            int.TryParse(match.Groups[2].Value, out subNum);
                        return mainNum + subNum * 0.1;
                    }
                    return 0;
                }
                // Handle villa levels (VILLA 3, etc.)
                if (levelUpper.Contains("VILLA"))
                {
                    var match = Regex.Match(levelName, @"(\d+)");
                    if (match.Success)
                    {
                        int villaNum = 0;
                        int.TryParse(match.Groups[1].Value, out villaNum);
                        return 3000 + villaNum;
                    }
                    return 3999;
                }
                // Handle mechanical/roof levels
                if (levelUpper.Contains("MECH") || levelUpper.Contains("ROOF"))
                {
                    var match = Regex.Match(levelName, @"(\d+)");
                    if (match.Success)
                    {
                        int mechNum = 0;
                        int.TryParse(match.Groups[1].Value, out mechNum);
                        return 4000 + mechNum;
                    }
                    return 4999;
                }
                // Handle main levels ("Level 1", "Level 2", �)
                if (levelName.StartsWith("Level "))
                {
                    var match = Regex.Match(levelName, @"Level\s+(\d+)");
                    if (match.Success)
                    {
                        int mainNum = 0;
                        int.TryParse(match.Groups[1].Value, out mainNum);
                        return 1000 + mainNum;
                    }
                }
                // Default: extract any number if present
                var defaultMatch = Regex.Match(levelName, @"(\d+)");
                if (defaultMatch.Success)
                {
                    int num = 0;
                    int.TryParse(defaultMatch.Groups[1].Value, out num);
                    return 2000 + num;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sorting level {levelName}: {ex.Message}");
            }

            return 5999;
        }
    }
}