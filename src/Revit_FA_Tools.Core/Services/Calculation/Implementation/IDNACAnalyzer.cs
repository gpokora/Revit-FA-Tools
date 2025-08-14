using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Revit_FA_Tools
{
    public class IDNACAnalyzer
    {
        /// <summary>
        /// Get current capacity policy from configuration
        /// </summary>
        private static FireAlarmConfiguration.CapacityPolicy GetCapacityPolicy() => ConfigurationService.Current.Capacity;
        
        /// <summary>
        /// Get spare policy from configuration
        /// </summary>
        private static FireAlarmConfiguration.SparePolicy GetSparePolicy() => ConfigurationService.Current.Spare;

        // Floor combination optimization thresholds
        private const double TARGET_UTILIZATION_MIN = 0.60;
        private const double TARGET_UTILIZATION_MAX = 0.80;
        private const double UNDERUTILIZED_THRESHOLD = 0.60;

        public IDNACSystemResults AnalyzeIDNACRequirements(ElectricalResults results, string scope)
        {
            var systemResults = new IDNACSystemResults();

            if (results == null)
            {
                return systemResults;
            }

            try
            {
                // Separate fire alarm from other families
                SeparateFamiliesByType(results, systemResults);

                // Check for circuit limit violations with dual limits
                var warnings = CheckDualLimitViolations(systemResults.FireAlarmFamilies, systemResults.OtherFamilies, scope);
                systemResults.Warnings = warnings;

                // Perform level-based analysis if applicable
                if ((scope == "Active View" || scope == "Entire Model") && results.ByLevel != null)
                {
                    var levelData = results.ByLevel;

                    // Apply floor combination optimization with dual limits
                    var optimizedLevelData = CombineUnderutilizedFloorsWithDualLimits(levelData);

                    // Analyze IDNAC requirements with dual limits and repeater islands
                    var levelAnalysis = AnalyzeLevelRequirementsWithDualLimits(optimizedLevelData.Item1);
                    systemResults.LevelAnalysis = levelAnalysis;
                    systemResults.TotalIdnacsNeeded = levelAnalysis.Values.Sum(a => a.IdnacsRequired);
                    systemResults.OptimizationSummary = optimizedLevelData.Item2;
                }
                else
                {
                    // For selection scope, calculate total requirements with dual limits
                    var totalCurrent = systemResults.FireAlarmFamilies.Values.Sum(f => f.Current);
                    var totalDevices = systemResults.FireAlarmFamilies.Values.Sum(f => f.Count);
                    var totalUnitLoads = CalculateUnitLoadsFromFamilies(systemResults.FireAlarmFamilies);

                    systemResults.TotalIdnacsNeeded = CalculateIDNACsNeededWithDualLimits(totalCurrent, totalUnitLoads);
                }
            }
            catch (Exception ex)
            {
                // Add error as warning
                systemResults.Warnings.Add(new IDNACWarning
                {
                    Type = "ANALYSIS ERROR",
                    Severity = "HIGH",
                    Message = $"Error during IDNAC analysis: {ex.Message}",
                    Recommendation = "Please check input data and try again"
                });
            }

            return systemResults;
        }

        /// <summary>
        /// Calculate IDNACs needed with dual limits: 3.0A current AND 139 UL standby
        /// </summary>
        private int CalculateIDNACsNeededWithDualLimits(double totalCurrent, int totalUnitLoads)
        {
            var capacityPolicy = GetCapacityPolicy();
            var sparePolicy = GetSparePolicy();

            // Apply spare capacity reduction if enforced
            var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA;
            var ulLimit = capacityPolicy.IdnacStandbyUnitLoadLimit;
            
            if (sparePolicy.EnforceOnCurrent)
            {
                currentLimit *= (1 - sparePolicy.SpareFractionDefault);
            }
            
            if (sparePolicy.EnforceOnUL)
            {
                ulLimit = (int)(ulLimit * (1 - sparePolicy.SpareFractionDefault));
            }

            // Calculate IDNACs needed for each constraint
            var idnacsForCurrent = totalCurrent > 0 ? (int)Math.Ceiling(totalCurrent / currentLimit) : 0;
            var idnacsForUL = totalUnitLoads > 0 ? (int)Math.Ceiling((double)totalUnitLoads / ulLimit) : 0;

            // Return the maximum (limiting factor)
            return Math.Max(idnacsForCurrent, idnacsForUL);
        }

        /// <summary>
        /// Estimate unit loads from family data (legacy support)
        /// </summary>
        private int CalculateUnitLoadsFromFamilies(Dictionary<string, FamilyData> families)
        {
            // Simple heuristic: most devices = 1 UL, isolators = 4 UL
            int totalUL = 0;
            
            foreach (var kvp in families)
            {
                var familyName = kvp.Key?.ToLower() ?? "";
                var family = kvp.Value;
                if (familyName.Contains("isolator"))
                {
                    totalUL += family.Count * 4; // Isolators = 4 UL each
                }
                else if (familyName.Contains("repeater"))
                {
                    var repeaterPolicy = ConfigurationService.Current.Repeater;
                    totalUL += family.Count * repeaterPolicy.RepeaterUnitLoad; // Configurable repeater UL
                }
                else
                {
                    totalUL += family.Count * 1; // Most devices = 1 UL each
                }
            }
            
            return totalUL;
        }

        /// <summary>
        /// Floor combination with dual limits support
        /// </summary>
        private Tuple<Dictionary<string, LevelData>, OptimizationSummary> CombineUnderutilizedFloorsWithDualLimits(Dictionary<string, LevelData> levelData)
        {
            // For now, delegate to original method - can enhance later with UL-aware optimization
            return CombineUnderutilizedFloors(levelData);
        }

        private Tuple<Dictionary<string, LevelData>, OptimizationSummary> CombineUnderutilizedFloors(Dictionary<string, LevelData> levelData)
        {
            if (levelData == null || !levelData.Any())
            {
                var emptyOptimization = new OptimizationSummary
                {
                    OriginalFloors = 0,
                    OptimizedFloors = 0,
                    Reduction = 0,
                    ReductionPercent = 0,
                    CombinedFloors = new List<Tuple<string, LevelData>>()
                };
                return new Tuple<Dictionary<string, LevelData>, OptimizationSummary>(new Dictionary<string, LevelData>(), emptyOptimization);
            }

            try
            {
                // Filter out levels with no devices
                var levelsWithDevices = levelData.Where(l => l.Value?.Devices > 0).ToDictionary(l => l.Key, l => l.Value);
                var originalFloorCount = levelsWithDevices.Count;

                if (originalFloorCount == 0)
                {
                    return new Tuple<Dictionary<string, LevelData>, OptimizationSummary>(
                        levelData,
                        new OptimizationSummary { OriginalFloors = 0, OptimizedFloors = 0, Reduction = 0, ReductionPercent = 0, CombinedFloors = new List<Tuple<string, LevelData>>() }
                    );
                }

                // Sort levels by building order (parking, main, villa, mechanical)
                var sortedLevels = levelsWithDevices.OrderBy(l => GetLevelSortKey(l.Key)).ToList();

                // Identify underutilized floors (candidates for combination)
                var candidates = IdentifyUnderutilizedFloors(sortedLevels);

                // Perform floor combination optimization
                var optimizedLevels = OptimizeFloorCombinations(sortedLevels, candidates);

                // Calculate optimization summary
                var optimizedFloorCount = optimizedLevels.Count;
                var reduction = originalFloorCount - optimizedFloorCount;
                var reductionPercent = originalFloorCount > 0 ? (double)reduction / originalFloorCount * 100 : 0;

                // Find combined floors for reporting
                var combinedFloors = optimizedLevels.Where(l => l.Value.Combined).Select(l => new Tuple<string, LevelData>(l.Key, l.Value)).ToList();

                var optimizationSummary = new OptimizationSummary
                {
                    OriginalFloors = originalFloorCount,
                    OptimizedFloors = optimizedFloorCount,
                    Reduction = reduction,
                    ReductionPercent = reductionPercent,
                    CombinedFloors = combinedFloors
                };

                return new Tuple<Dictionary<string, LevelData>, OptimizationSummary>(optimizedLevels, optimizationSummary);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in floor combination optimization: {ex.Message}");

                // Return original data on error
                var originalCount = levelData.Count(l => l.Value?.Devices > 0);
                var errorOptimization = new OptimizationSummary
                {
                    OriginalFloors = originalCount,
                    OptimizedFloors = originalCount,
                    Reduction = 0,
                    ReductionPercent = 0,
                    CombinedFloors = new List<Tuple<string, LevelData>>()
                };
                return new Tuple<Dictionary<string, LevelData>, OptimizationSummary>(levelData, errorOptimization);
            }
        }

        private List<KeyValuePair<string, LevelData>> IdentifyUnderutilizedFloors(List<KeyValuePair<string, LevelData>> sortedLevels)
        {
            var candidates = new List<KeyValuePair<string, LevelData>>();

            foreach (var level in sortedLevels)
            {
                if (level.Value == null || level.Value.Devices == 0)
                    continue;

                // Calculate current utilization for this floor
                var currentUtilization = CalculateUtilization(level.Value.Current, level.Value.Devices);

                // Floor is a candidate if utilization is below threshold
                if (currentUtilization < UNDERUTILIZED_THRESHOLD)
                {
                    candidates.Add(level);
                }
            }

            return candidates;
        }

        private Dictionary<string, LevelData> OptimizeFloorCombinations(
            List<KeyValuePair<string, LevelData>> sortedLevels,
            List<KeyValuePair<string, LevelData>> candidates)
        {
            var optimizedLevels = new Dictionary<string, LevelData>();
            var processedLevels = new HashSet<string>();

            foreach (var level in sortedLevels)
            {
                if (processedLevels.Contains(level.Key))
                    continue;

                // If this level is not a candidate for combination, add it as-is
                if (!candidates.Any(c => c.Key == level.Key))
                {
                    optimizedLevels[level.Key] = CloneLevelData(level.Value);
                    processedLevels.Add(level.Key);
                    continue;
                }

                // Try to combine this level with adjacent underutilized levels
                var combinationResult = AttemptFloorCombination(level, sortedLevels, candidates, processedLevels);

                if (combinationResult.Item1) // Successful combination
                {
                    var combinedLevel = combinationResult.Item2;
                    optimizedLevels[combinedLevel.Key] = combinedLevel.Value;

                    // Mark all combined levels as processed
                    foreach (var originalFloor in combinedLevel.Value.OriginalFloors)
                    {
                        processedLevels.Add(originalFloor);
                    }
                }
                else
                {
                    // No successful combination, add original level
                    optimizedLevels[level.Key] = CloneLevelData(level.Value);
                    processedLevels.Add(level.Key);
                }
            }

            return optimizedLevels;
        }

        private Tuple<bool, KeyValuePair<string, LevelData>> AttemptFloorCombination(
            KeyValuePair<string, LevelData> primaryLevel,
            List<KeyValuePair<string, LevelData>> allLevels,
            List<KeyValuePair<string, LevelData>> candidates,
            HashSet<string> processedLevels)
        {
            var combinedCurrent = primaryLevel.Value.Current;
            var combinedDevices = primaryLevel.Value.Devices;
            var combinedWattage = primaryLevel.Value.Wattage;
            var combinedFamilies = new Dictionary<string, int>(primaryLevel.Value.Families);
            var originalFloors = new List<string> { primaryLevel.Key };

            // Find adjacent levels in the same building zone that are candidates for combination
            var adjacentCandidates = FindAdjacentCandidates(primaryLevel, allLevels, candidates, processedLevels);

            foreach (var adjacent in adjacentCandidates)
            {
                var potentialCurrent = combinedCurrent + adjacent.Value.Current;
                var potentialDevices = combinedDevices + adjacent.Value.Devices;
                var potentialWattage = combinedWattage + adjacent.Value.Wattage;

                // Check if combination stays within IDNAC dual limits
                var capacityPolicy = GetCapacityPolicy();
                var sparePolicy = GetSparePolicy();
                
                var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA;
                var ulLimit = capacityPolicy.IdnacStandbyUnitLoadLimit;
                
                if (sparePolicy.EnforceOnCurrent) currentLimit *= (1 - sparePolicy.SpareFractionDefault);
                if (sparePolicy.EnforceOnUL) ulLimit = (int)(ulLimit * (1 - sparePolicy.SpareFractionDefault));

                if (potentialCurrent <= currentLimit && potentialDevices <= ulLimit)
                {
                    // Check if combined utilization is within target range
                    var combinedUtilization = CalculateUtilization(potentialCurrent, potentialDevices);
                    if (combinedUtilization <= TARGET_UTILIZATION_MAX)
                    {
                        // Accept this combination
                        combinedCurrent = potentialCurrent;
                        combinedDevices = potentialDevices;
                        combinedWattage = potentialWattage;
                        originalFloors.Add(adjacent.Key);

                        // Merge family counts
                        foreach (var family in adjacent.Value.Families)
                        {
                            if (combinedFamilies.ContainsKey(family.Key))
                                combinedFamilies[family.Key] += family.Value;
                            else
                                combinedFamilies[family.Key] = family.Value;
                        }
                    }
                }
            }

            // If we combined multiple floors, create the combined level
            if (originalFloors.Count > 1)
            {
                var combinedUtilization = CalculateUtilization(combinedCurrent, combinedDevices);
                var combinedName = GenerateCombinedLevelName(originalFloors);

                var combinedLevelData = new LevelData
                {
                    Devices = combinedDevices,
                    Current = combinedCurrent,
                    Wattage = combinedWattage,
                    Families = combinedFamilies,
                    Combined = true,
                    RequiresIsolators = true, // Combined floors always require isolators
                    OriginalFloors = originalFloors,
                    UtilizationPercent = combinedUtilization * 100
                };

                return new Tuple<bool, KeyValuePair<string, LevelData>>(
                    true,
                    new KeyValuePair<string, LevelData>(combinedName, combinedLevelData)
                );
            }

            return new Tuple<bool, KeyValuePair<string, LevelData>>(false, primaryLevel);
        }

        private List<KeyValuePair<string, LevelData>> FindAdjacentCandidates(
            KeyValuePair<string, LevelData> primaryLevel,
            List<KeyValuePair<string, LevelData>> allLevels,
            List<KeyValuePair<string, LevelData>> candidates,
            HashSet<string> processedLevels)
        {
            var adjacentCandidates = new List<KeyValuePair<string, LevelData>>();
            var primarySortKey = GetLevelSortKey(primaryLevel.Key);
            var primaryCategory = GetLevelCategory(primaryLevel.Key);

            foreach (var candidate in candidates)
            {
                if (candidate.Key == primaryLevel.Key || processedLevels.Contains(candidate.Key))
                    continue;

                var candidateSortKey = GetLevelSortKey(candidate.Key);
                var candidateCategory = GetLevelCategory(candidate.Key);

                // Only combine levels in the same category (parking with parking, main with main, etc.)
                if (candidateCategory == primaryCategory)
                {
                    // Check if levels are adjacent (within reasonable range)
                    var distance = Math.Abs(candidateSortKey - primarySortKey);
                    if (distance <= 100) // Adjust this threshold as needed
                    {
                        adjacentCandidates.Add(candidate);
                    }
                }
            }

            // Sort by proximity to primary level
            return adjacentCandidates.OrderBy(c => Math.Abs(GetLevelSortKey(c.Key) - primarySortKey)).ToList();
        }

        private double CalculateUtilization(double current, int devices)
        {
            return CalculateUtilizationWithDualLimits(current, devices, devices); // Assume devices = unit loads
        }

        /// <summary>
        /// Calculate utilization with dual limits (current AND unit loads)
        /// </summary>
        private double CalculateUtilizationWithDualLimits(double current, int devices, int unitLoads)
        {
            var capacityPolicy = GetCapacityPolicy();
            var sparePolicy = GetSparePolicy();

            // Get effective limits with spare capacity
            var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA;
            var ulLimit = capacityPolicy.IdnacStandbyUnitLoadLimit;
            
            if (sparePolicy.EnforceOnCurrent)
            {
                currentLimit *= (1 - sparePolicy.SpareFractionDefault);
            }
            
            if (sparePolicy.EnforceOnUL)
            {
                ulLimit = (int)(ulLimit * (1 - sparePolicy.SpareFractionDefault));
            }

            var currentUtilization = currentLimit > 0 ? current / currentLimit : 0;
            var ulUtilization = ulLimit > 0 ? (double)unitLoads / ulLimit : 0;

            // Return the maximum (limiting factor)
            return Math.Max(currentUtilization, ulUtilization);
        }

        private string GetLevelCategory(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return "unknown";

            var levelUpper = levelName.ToUpper();

            if (levelUpper.Contains("LEVEL P") || levelUpper.Contains("PARKING"))
                return "parking";
            else if (levelUpper.Contains("VILLA"))
                return "villa";
            else if (levelUpper.Contains("MECH") || levelUpper.Contains("ROOF"))
                return "mechanical";
            else
                return "main";
        }

        private string GenerateCombinedLevelName(List<string> originalFloors)
        {
            if (originalFloors.Count <= 1)
                return originalFloors.FirstOrDefault() ?? "Combined Level";

            // Sort floors for consistent naming
            var sortedFloors = originalFloors.OrderBy(GetLevelSortKey).ToList();

            if (sortedFloors.Count == 2)
            {
                return $"{sortedFloors[0]} + {sortedFloors[1]}";
            }
            else if (sortedFloors.Count <= 4)
            {
                return string.Join(" + ", sortedFloors);
            }
            else
            {
                return $"{sortedFloors.First()} to {sortedFloors.Last()} ({sortedFloors.Count} floors)";
            }
        }

        private LevelData CloneLevelData(LevelData original)
        {
            if (original == null)
                return new LevelData();

            return new LevelData
            {
                Devices = original.Devices,
                Current = original.Current,
                Wattage = original.Wattage,
                Families = new Dictionary<string, int>(original.Families),
                Combined = original.Combined,
                RequiresIsolators = original.RequiresIsolators,
                OriginalFloors = new List<string>(original.OriginalFloors ?? new List<string>()),
                UtilizationPercent = original.UtilizationPercent
            };
        }

        private double GetLevelSortKey(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return 9999;

            try
            {
                string levelUpper = levelName.ToUpper().Trim();

                // Handle basement levels (B1, B2, etc.)
                if (levelUpper.Contains("BASEMENT") || Regex.IsMatch(levelUpper, @"\bB\d+"))
                {
                    var match = Regex.Match(levelUpper, @"B\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int basementNum))
                    {
                        return -1000 - basementNum;
                    }
                    return -1000;
                }

                // Handle parking levels (P1, P2, etc.)
                if (levelUpper.Contains("PARK") || levelUpper.Contains("LEVEL P"))
                {
                    var match = Regex.Match(levelUpper, @"P\s*(\d+)\.?(\d*)");
                    if (match.Success)
                    {
                        int.TryParse(match.Groups[1].Value, out int mainNum);
                        double.TryParse("0." + match.Groups[2].Value, out double subNum);
                        return -500 + mainNum + subNum;
                    }
                    return -500;
                }

                // Handle ground/lobby levels
                if (levelUpper.Contains("GROUND") || levelUpper.Contains("LOBBY") || levelUpper == "LEVEL 1")
                {
                    return 0;
                }

                // Handle main building levels
                var levelMatch = Regex.Match(levelUpper, @"LEVEL\s+(\d+)");
                if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out int levelNum))
                {
                    return 1000 + levelNum;
                }

                // Handle villa levels
                if (levelUpper.Contains("VILLA"))
                {
                    var match = Regex.Match(levelUpper, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int villaNum))
                    {
                        return 5000 + villaNum;
                    }
                    return 5000;
                }

                // Handle mechanical/roof levels
                if (levelUpper.Contains("MECH") || levelUpper.Contains("ROOF"))
                {
                    var match = Regex.Match(levelUpper, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int mechNum))
                    {
                        return 9000 + mechNum;
                    }
                    return 9000;
                }

                // Default: extract any number
                var defaultMatch = Regex.Match(levelUpper, @"(\d+)");
                if (defaultMatch.Success && int.TryParse(defaultMatch.Groups[1].Value, out int num))
                {
                    return 2000 + num;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sorting level {levelName}: {ex.Message}");
            }

            return 9999;
        }


        private void SeparateFamiliesByType(ElectricalResults results, IDNACSystemResults systemResults)
        {
            if (results?.ByFamily == null)
                return;

            foreach (var family in results.ByFamily)
            {
                var familyName = family.Key;
                var familyData = family.Value;

                if (string.IsNullOrEmpty(familyName) || familyData == null)
                    continue;

                var familyLower = familyName.ToLower();

                bool isFireAlarm = new[] { "fire", "alarm", "notification", "strobe", "horn", "speaker",
                                         "smoke", "fa-", "fire alarm", "addressable" }
                    .Any(keyword => familyLower.Contains(keyword));

                if (isFireAlarm)
                {
                    systemResults.FireAlarmFamilies[familyName] = familyData;
                }
                else
                {
                    systemResults.OtherFamilies[familyName] = familyData;
                }
            }
        }

        /// <summary>
        /// Check circuit limits with dual enforcement (3.0A current AND 139 UL standby)
        /// </summary>
        private List<IDNACWarning> CheckDualLimitViolations(Dictionary<string, FamilyData> faFamilies,
            Dictionary<string, FamilyData> otherFamilies, string scope)
        {
            // For now, delegate to existing method - can enhance with UL checking later
            return CheckCircuitLimits(faFamilies, otherFamilies, scope);
        }

        private List<IDNACWarning> CheckCircuitLimits(Dictionary<string, FamilyData> faFamilies,
            Dictionary<string, FamilyData> otherFamilies, string scope)
        {
            var warnings = new List<IDNACWarning>();

            if (faFamilies == null || !faFamilies.Any())
            {
                return warnings;
            }

            try
            {
                var totalFaCurrent = faFamilies.Values.Sum(data => data?.Current ?? 0);
                var totalFaDevices = faFamilies.Values.Sum(data => data?.Count ?? 0);

                var totalUnitLoads = CalculateUnitLoadsFromFamilies(faFamilies);
                var idnacsNeeded = CalculateIDNACsNeededWithDualLimits(totalFaCurrent, totalUnitLoads);

                if (scope == "Selection")
                {
                    // For selections, check IDNAC capacity with spare capacity warnings
                    var capacityPolicy = GetCapacityPolicy();
                    var sparePolicy = GetSparePolicy();
                    
                    var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA * (1 - sparePolicy.SpareFractionDefault);
                    var ulLimit = (int)(capacityPolicy.IdnacStandbyUnitLoadLimit * (1 - sparePolicy.SpareFractionDefault));
                    
                    if (totalFaCurrent > currentLimit || totalUnitLoads > ulLimit)
                    {
                        var limitingFactor = totalFaCurrent > currentLimit ? "current" : "unit loads";

                        // Determine severity based on how much over limit
                        string severity;
                        string message;
                        if (totalFaCurrent > capacityPolicy.IdnacAlarmCurrentLimitA || totalUnitLoads > capacityPolicy.IdnacStandbyUnitLoadLimit)
                        {
                            severity = "HIGH";
                            message = "Selected devices exceed IDNAC MAXIMUM capacity";
                        }
                        else
                        {
                            severity = "MEDIUM";
                            message = $"Selected devices exceed IDNAC usable capacity ({sparePolicy.SpareFractionDefault*100:F0}% spare consumed)";
                        }

                        warnings.Add(new IDNACWarning
                        {
                            Type = "SELECTED DEVICES EXCEED IDNAC CAPACITY",
                            Severity = severity,
                            Message = $"{message} - {limitingFactor} limit exceeded ({totalFaCurrent:F2}A/{totalUnitLoads} UL vs {currentLimit:F1}A/{ulLimit} UL usable)",
                            Recommendation = $"Split selection across {idnacsNeeded} IDNACs or reduce device count",
                            Details = new Dictionary<string, object>
                            {
                                ["total_current"] = totalFaCurrent,
                                ["total_unit_loads"] = totalUnitLoads,
                                ["idnac_current_usable"] = currentLimit,
                                ["idnac_current_max"] = capacityPolicy.IdnacAlarmCurrentLimitA,
                                ["idnac_ul_usable"] = ulLimit,
                                ["idnac_ul_max"] = capacityPolicy.IdnacStandbyUnitLoadLimit,
                                ["idnacs_needed"] = idnacsNeeded,
                                ["limiting_factor"] = limitingFactor,
                                ["spare_capacity_percent"] = sparePolicy.SpareFractionDefault * 100
                            }
                        });
                    }
                }
                else
                {
                    // For building-wide analysis, provide IDNAC planning info with configurable spare capacity
                    if (idnacsNeeded > 1)
                    {
                        var powerSupplyConfig = ConfigurationService.Current.PowerSupply;
                        var sparePolicy = GetSparePolicy();
                        var powerSuppliesNeeded = (int)Math.Ceiling((double)idnacsNeeded / powerSupplyConfig.IDNACCircuitsPerPS);
                        
                        warnings.Add(new IDNACWarning
                        {
                            Type = "BUILDING REQUIRES MULTIPLE IDNACS",
                            Severity = "INFO",
                            Message = $"Building fire alarm load requires {idnacsNeeded} IDNACs across {powerSuppliesNeeded} ES-PS power supplies (with {sparePolicy.SpareFractionDefault*100:F0}% spare)",
                            Recommendation = "Plan panel placement and IDNAC organization by building levels/zones",
                            Details = new Dictionary<string, object>
                            {
                                ["total_current"] = totalFaCurrent,
                                ["total_unit_loads"] = totalUnitLoads,
                                ["idnacs_needed"] = idnacsNeeded,
                                ["power_supplies_needed"] = powerSuppliesNeeded,
                                ["idnacs_per_power_supply"] = powerSupplyConfig.IDNACCircuitsPerPS,
                                ["spare_capacity_percent"] = sparePolicy.SpareFractionDefault * 100
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add(new IDNACWarning
                {
                    Type = "CIRCUIT LIMIT CHECK ERROR",
                    Severity = "HIGH",
                    Message = $"Error checking circuit limits: {ex.Message}",
                    Recommendation = "Please verify input data and try again"
                });
            }

            return warnings;
        }

        /// <summary>
        /// Analyze level requirements with dual limits (3.0A current AND 139 UL standby) and repeater islands
        /// </summary>
        private Dictionary<string, IDNACAnalysisResult> AnalyzeLevelRequirementsWithDualLimits(Dictionary<string, LevelData> levelData)
        {
            var analysis = new Dictionary<string, IDNACAnalysisResult>();

            if (levelData == null)
                return analysis;

            foreach (var level in levelData)
            {
                try
                {
                    var levelName = level.Key;
                    var levelInfo = level.Value;

                    if (levelInfo == null)
                        continue;

                    var current = levelInfo.Current;
                    var devices = levelInfo.Devices;
                    var unitLoads = devices; // Assume 1 UL per device for legacy compatibility

                    // Check for repeater islands - if level has repeaters, they get fresh budget
                    var hasRepeaters = CheckForRepeaters(levelInfo);
                    var repeaterPolicy = ConfigurationService.Current.Repeater;
                    
                    if (hasRepeaters && repeaterPolicy.TreatRepeaterAsFreshBudget)
                    {
                        // Repeater islands get fresh 3.0A/139 UL budget
                        analysis[levelName] = AnalyzeRepeaterIsland(levelName, levelInfo, current, unitLoads);
                    }
                    else
                    {
                        // Regular IDNAC analysis with dual limits
                        analysis[levelName] = AnalyzeRegularLevel(levelName, levelInfo, current, unitLoads);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error analyzing level {level.Key}: {ex.Message}");
                    analysis[level.Key] = CreateErrorAnalysis(level.Key, level.Value, ex);
                }
            }

            return analysis;
        }

        /// <summary>
        /// Check if level contains repeater devices
        /// </summary>
        private bool CheckForRepeaters(LevelData levelInfo)
        {
            if (levelInfo.Families == null) return false;
            
            return levelInfo.Families.Keys.Any(familyName => 
                familyName?.ToLower().Contains("repeater") == true);
        }

        /// <summary>
        /// Analyze repeater island with fresh capacity budget
        /// </summary>
        private IDNACAnalysisResult AnalyzeRepeaterIsland(string levelName, LevelData levelInfo, double current, int unitLoads)
        {
            var capacityPolicy = GetCapacityPolicy();
            var sparePolicy = GetSparePolicy();

            // Fresh capacity budget for repeater island
            var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA;
            var ulLimit = capacityPolicy.IdnacStandbyUnitLoadLimit;
            
            if (sparePolicy.EnforceOnCurrent) currentLimit *= (1 - sparePolicy.SpareFractionDefault);
            if (sparePolicy.EnforceOnUL) ulLimit = (int)(ulLimit * (1 - sparePolicy.SpareFractionDefault));

            var idnacsForCurrent = current > 0 ? (int)Math.Ceiling(current / currentLimit) : 0;
            var idnacsForUL = unitLoads > 0 ? (int)Math.Ceiling((double)unitLoads / ulLimit) : 0;
            var idnacsRequired = Math.Max(idnacsForCurrent, idnacsForUL);

            var limitingFactor = idnacsForCurrent >= idnacsForUL ? 
                $"Current ({current:F2}A)" : $"Unit Loads ({unitLoads} UL)";

            return new IDNACAnalysisResult
            {
                IdnacsRequired = idnacsRequired,
                Status = $"{idnacsRequired} IDNAC(s) [REPEATER ISLAND]",
                LimitingFactor = $"{limitingFactor} - Fresh Budget",
                Current = current,
                Wattage = levelInfo.Wattage,
                Devices = levelInfo.Devices,
                UnitLoads = unitLoads,
                SpareInfo = CalculateSpareCapacity(current, unitLoads, idnacsRequired),
                IsCombined = levelInfo.Combined,
                OriginalFloors = levelInfo.OriginalFloors ?? new List<string>(),
                RequiresIsolators = true // Repeater islands always need isolators
            };
        }

        /// <summary>
        /// Analyze regular level with dual limits
        /// </summary>
        private IDNACAnalysisResult AnalyzeRegularLevel(string levelName, LevelData levelInfo, double current, int unitLoads)
        {
            var idnacsRequired = CalculateIDNACsNeededWithDualLimits(current, unitLoads);
            
            var capacityPolicy = GetCapacityPolicy();
            var currentLimit = capacityPolicy.IdnacAlarmCurrentLimitA;
            var ulLimit = capacityPolicy.IdnacStandbyUnitLoadLimit;
            
            var idnacsForCurrent = current > 0 ? (int)Math.Ceiling(current / currentLimit) : 0;
            var idnacsForUL = unitLoads > 0 ? (int)Math.Ceiling((double)unitLoads / ulLimit) : 0;

            var limitingFactor = idnacsForCurrent >= idnacsForUL ? 
                $"Current ({current:F2}A requires {idnacsForCurrent} IDNACs)" : 
                $"Unit Loads ({unitLoads} UL requires {idnacsForUL} IDNACs)";

            var utilization = CalculateUtilizationWithDualLimits(current, levelInfo.Devices, unitLoads);
            var status = GenerateStatusString(idnacsRequired, utilization, levelInfo.Combined);

            return new IDNACAnalysisResult
            {
                IdnacsRequired = idnacsRequired,
                Status = status,
                LimitingFactor = limitingFactor,
                Current = current,
                Wattage = levelInfo.Wattage,
                Devices = levelInfo.Devices,
                UnitLoads = unitLoads,
                SpareInfo = CalculateSpareCapacity(current, unitLoads, idnacsRequired),
                IsCombined = levelInfo.Combined,
                OriginalFloors = levelInfo.OriginalFloors ?? new List<string>(),
                RequiresIsolators = levelInfo.RequiresIsolators
            };
        }

        private SpareCapacityInfo CalculateSpareCapacity(double current, int unitLoads, int idnacsRequired)
        {
            var capacityPolicy = GetCapacityPolicy();
            
            if (idnacsRequired > 0)
            {
                var totalCurrentCapacity = idnacsRequired * capacityPolicy.IdnacAlarmCurrentLimitA;
                var totalULCapacity = idnacsRequired * capacityPolicy.IdnacStandbyUnitLoadLimit;

                return new SpareCapacityInfo
                {
                    SpareCurrent = Math.Max(0, totalCurrentCapacity - current),
                    SpareDevices = Math.Max(0, totalULCapacity - unitLoads), // Using UL as device proxy
                    CurrentUtilization = totalCurrentCapacity > 0 ? (current / totalCurrentCapacity * 100) : 0,
                    DeviceUtilization = totalULCapacity > 0 ? ((double)unitLoads / totalULCapacity * 100) : 0
                };
            }

            return new SpareCapacityInfo();
        }

        private string GenerateStatusString(int idnacsRequired, double utilization, bool isCombined)
        {
            if (idnacsRequired == 0) return "No devices";

            var utilizationPercent = utilization * 100;
            
            if (idnacsRequired == 1)
            {
                if (isCombined) return $"1 IDNAC [OPTIMIZED] ({utilizationPercent:F0}% utilized)";
                if (utilizationPercent <= 60) return $"1 IDNAC [UNDERUTILIZED] ({utilizationPercent:F0}% utilized)";
                if (utilizationPercent <= 80) return $"1 IDNAC [GOOD] ({utilizationPercent:F0}% utilized)";
                if (utilizationPercent <= 95) return $"1 IDNAC [EXCELLENT] ({utilizationPercent:F0}% utilized)";
                return $"1 IDNAC [NEAR LIMIT] ({utilizationPercent:F0}% utilized)";
            }

            return $"{idnacsRequired} IDNACs [MULTIPLE] ({utilizationPercent:F0}% avg util)";
        }

        private IDNACAnalysisResult CreateErrorAnalysis(string levelName, LevelData levelData, Exception ex)
        {
            return new IDNACAnalysisResult
            {
                IdnacsRequired = 0,
                Status = "ANALYSIS ERROR",
                LimitingFactor = $"Error: {ex.Message}",
                Current = levelData?.Current ?? 0,
                Wattage = levelData?.Wattage ?? 0,
                Devices = levelData?.Devices ?? 0,
                UnitLoads = levelData?.Devices ?? 0,
                SpareInfo = new SpareCapacityInfo(),
                IsCombined = false,
                OriginalFloors = new List<string>(),
                RequiresIsolators = false
            };
        }

        private Dictionary<string, IDNACAnalysisResult> AnalyzeLevelRequirements(Dictionary<string, LevelData> levelData)
        {
            var analysis = new Dictionary<string, IDNACAnalysisResult>();

            if (levelData == null)
                return analysis;

            foreach (var level in levelData)
            {
                try
                {
                    var levelName = level.Key;
                    var levelInfo = level.Value;

                    if (levelInfo == null)
                        continue;

                    var current = levelInfo.Current;
                    var devices = levelInfo.Devices;
                    var unitLoads = devices; // Assume 1 unit load per device

                    // FIXED: Add validation for impossible current values
                    if (current > ConfigurationService.Current.IDNAC.MaxCurrent * 10) // Sanity check: max 10 IDNACs worth
                    {
                        analysis[levelName] = new IDNACAnalysisResult
                        {
                            IdnacsRequired = 0,
                            Status = "ERROR - Current too high",
                            LimitingFactor = $"Current {current:F2}A exceeds system limits",
                            Current = current,
                            Wattage = levelInfo.Wattage,
                            Devices = devices,
                            UnitLoads = unitLoads,
                            SpareInfo = new SpareCapacityInfo(),
                            IsCombined = levelInfo.Combined,
                            OriginalFloors = levelInfo.OriginalFloors ?? new List<string>(),
                            RequiresIsolators = levelInfo.RequiresIsolators
                        };
                        continue;
                    }

                    // Calculate IDNACs needed for each constraint (using USABLE limits with 20% spare capacity)
                    var config = ConfigurationService.Current;
                    var usableCurrent = config.IDNAC.MaxCurrent * (1 - config.Spare.SpareFractionDefault);
                    var usableDevices = (int)(config.IDNAC.MaxDevicesStandard * (1 - config.Spare.SpareFractionDefault));
                    var usableUnitLoads = (int)(139 * (1 - config.Spare.SpareFractionDefault)); // Standard UL limit is 139
                    
                    var idnacsForCurrent = current > 0 ? (int)Math.Ceiling(current / usableCurrent) : 0;
                    var idnacsForDevices = devices > 0 ? (int)Math.Ceiling((double)devices / usableDevices) : 0;
                    var idnacsForUnitLoads = unitLoads > 0 ? (int)Math.Ceiling((double)unitLoads / usableUnitLoads) : 0;

                    // The limiting factor determines IDNACs needed
                    var idnacsRequired = Math.Max(Math.Max(idnacsForCurrent, idnacsForDevices), idnacsForUnitLoads);

                    // FIXED: Determine the actual limiting factor
                    string limitingFactor;
                    bool currentLimited = (idnacsForCurrent >= idnacsForDevices && idnacsForCurrent >= idnacsForUnitLoads);
                    bool deviceLimited = (idnacsForDevices >= idnacsForCurrent && idnacsForDevices >= idnacsForUnitLoads);

                    if (currentLimited)
                    {
                        limitingFactor = $"Current ({current:F2}A requires {idnacsForCurrent} IDNACs)";
                    }
                    else if (deviceLimited)
                    {
                        limitingFactor = $"Device Count ({devices} devices requires {idnacsForDevices} IDNACs)";
                    }
                    else
                    {
                        limitingFactor = $"Unit Loads ({unitLoads} loads requires {idnacsForUnitLoads} IDNACs)";
                    }

                    // FIXED: Calculate correct utilization percentages
                    var isCombined = levelInfo.Combined;
                    var currentUtilization = idnacsRequired > 0 ? (current / (idnacsRequired * usableCurrent) * 100) : 0;
                    var deviceUtilization = idnacsRequired > 0 ? ((double)devices / (idnacsRequired * usableDevices) * 100) : 0;
                    var maxUtilization = Math.Max(currentUtilization, deviceUtilization);

                    // FIXED: Enhanced status description with correct utilization
                    string status;
                    if (idnacsRequired == 0)
                    {
                        status = "No devices";
                    }
                    else if (idnacsRequired == 1)
                    {
                        if (isCombined)
                        {
                            status = $"1 IDNAC [OPTIMIZED] ({maxUtilization:F0}% utilized)";
                        }
                        else if (maxUtilization <= 60)
                        {
                            status = $"1 IDNAC [UNDERUTILIZED] ({maxUtilization:F0}% utilized)";
                        }
                        else if (maxUtilization <= 75)
                        {
                            status = $"1 IDNAC [GOOD] ({maxUtilization:F0}% utilized)";
                        }
                        else if (maxUtilization <= 90)
                        {
                            status = $"1 IDNAC [EXCELLENT] ({maxUtilization:F0}% utilized)";
                        }
                        else
                        {
                            status = $"1 IDNAC [NEAR LIMIT] ({maxUtilization:F0}% utilized)";
                        }
                    }
                    else if (idnacsRequired <= 3)
                    {
                        status = $"{idnacsRequired} IDNACs [MULTIPLE] ({maxUtilization:F0}% avg util)";
                    }
                    else
                    {
                        status = $"{idnacsRequired} IDNACs [HIGH LOAD] ({maxUtilization:F0}% avg util)";
                    }

                    // FIXED: Calculate spare capacity remaining
                    SpareCapacityInfo spareInfo;
                    if (idnacsRequired > 0)
                    {
                        var totalCurrentCapacity = idnacsRequired * usableCurrent;
                        var totalDeviceCapacity = idnacsRequired * usableDevices;

                        spareInfo = new SpareCapacityInfo
                        {
                            SpareCurrent = Math.Max(0, totalCurrentCapacity - current),
                            SpareDevices = Math.Max(0, totalDeviceCapacity - devices),
                            CurrentUtilization = currentUtilization,
                            DeviceUtilization = deviceUtilization
                        };
                    }
                    else
                    {
                        spareInfo = new SpareCapacityInfo();
                    }

                    analysis[levelName] = new IDNACAnalysisResult
                    {
                        IdnacsRequired = idnacsRequired,
                        Status = status,
                        LimitingFactor = limitingFactor,
                        Current = current,
                        Wattage = levelInfo.Wattage,
                        Devices = devices,
                        UnitLoads = unitLoads,
                        SpareInfo = spareInfo,
                        IsCombined = isCombined,
                        OriginalFloors = levelInfo.OriginalFloors ?? new List<string>(),
                        RequiresIsolators = levelInfo.RequiresIsolators
                    };
                }
                catch (Exception ex)
                {
                    // Log error but continue with other levels
                    System.Diagnostics.Debug.WriteLine($"Error analyzing level {level.Key}: {ex.Message}");

                    // Add error entry
                    analysis[level.Key] = new IDNACAnalysisResult
                    {
                        IdnacsRequired = 0,
                        Status = "ANALYSIS ERROR",
                        LimitingFactor = $"Error: {ex.Message}",
                        Current = level.Value?.Current ?? 0,
                        Wattage = level.Value?.Wattage ?? 0,
                        Devices = level.Value?.Devices ?? 0,
                        UnitLoads = level.Value?.Devices ?? 0,
                        SpareInfo = new SpareCapacityInfo(),
                        IsCombined = false,
                        OriginalFloors = new List<string>(),
                        RequiresIsolators = false
                    };
                }
            }

            return analysis;
        }
    }
}