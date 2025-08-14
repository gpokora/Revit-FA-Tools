using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Services;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools
{
    public class AmplifierCalculator
    {
        // Cabinet and Amplifier Specifications (per 4100ES Table 12)
        private const int CABINET_BLOCKS_SINGLE_BAY = 8;          // Total blocks in single expansion bay (A,B,C,D,E,F,G,H)
        private const int CABINET_BLOCKS_TWO_BAY = 16;            // Total blocks in two bay system  
        private const int CABINET_BLOCKS_THREE_BAY = 24;          // Total blocks in three bay system

        // Available blocks after accounting for Audio Controller (Blocks A+B) and ES-PS
        private const int AVAILABLE_BLOCKS_SINGLE_BAY = 4;        // Blocks E,F available (G,H used by ES-PS with 1 Flex)
        private const int AVAILABLE_BLOCKS_TWO_BAY = 12;          // 14 blocks - 2 for Audio Controller = 12 available
        private const int AVAILABLE_BLOCKS_THREE_BAY = 20;        // 22 blocks - 2 for Audio Controller = 20 available

        // Amplifier Requirements (based on 4100ES documentation)
        private const int AMPLIFIER_BLOCKS_PER_FLEX35 = 2;        // Flex-35 amplifier takes 2 blocks
        private const int AMPLIFIER_BLOCKS_PER_FLEX50 = 2;        // Flex-50 amplifier takes 2 blocks  
        private const int AMPLIFIER_BLOCKS_PER_FLEX100 = 4;       // Flex-100 amplifier takes 4 blocks (E,F,G,H)
        private const double AMPLIFIER_POWER_FLEX35_MAX = 35;     // 35W amplifier maximum
        private const double AMPLIFIER_POWER_FLEX50_MAX = 50;     // 50W amplifier maximum
        private const double AMPLIFIER_POWER_FLEX100_MAX = 100;   // 100W amplifier maximum
        private const int SPEAKERS_PER_FLEX35 = 100;              // Maximum 100 speakers per Flex-35 (per spec)
        private const int SPEAKERS_PER_FLEX50 = 100;              // Maximum 100 speakers per Flex-50 (per spec)
        private const int SPEAKERS_PER_FLEX100 = 100;             // Maximum 100 speakers per Flex-100 (per spec)

        // Amplifier Current Requirements (from 4100ES Table 14)
        private const double AMPLIFIER_CURRENT_FLEX35 = 5.5;      // 5.5A power supply loading
        private const double AMPLIFIER_CURRENT_FLEX50 = 5.55;     // 5.55A power supply loading  
        private const double AMPLIFIER_CURRENT_FLEX100 = 9.6;     // 9.6A power supply loading

        // Amplifier Spare Capacity (configurable through UI)
        private double GetSpareCapacityPercent()
        {
            try
            {
                var configService = new ConfigurationManagementService();
                var config = configService.GetSystemConfiguration();
                return config.SpareCapacityPercent / 100.0; // Convert percentage to decimal
            }
            catch
            {
                return 0.20; // Default 20% if config unavailable
            }
        }

        private double GetAmplifierPowerFlex35() => AMPLIFIER_POWER_FLEX35_MAX * (1 - GetSpareCapacityPercent());
        private double GetAmplifierPowerFlex50() => AMPLIFIER_POWER_FLEX50_MAX * (1 - GetSpareCapacityPercent());
        private double GetAmplifierPowerFlex100() => AMPLIFIER_POWER_FLEX100_MAX * (1 - GetSpareCapacityPercent());

        public DeviceTypeAnalysis AnalyzeDeviceTypes(ElectricalResults results)
        {
            var analysis = new DeviceTypeAnalysis();

            if (results?.Elements == null)
                return analysis;

            // Initialize device type categories
            analysis.DeviceTypes["speakers"] = new DeviceTypeData();
            analysis.DeviceTypes["strobes"] = new DeviceTypeData();
            analysis.DeviceTypes["horns"] = new DeviceTypeData();
            analysis.DeviceTypes["combo"] = new DeviceTypeData();
            analysis.DeviceTypes["other"] = new DeviceTypeData();

            System.Diagnostics.Debug.WriteLine($"Device Type Analysis Starting - Total Elements: {results.Elements.Count}");

            foreach (var elem in results.Elements.Where(e => e != null))
            {
                try
                {
                    var familyName = elem.FamilyName ?? "";
                    var typeName = elem.TypeName ?? "";
                    var current = elem.Current;
                    var wattage = elem.Wattage;

                    // Use device classification service for proper categorization
                    var classification = CandelaConfigurationService.ClassifyDevice(familyName, typeName);
                    
                    // Determine primary category based on classification
                    string primaryCategory = "other"; // Default primary category
                    
                    if (classification.IsSpeaker)
                    {
                        if (classification.HasStrobe)
                        {
                            primaryCategory = "combo"; // Speaker + Strobe combination
                        }
                        else
                        {
                            primaryCategory = "speakers"; // Pure speaker
                        }
                    }
                    else if (classification.HasStrobe)
                    {
                        primaryCategory = "strobes"; // Pure strobe
                    }
                    else if (classification.IsAudioDevice)
                    {
                        primaryCategory = "horns"; // Audio but not speaker (horn/buzzer)
                    }

                    // Add to primary category totals
                    analysis.DeviceTypes[primaryCategory].Count++;
                    analysis.DeviceTypes[primaryCategory].Current += current;
                    analysis.DeviceTypes[primaryCategory].Wattage += wattage;
                    
                    // IMPORTANT: ALSO add to strobes category if device has strobe (for strobe current calculation)
                    // This ensures ALL strobe devices (including combo devices) contribute to strobe current
                    if (classification.HasStrobe && primaryCategory != "strobes")
                    {
                        analysis.DeviceTypes["strobes"].Count++;
                        analysis.DeviceTypes["strobes"].Current += current;
                        analysis.DeviceTypes["strobes"].Wattage += wattage;
                    }

                    // Track families per primary category
                    var familyOriginal = elem.FamilyName ?? "Unknown";
                    if (!analysis.DeviceTypes[primaryCategory].Families.ContainsKey(familyOriginal))
                    {
                        analysis.DeviceTypes[primaryCategory].Families[familyOriginal] = 0;
                    }
                    analysis.DeviceTypes[primaryCategory].Families[familyOriginal]++;
                    
                    // Also track families for strobes if device has strobe
                    if (classification.HasStrobe && primaryCategory != "strobes")
                    {
                        if (!analysis.DeviceTypes["strobes"].Families.ContainsKey(familyOriginal))
                        {
                            analysis.DeviceTypes["strobes"].Families[familyOriginal] = 0;
                        }
                        analysis.DeviceTypes["strobes"].Families[familyOriginal]++;
                    }

                    // FIXED: Add detailed logging for first few elements to debug categorization
                    if (analysis.DeviceTypes.Values.Sum(d => d.Count) <= 10)
                    {
                        var strobeNote = classification.HasStrobe && primaryCategory != "strobes" ? " + strobes" : "";
                        var classNote = classification.IsSpeaker ? " [Speaker]" : "";
                        System.Diagnostics.Debug.WriteLine($"  Element {elem.Id}: '{familyName}/{typeName}' → {primaryCategory}{strobeNote}{classNote} ({current:F3}A, {wattage:F1}W)");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error analyzing device type for element {elem?.Id}: {ex.Message}");
                }
            }

            // FIXED: Add comprehensive summary logging
            System.Diagnostics.Debug.WriteLine($"Device Type Analysis Complete:");
            foreach (var category in analysis.DeviceTypes)
            {
                if (category.Value.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  {category.Key.ToUpper()}: {category.Value.Count} devices, {category.Value.Current:F2}A, {category.Value.Wattage:F2}W");
                }
            }

            return analysis;
        }

        public AmplifierRequirements CalculateAmplifierRequirements(ElectricalResults results)
        {
            if (results == null)
            {
                System.Diagnostics.Debug.WriteLine("AmplifierCalculator: No results provided");
                return CreateEmptyAmplifierRequirements();
            }

            try
            {
                var deviceAnalysis = AnalyzeDeviceTypes(results);

                // FIXED: Add debug output to track speaker detection
                var speakersFromSpeakerCategory = deviceAnalysis.DeviceTypes["speakers"].Count;
                var speakersFromComboCategory = deviceAnalysis.DeviceTypes["combo"].Count;
                var totalSpeakers = speakersFromSpeakerCategory + speakersFromComboCategory;

                var speakerWattageFromSpeakers = deviceAnalysis.DeviceTypes["speakers"].Wattage;
                var speakerWattageFromCombo = deviceAnalysis.DeviceTypes["combo"].Wattage;
                var totalSpeakerWattage = speakerWattageFromSpeakers + speakerWattageFromCombo;

                System.Diagnostics.Debug.WriteLine($"Amplifier Requirements Analysis:");
                System.Diagnostics.Debug.WriteLine($"  Speakers from 'speakers' category: {speakersFromSpeakerCategory}");
                System.Diagnostics.Debug.WriteLine($"  Speakers from 'combo' category: {speakersFromComboCategory}");
                System.Diagnostics.Debug.WriteLine($"  Total Speakers Detected: {totalSpeakers}");
                System.Diagnostics.Debug.WriteLine($"  Total Speaker Wattage: {totalSpeakerWattage:F2}W");

                if (totalSpeakers == 0)
                {
                    System.Diagnostics.Debug.WriteLine("  No speakers detected - returning no amplifiers needed");
                    return CreateEmptyAmplifierRequirements();
                }

                // FIXED: Enhanced amplifier sizing logic for large systems
                int amplifiersNeeded;
                string amplifierType;
                int blocksPerAmp;
                double ampPowerUsable;
                double ampPowerMax;
                double ampCurrentPerAmp;
                double totalAmpCurrent;

                // FIXED: Always use Flex-100 for large systems (>100 speakers) for cost efficiency
                if (totalSpeakers > 100)
                {
                    // Large system - use Flex-100 amplifiers exclusively
                    amplifiersNeeded = (int)Math.Ceiling((double)totalSpeakers / SPEAKERS_PER_FLEX100);
                    amplifierType = amplifiersNeeded == 1 ? "Flex-100" : $"Flex-100 x{amplifiersNeeded}";
                    blocksPerAmp = AMPLIFIER_BLOCKS_PER_FLEX100;
                    ampPowerUsable = GetAmplifierPowerFlex100();
                    ampPowerMax = AMPLIFIER_POWER_FLEX100_MAX;
                    ampCurrentPerAmp = AMPLIFIER_CURRENT_FLEX100;
                    totalAmpCurrent = ampCurrentPerAmp * amplifiersNeeded;

                    System.Diagnostics.Debug.WriteLine($"  Large system - using Flex-100 amplifiers:");
                    System.Diagnostics.Debug.WriteLine($"    Amplifiers needed: {amplifiersNeeded}");
                    System.Diagnostics.Debug.WriteLine($"    Blocks per amplifier: {blocksPerAmp}");
                    System.Diagnostics.Debug.WriteLine($"    Total blocks required: {amplifiersNeeded * blocksPerAmp}");
                    System.Diagnostics.Debug.WriteLine($"    Total amplifier current: {totalAmpCurrent:F2}A");
                }
                else if (totalSpeakers <= SPEAKERS_PER_FLEX35 && totalSpeakerWattage <= GetAmplifierPowerFlex35())
                {
                    // Small system - Flex-35 sufficient
                    amplifiersNeeded = 1;
                    amplifierType = "Flex-35";
                    blocksPerAmp = AMPLIFIER_BLOCKS_PER_FLEX35;
                    ampPowerUsable = GetAmplifierPowerFlex35();
                    ampPowerMax = AMPLIFIER_POWER_FLEX35_MAX;
                    ampCurrentPerAmp = AMPLIFIER_CURRENT_FLEX35;
                    totalAmpCurrent = ampCurrentPerAmp;

                    System.Diagnostics.Debug.WriteLine($"  Small system - using single Flex-35 amplifier");
                }
                else if (totalSpeakers <= SPEAKERS_PER_FLEX50 && totalSpeakerWattage <= GetAmplifierPowerFlex50())
                {
                    // Medium system - Flex-50 sufficient  
                    amplifiersNeeded = 1;
                    amplifierType = "Flex-50";
                    blocksPerAmp = AMPLIFIER_BLOCKS_PER_FLEX50;
                    ampPowerUsable = GetAmplifierPowerFlex50();
                    ampPowerMax = AMPLIFIER_POWER_FLEX50_MAX;
                    ampCurrentPerAmp = AMPLIFIER_CURRENT_FLEX50;
                    totalAmpCurrent = ampCurrentPerAmp;

                    System.Diagnostics.Debug.WriteLine($"  Medium system - using single Flex-50 amplifier");
                }
                else
                {
                    // Default to Flex-100 for anything that doesn't fit smaller amplifiers
                    amplifiersNeeded = (int)Math.Ceiling((double)totalSpeakers / SPEAKERS_PER_FLEX100);
                    amplifierType = amplifiersNeeded == 1 ? "Flex-100" : $"Flex-100 x{amplifiersNeeded}";
                    blocksPerAmp = AMPLIFIER_BLOCKS_PER_FLEX100;
                    ampPowerUsable = GetAmplifierPowerFlex100();
                    ampPowerMax = AMPLIFIER_POWER_FLEX100_MAX;
                    ampCurrentPerAmp = AMPLIFIER_CURRENT_FLEX100;
                    totalAmpCurrent = ampCurrentPerAmp * amplifiersNeeded;

                    System.Diagnostics.Debug.WriteLine($"  Default to Flex-100 - {amplifiersNeeded} amplifiers needed");
                }

                var totalAmplifierBlocks = amplifiersNeeded * blocksPerAmp;

                // FIXED: Add validation for extremely large configurations and provide warnings
                if (totalAmplifierBlocks > 100)
                {
                    System.Diagnostics.Debug.WriteLine($"  WARNING: System requires {totalAmplifierBlocks} blocks - extremely large system!");
                    System.Diagnostics.Debug.WriteLine($"  This will require {Math.Ceiling((double)totalAmplifierBlocks / 20)} three-bay cabinets minimum");
                }
                else if (totalAmplifierBlocks > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"  LARGE SYSTEM: {totalAmplifierBlocks} blocks required");
                    System.Diagnostics.Debug.WriteLine($"  Will require multiple panels/cabinets");
                }

                var result = new AmplifierRequirements
                {
                    AmplifiersNeeded = amplifiersNeeded,
                    AmplifierType = amplifierType,
                    AmplifierBlocks = totalAmplifierBlocks,
                    AmplifierPowerUsable = ampPowerUsable * amplifiersNeeded,
                    AmplifierPowerMax = ampPowerMax * amplifiersNeeded,
                    AmplifierCurrent = totalAmpCurrent,
                    SpeakerCount = totalSpeakers,
                    SpareCapacityPercent = 20
                };

                // FIXED: Add comprehensive result logging
                System.Diagnostics.Debug.WriteLine($"Amplifier Requirements Final Result:");
                System.Diagnostics.Debug.WriteLine($"  Speaker Count: {result.SpeakerCount}");
                System.Diagnostics.Debug.WriteLine($"  Amplifiers Needed: {result.AmplifiersNeeded}");
                System.Diagnostics.Debug.WriteLine($"  Amplifier Type: {result.AmplifierType}");
                System.Diagnostics.Debug.WriteLine($"  Total Blocks Required: {result.AmplifierBlocks}");
                System.Diagnostics.Debug.WriteLine($"  Total Power (Usable/Max): {result.AmplifierPowerUsable:F0}W / {result.AmplifierPowerMax:F0}W");
                System.Diagnostics.Debug.WriteLine($"  Total Current Required: {result.AmplifierCurrent:F2}A");
                System.Diagnostics.Debug.WriteLine($"  Estimated Cabinets Needed: {Math.Ceiling((double)result.AmplifierBlocks / 20)}");

                return result;
            }
            catch (Exception ex)
            {
                // FIXED: Enhanced error handling with detailed diagnostics
                System.Diagnostics.Debug.WriteLine($"ERROR in amplifier requirements calculation: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return new AmplifierRequirements
                {
                    AmplifiersNeeded = 0,
                    AmplifierType = $"CALCULATION ERROR: {ex.Message}",
                    AmplifierBlocks = 0,
                    AmplifierPowerUsable = 0,
                    AmplifierPowerMax = 0,
                    AmplifierCurrent = 0,
                    SpeakerCount = 0,
                    SpareCapacityPercent = 20
                };
            }
        }

        // FIXED: Add helper method to create empty requirements
        private AmplifierRequirements CreateEmptyAmplifierRequirements()
        {
            return new AmplifierRequirements
            {
                AmplifiersNeeded = 0,
                AmplifierType = "None Required - No Speakers Detected",
                AmplifierBlocks = 0,
                AmplifierPowerUsable = 0,
                AmplifierPowerMax = 0,
                AmplifierCurrent = 0,
                SpeakerCount = 0,
                SpareCapacityPercent = 20
            };
        }

        /// <summary>
        /// Calculate optimized circuit balancing for notification devices
        /// </summary>
        public CircuitBalancingResults CalculateOptimizedCircuitBalancing(ElectricalResults results, bool useOptimization = true)
        {
            if (results == null)
            {
                return new CircuitBalancingResults { Success = false, Message = "No results provided" };
            }

            try
            {
                var configService = new ConfigurationManagementService();
                var config = configService.GetSystemConfiguration();
                var balancer = new CircuitBalancer();

                // Setup circuit capacity with current configuration
                var capacity = new CircuitBalancer.CircuitCapacity
                {
                    MaxCurrent = config.IDNACAlarmCurrentLimit,
                    MaxUnitLoads = config.IDNACUnitLoadLimit,
                    MaxDevices = config.IDNACDeviceLimit,
                    SpareCapacityFraction = config.SpareCapacityPercent / 100.0
                };

                // Setup balancing options
                var options = new CircuitBalancer.BalancingOptions
                {
                    UseOptimizedBalancing = useOptimization,
                    GroupByLevel = !config.ExcludeVillaLevels, // Group by level unless excluding villas
                    PrioritizeHighLoads = true,
                    TargetUtilization = config.TargetUtilizationPercent / 100.0,
                    AllowMixedLevels = !config.EnableAutoBalancing,
                    BalanceCurrentFirst = true
                };

                // Add excluded levels
                if (config.ExcludeVillaLevels)
                {
                    options.ExcludedLevels.AddRange(new[] { "VILLA", "PRIVATE" });
                }
                if (config.ExcludeGarageLevels)
                {
                    options.ExcludedLevels.AddRange(new[] { "GARAGE", "PARKING" });
                }

                // Filter to notification devices only and convert to DeviceSnapshot
                var notificationDevices = results.Elements?.Where(e => 
                    e.TypeName.ToUpper().Contains("STROBE") || 
                    e.TypeName.ToUpper().Contains("SPEAKER") || 
                    e.TypeName.ToUpper().Contains("HORN"))
                    .Select(e => new DeviceSnapshot(
                        ElementId: (int)e.Id,
                        LevelName: e.LevelName,
                        FamilyName: e.FamilyName,
                        TypeName: e.TypeName,
                        Watts: e.Wattage,
                        Amps: e.Current,
                        UnitLoads: (int)(e.Current / 0.0008), // Convert to unit loads
                        HasStrobe: e.TypeName.ToUpper().Contains("STROBE"),
                        HasSpeaker: e.TypeName.ToUpper().Contains("SPEAKER"),
                        IsIsolator: false,
                        IsRepeater: false,
                        Zone: null,
                        X: 0.0,
                        Y: 0.0,
                        Z: 0.0,
                        StandbyCurrent: e.Current,
                        HasOverride: false,
                        CustomProperties: new Dictionary<string, object>()
                    )).ToList() ?? new List<DeviceSnapshot>();

                // Perform balancing
                var balancingResult = balancer.BalanceDevices(notificationDevices, capacity, options);

                return new CircuitBalancingResults
                {
                    Success = true,
                    Message = $"Circuit balancing completed using {balancingResult.AlgorithmUsed}",
                    TotalCircuits = balancingResult.TotalCircuitsUsed,
                    AverageUtilization = balancingResult.AverageUtilization,
                    EfficiencyScore = balancingResult.EfficiencyScore,
                    UnallocatedDevices = balancingResult.UnallocatedDevices,
                    CalculationTime = balancingResult.CalculationTime,
                    AlgorithmUsed = balancingResult.AlgorithmUsed,
                    Circuits = balancingResult.Circuits.Select(c => new CircuitSummary
                    {
                        CircuitId = c.CircuitId,
                        DeviceCount = c.DeviceCount,
                        TotalCurrent = c.TotalCurrent,
                        TotalUnitLoads = c.TotalUnitLoads,
                        PrimaryLevel = c.PrimaryLevel,
                        CurrentUtilization = c.CurrentUtilization(capacity),
                        UnitLoadUtilization = c.UnitLoadUtilization(capacity),
                        MaxUtilization = c.MaxUtilization(capacity)
                    }).ToList(),
                    Warnings = balancingResult.Warnings,
                    OptimizationEnabled = useOptimization
                };
            }
            catch (Exception ex)
            {
                return new CircuitBalancingResults
                {
                    Success = false,
                    Message = $"Circuit balancing failed: {ex.Message}",
                    Warnings = new List<string> { ex.ToString() }
                };
            }
        }

        public class CircuitBalancingResults
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int TotalCircuits { get; set; }
            public double AverageUtilization { get; set; }
            public double EfficiencyScore { get; set; }
            public int UnallocatedDevices { get; set; }
            public TimeSpan CalculationTime { get; set; }
            public string AlgorithmUsed { get; set; }
            public List<CircuitSummary> Circuits { get; set; } = new List<CircuitSummary>();
            public List<string> Warnings { get; set; } = new List<string>();
            public bool OptimizationEnabled { get; set; }
        }

        public class CircuitSummary
        {
            public int CircuitId { get; set; }
            public int DeviceCount { get; set; }
            public double TotalCurrent { get; set; }
            public int TotalUnitLoads { get; set; }
            public string PrimaryLevel { get; set; }
            public double CurrentUtilization { get; set; }
            public double UnitLoadUtilization { get; set; }
            public double MaxUtilization { get; set; }
            public string Status => MaxUtilization > 0.95 ? "Near Capacity" : 
                                   MaxUtilization > 0.80 ? "High Load" :
                                   MaxUtilization > 0.60 ? "Balanced" : "Low Load";
        }
    }
}