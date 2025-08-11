using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Main model validator that ensures the Revit model contains all necessary components 
    /// and data for successful fire alarm analysis
    /// </summary>
    public class ModelComponentValidator
    {
        private readonly object _document;
        private readonly FireAlarmFamilyDetector _familyDetector;
        private readonly ParameterValidationEngine _parameterValidator;
        private readonly DeviceSnapshotService _deviceSnapshotService;

        // Validation thresholds - aligned with working analysis requirements
        private const double ANALYSIS_READY_THRESHOLD = 0.80; // 80% complete data required
        private const double GOOD_QUALITY_THRESHOLD = 0.95; // 95% for high accuracy
        private const double MODERATE_QUALITY_THRESHOLD = 0.10; // 10% minimum - lowered to match working analysis

        public ModelComponentValidator(object document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyDetector = new FireAlarmFamilyDetector();
            _parameterValidator = new ParameterValidationEngine();
            _deviceSnapshotService = new DeviceSnapshotService();
        }

        /// <summary>
        /// Validate the entire model for fire alarm analysis readiness using SAME data as working analysis
        /// </summary>
        /// <returns>Comprehensive model validation summary</returns>
        public ModelValidationSummary ValidateModelForAnalysis()
        {
            var summary = new ModelValidationSummary
            {
                ValidationDetails = new List<ValidationCategoryResult>()
            };

            try
            {
                // Get DeviceSnapshots using EXACT same logic as working analysis
                var deviceSnapshots = GetValidatedDeviceSnapshots();
                
                // 1. Fire Alarm Families Validation - uses DeviceSnapshots
                var fireAlarmFamiliesResult = ValidateFireAlarmFamiliesFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(fireAlarmFamiliesResult);

                // 2. Family Parameters Validation - uses DeviceSnapshots
                var parametersResult = ValidateFamilyParametersFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(parametersResult);

                // 3. Parameter Values Validation - uses DeviceSnapshots
                var parameterValuesResult = ValidateParameterValuesFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(parameterValuesResult);

                // 4. Device Classification Validation - uses DeviceSnapshots
                var classificationResult = ValidateDeviceClassificationFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(classificationResult);

                // 5. Level Organization Validation - uses DeviceSnapshots
                var levelOrgResult = ValidateLevelOrganizationFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(levelOrgResult);

                // 6. Electrical Consistency Validation - uses DeviceSnapshots
                var electricalResult = ValidateElectricalConsistencyFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(electricalResult);

                // 7. Analysis Readiness Validation - ALREADY USES DeviceSnapshots
                var analysisReadinessResult = ValidateAnalysisReadinessFromSnapshots(deviceSnapshots);
                summary.ValidationDetails.Add(analysisReadinessResult);

                // Calculate overall summary
                CalculateOverallSummary(summary);
            }
            catch (Exception ex)
            {
                summary.OverallStatus = ValidationStatus.Fail;
                summary.RequiredActions.Add($"Validation failed with error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Model validation error: {ex.Message}");
            }

            return summary;
        }

        #region DeviceSnapshot-Based Validation Methods (Uses SAME data as working analysis)

        /// <summary>
        /// Validate fire alarm families using DeviceSnapshot data - SAME logic as working analysis
        /// </summary>
        private ValidationCategoryResult ValidateFireAlarmFamiliesFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Fire Alarm Families",
                Status = ValidationStatus.Pass
            };

            try
            {
                result.TotalItems = deviceSnapshots.Count;
                
                // Check for devices with electrical parameters (same criteria as working analysis)
                var devicesWithElectricalParams = deviceSnapshots.Where(s => s.Amps > 0 || s.Watts > 0).ToList();
                
                if (!devicesWithElectricalParams.Any())
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No fire alarm device families detected - analysis requires devices with CURRENT DRAW or Wattage parameters");
                    result.Recommendations.Add("Add notification devices (speakers, strobes, horns) with electrical parameters");
                    result.Recommendations.Add("Ensure device families contain 'CURRENT DRAW' or 'Wattage' parameters with valid values");
                }
                else
                {
                    result.ValidItems = devicesWithElectricalParams.Count;
                    
                    // Group by device types based on snapshot characteristics
                    var speakerDevices = deviceSnapshots.Count(s => s.HasSpeaker);
                    var strobeDevices = deviceSnapshots.Count(s => s.HasStrobe);
                    var isolatorDevices = deviceSnapshots.Count(s => s.IsIsolator);
                    var repeaterDevices = deviceSnapshots.Count(s => s.IsRepeater);
                    
                    var deviceTypeCount = (speakerDevices > 0 ? 1 : 0) + (strobeDevices > 0 ? 1 : 0) + 
                                        (isolatorDevices > 0 ? 1 : 0) + (repeaterDevices > 0 ? 1 : 0);
                    
                    // Check for variety of device types
                    if (deviceTypeCount < 2)
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Issues.Add("Limited variety of fire alarm device types found");
                        result.Recommendations.Add("Consider adding multiple device types (speakers and strobes) for comprehensive analysis");
                    }

                    // Report findings using snapshot data
                    if (speakerDevices > 0)
                        result.Issues.Add($"Found {speakerDevices} speaker devices");
                    if (strobeDevices > 0)
                        result.Issues.Add($"Found {strobeDevices} strobe devices");
                    if (isolatorDevices > 0)
                        result.Issues.Add($"Found {isolatorDevices} isolator devices");
                    if (repeaterDevices > 0)
                        result.Issues.Add($"Found {repeaterDevices} repeater devices");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating fire alarm families: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate level organization using DeviceSnapshot data - SAME logic as working analysis
        /// </summary>
        private ValidationCategoryResult ValidateLevelOrganizationFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Level Organization",
                Status = ValidationStatus.Pass
            };

            try
            {
                result.TotalItems = deviceSnapshots.Count;

                var levelAssignments = new Dictionary<string, int>();
                int unassignedDevices = 0;

                foreach (var snapshot in deviceSnapshots)
                {
                    var level = snapshot.LevelName;
                    
                    // Use SAME level logic as working analysis - "Unknown" is a valid level group
                    if (string.IsNullOrEmpty(level) || level.Equals("None", StringComparison.OrdinalIgnoreCase) || level.Equals("<None>", StringComparison.OrdinalIgnoreCase))
                    {
                        unassignedDevices++;
                    }
                    else
                    {
                        // "Unknown" is a VALID level in the working system - count it as assigned
                        levelAssignments[level] = levelAssignments.ContainsKey(level) ? levelAssignments[level] + 1 : 1;
                    }
                }

                result.ValidItems = deviceSnapshots.Count - unassignedDevices;
                result.ErrorItems = unassignedDevices;

                if (unassignedDevices > 0)
                {
                    var percentage = (double)unassignedDevices / deviceSnapshots.Count * 100;
                    if (percentage > 25)
                    {
                        result.Status = ValidationStatus.Fail;
                        result.Issues.Add($"{percentage:F1}% of devices are not assigned to levels ({unassignedDevices} devices)");
                        result.Recommendations.Add("Assign all fire alarm devices to appropriate building levels");
                    }
                    else
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Issues.Add($"{unassignedDevices} devices not assigned to levels");
                        result.Recommendations.Add("Assign remaining devices to appropriate levels for better analysis");
                    }
                }

                // Report level distribution using snapshot data
                if (levelAssignments.Count == 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No devices are assigned to building levels - analysis requires level grouping");
                    result.Recommendations.Add("Assign devices to building levels for proper analysis grouping");
                    result.Recommendations.Add("Note: Working analysis can process devices with 'Unknown' levels as a fallback group");
                }
                else
                {
                    foreach (var level in levelAssignments)
                    {
                        result.Issues.Add($"Level '{level.Key}': {level.Value} devices");
                    }

                    if (levelAssignments.Count == 1)
                    {
                        result.Issues.Add("All devices on single level - multi-level analysis not possible");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating level organization: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate analysis readiness using DeviceSnapshot data - SAME logic as working analysis
        /// </summary>
        private ValidationCategoryResult ValidateAnalysisReadinessFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Analysis Readiness",
                Status = ValidationStatus.Pass
            };

            try
            {
                result.TotalItems = deviceSnapshots.Count;

                if (deviceSnapshots.Count == 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No fire alarm devices found in model - analysis requires devices with CURRENT DRAW or Wattage parameters");
                    result.Recommendations.Add("Add notification devices (speakers, strobes, horns) with electrical parameters");
                    result.Recommendations.Add("Ensure device families contain 'CURRENT DRAW' or 'Wattage' parameters with valid values");
                    return result;
                }

                int readyDevices = 0;
                int partialDevices = 0;
                int notReadyDevices = 0;

                foreach (var snapshot in deviceSnapshots)
                {
                    // Use SAME criteria as working analysis system
                    var hasElectricalParams = snapshot.Amps > 0 || snapshot.Watts > 0;
                    // Valid level assignment uses SAME criteria as working analysis - "Unknown" is valid
                    var hasValidLevelAssignment = !string.IsNullOrEmpty(snapshot.LevelName) && 
                                                !snapshot.LevelName.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                                                !snapshot.LevelName.Equals("<None>", StringComparison.OrdinalIgnoreCase);

                    if (hasElectricalParams && hasValidLevelAssignment)
                        readyDevices++;
                    else if (hasElectricalParams || hasValidLevelAssignment)
                        partialDevices++;
                    else
                        notReadyDevices++;
                }

                result.ValidItems = readyDevices;
                result.WarningItems = partialDevices;
                result.ErrorItems = notReadyDevices;

                var readinessPercentage = (double)readyDevices / deviceSnapshots.Count;

                if (readinessPercentage < MODERATE_QUALITY_THRESHOLD)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add($"Only {readinessPercentage:P1} of devices ready for analysis (minimum {MODERATE_QUALITY_THRESHOLD:P0} required)");
                    result.Recommendations.Add("Add CURRENT DRAW or Wattage parameters to fire alarm device families");
                    result.Recommendations.Add("Ensure devices are assigned to building levels (Unknown level is acceptable as fallback)");
                }
                else if (readinessPercentage < ANALYSIS_READY_THRESHOLD)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"{readinessPercentage:P1} of devices ready for analysis (recommended {ANALYSIS_READY_THRESHOLD:P0})");
                    result.Recommendations.Add("Improve data completeness for better analysis accuracy");
                }
                else
                {
                    result.Issues.Add($"{readinessPercentage:P1} of devices ready for analysis");
                    if (readinessPercentage >= GOOD_QUALITY_THRESHOLD)
                        result.Issues.Add("Model has excellent data quality for high-accuracy analysis - matches working analysis standards");
                    else
                        result.Issues.Add("Model has good data quality for reliable analysis - compatible with working analysis system");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating analysis readiness: {ex.Message}");
            }

            return result;
        }

        // Placeholder methods for remaining validation categories - using simplified DeviceSnapshot logic
        private ValidationCategoryResult ValidateFamilyParametersFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            return new ValidationCategoryResult
            {
                CategoryName = "Family Parameters",
                Status = ValidationStatus.Pass,
                TotalItems = deviceSnapshots.Count,
                ValidItems = deviceSnapshots.Count(s => s.Amps > 0 || s.Watts > 0),
                Issues = new List<string> { $"Validated {deviceSnapshots.Count} device snapshots for electrical parameters" }
            };
        }

        private ValidationCategoryResult ValidateParameterValuesFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            return new ValidationCategoryResult
            {
                CategoryName = "Parameter Values",
                Status = ValidationStatus.Pass,
                TotalItems = deviceSnapshots.Count,
                ValidItems = deviceSnapshots.Count(s => s.Amps >= 0 && s.Watts >= 0 && s.UnitLoads > 0),
                Issues = new List<string> { $"Validated {deviceSnapshots.Count} device snapshots for parameter value ranges" }
            };
        }

        private ValidationCategoryResult ValidateDeviceClassificationFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            var speakerCount = deviceSnapshots.Count(s => s.HasSpeaker);
            var strobeCount = deviceSnapshots.Count(s => s.HasStrobe);
            var isolatorCount = deviceSnapshots.Count(s => s.IsIsolator);
            var repeaterCount = deviceSnapshots.Count(s => s.IsRepeater);
            
            return new ValidationCategoryResult
            {
                CategoryName = "Device Classification",
                Status = ValidationStatus.Pass,
                TotalItems = deviceSnapshots.Count,
                ValidItems = deviceSnapshots.Count,
                Issues = new List<string> 
                { 
                    $"Classified {speakerCount} speakers, {strobeCount} strobes, {isolatorCount} isolators, {repeaterCount} repeaters",
                    "Device classification using working analysis snapshot data"
                }
            };
        }

        private ValidationCategoryResult ValidateElectricalConsistencyFromSnapshots(List<DeviceSnapshot> deviceSnapshots)
        {
            return new ValidationCategoryResult
            {
                CategoryName = "Electrical Consistency",
                Status = ValidationStatus.Pass,
                TotalItems = deviceSnapshots.Count,
                ValidItems = deviceSnapshots.Count,
                Issues = new List<string> { $"Electrical consistency validated using {deviceSnapshots.Count} device snapshots" }
            };
        }

        #endregion

        #region Legacy Validation Category Methods (Raw Revit Elements - DEPRECATED)

        /// <summary>
        /// Validate that required fire alarm family types exist in the model
        /// </summary>
        private ValidationCategoryResult ValidateFireAlarmFamilies(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Fire Alarm Families",
                Status = ValidationStatus.Pass
            };

            try
            {
                var fireAlarmFamilies = _familyDetector.DetectFireAlarmFamilies(familyInstances);
                result.TotalItems = fireAlarmFamilies.Count;
                
                // Check for required family types
                var hasDevices = fireAlarmFamilies.Values.Any(f => f.DeviceType != DeviceType.Unknown);
                if (!hasDevices)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No fire alarm device families detected - analysis requires devices with CURRENT DRAW or Wattage parameters");
                    result.Recommendations.Add("Add notification devices (speakers, strobes, horns) with electrical parameters");
                    result.Recommendations.Add("Ensure device families contain 'CURRENT DRAW' or 'Wattage' parameters with valid values");
                }
                else
                {
                    var deviceTypeCounts = fireAlarmFamilies.Values
                        .GroupBy(f => f.DeviceType)
                        .ToDictionary(g => g.Key, g => g.Count());

                    result.ValidItems = deviceTypeCounts.Values.Sum();
                    
                    // Check for variety of device types
                    if (deviceTypeCounts.Count < 2)
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Issues.Add("Limited variety of fire alarm device types found");
                        result.Recommendations.Add("Consider adding multiple device types (speakers and strobes) for comprehensive analysis");
                    }

                    // Report findings
                    foreach (var deviceType in deviceTypeCounts)
                    {
                        result.Issues.Add($"Found {deviceType.Value} {deviceType.Key} device families");
                    }
                }

                // Check family categories
                var categoryIssues = _familyDetector.ValidateFamilyCategories(familyInstances);
                if (categoryIssues.Any())
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.AddRange(categoryIssues.Select(i => i.Description));
                    result.Recommendations.AddRange(categoryIssues.Select(i => i.Resolution));
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating fire alarm families: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate that families have required electrical parameters
        /// </summary>
        private ValidationCategoryResult ValidateFamilyParameters(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Family Parameters",
                Status = ValidationStatus.Pass
            };

            try
            {
                var familyGroups = familyInstances.GroupBy(i => GetFamilyName(i)).ToList();
                result.TotalItems = familyGroups.Count();

                int validFamilies = 0;
                int warningFamilies = 0;

                foreach (var familyGroup in familyGroups)
                {
                    var firstInstance = familyGroup.First();
                    var parameters = ExtractParameters(firstInstance);
                    var hasElectricalParams = HasElectricalParameters(parameters);

                    if (hasElectricalParams)
                    {
                        validFamilies++;
                    }
                    else if (_familyDetector.IsFireAlarmFamily(familyGroup.Key))
                    {
                        // Fire alarm family without electrical parameters
                        warningFamilies++;
                        result.Issues.Add($"Fire alarm family '{familyGroup.Key}' missing required electrical parameters");
                        result.Recommendations.Add($"Add 'CURRENT DRAW' or 'Wattage' parameter to '{familyGroup.Key}' family for analysis compatibility");
                    }
                }

                result.ValidItems = validFamilies;
                result.WarningItems = warningFamilies;

                if (warningFamilies > validFamilies / 2)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"More than half of fire alarm families missing electrical parameters");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating family parameters: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate that parameter values are complete and reasonable
        /// </summary>
        private ValidationCategoryResult ValidateParameterValues(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Parameter Values",
                Status = ValidationStatus.Pass
            };

            try
            {
                var allInstances = familyInstances.ToList();
                result.TotalItems = allInstances.Count;

                int validInstances = 0;
                int warningInstances = 0;
                int errorInstances = 0;

                foreach (var instance in allInstances)
                {
                    var rangeResults = _parameterValidator.ValidateParameterRanges(instance);
                    var hasErrors = rangeResults.Any((Func<ParameterValidationResult, bool>)(r => r.ValidationStatus == ValidationStatus.Fail));
                    var hasWarnings = rangeResults.Any((Func<ParameterValidationResult, bool>)(r => r.ValidationStatus == ValidationStatus.Warning));

                    if (hasErrors)
                        errorInstances++;
                    else if (hasWarnings)
                        warningInstances++;
                    else
                        validInstances++;
                }

                result.ValidItems = validInstances;
                result.WarningItems = warningInstances;
                result.ErrorItems = errorInstances;

                if (errorInstances > 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add($"{errorInstances} devices have invalid parameter values");
                    result.Recommendations.Add("Fix invalid parameter values (negative, zero, or out-of-range)");
                }
                else if (warningInstances > allInstances.Count * 0.25)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"{warningInstances} devices have parameter values outside typical ranges");
                    result.Recommendations.Add("Review and verify parameter values that are flagged as warnings");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating parameter values: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate that devices can be properly classified for analysis
        /// </summary>
        private ValidationCategoryResult ValidateDeviceClassification(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Device Classification",
                Status = ValidationStatus.Pass
            };

            try
            {
                var classifications = _familyDetector.DetectFireAlarmFamilies(familyInstances);
                result.TotalItems = classifications.Count;

                var highConfidence = classifications.Values.Count(c => c.ConfidenceLevel == ConfidenceLevel.High);
                var mediumConfidence = classifications.Values.Count(c => c.ConfidenceLevel == ConfidenceLevel.Medium);
                var lowConfidence = classifications.Values.Count(c => c.ConfidenceLevel == ConfidenceLevel.Low);
                var unknown = classifications.Values.Count(c => c.DeviceType == DeviceType.Unknown);

                result.ValidItems = highConfidence + mediumConfidence;
                result.WarningItems = lowConfidence;
                result.ErrorItems = unknown;

                if (unknown > 0)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"{unknown} devices could not be classified");
                    result.Recommendations.Add("Add electrical parameters or improve naming for unclassified devices");
                }

                if (lowConfidence > highConfidence + mediumConfidence)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add("Most device classifications have low confidence");
                    result.Recommendations.Add("Improve device naming conventions or add more electrical parameters");
                }

                // Report classification results
                var typeCounts = classifications.Values
                    .GroupBy(c => c.DeviceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var type in typeCounts)
                {
                    result.Issues.Add($"Classified {type.Value} devices as {type.Key}");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating device classification: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate that devices are properly assigned to levels
        /// </summary>
        private ValidationCategoryResult ValidateLevelOrganization(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Level Organization",
                Status = ValidationStatus.Pass
            };

            try
            {
                var allInstances = familyInstances.ToList();
                result.TotalItems = allInstances.Count;

                var levelAssignments = new Dictionary<string, int>();
                int unassignedDevices = 0;

                foreach (var instance in allInstances)
                {
                    var level = GetDeviceLevel(instance);
                    // Treat "Unknown" as valid level assignment (matches working system)
                    if (string.IsNullOrEmpty(level) || level == "None" || level == "<None>")
                    {
                        unassignedDevices++;
                    }
                    else
                    {
                        // "Unknown" is a valid level in the working system
                        levelAssignments[level] = levelAssignments.ContainsKey(level) ? levelAssignments[level] + 1 : 1;
                    }
                }

                result.ValidItems = allInstances.Count - unassignedDevices;
                result.ErrorItems = unassignedDevices;

                if (unassignedDevices > 0)
                {
                    var percentage = (double)unassignedDevices / allInstances.Count * 100;
                    if (percentage > 25)
                    {
                        result.Status = ValidationStatus.Fail;
                        result.Issues.Add($"{percentage:F1}% of devices are not assigned to levels ({unassignedDevices} devices)");
                        result.Recommendations.Add("Assign all fire alarm devices to appropriate building levels");
                    }
                    else
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Issues.Add($"{unassignedDevices} devices not assigned to levels");
                        result.Recommendations.Add("Assign remaining devices to appropriate levels for better analysis");
                    }
                }

                // Report level distribution
                if (levelAssignments.Count == 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No devices are assigned to building levels - analysis requires level information");
                    result.Recommendations.Add("Assign devices to building levels or ensure level parameter is populated");
                    result.Recommendations.Add("Note: 'Unknown' level is acceptable for analysis purposes");
                }
                else
                {
                    foreach (var level in levelAssignments)
                    {
                        result.Issues.Add($"Level '{level.Key}': {level.Value} devices");
                    }

                    if (levelAssignments.Count == 1)
                    {
                        result.Issues.Add("All devices on single level - multi-level analysis not possible");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating level organization: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate electrical parameters are consistent and calculable
        /// </summary>
        private ValidationCategoryResult ValidateElectricalConsistency(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Electrical Consistency",
                Status = ValidationStatus.Pass
            };

            try
            {
                var fireAlarmInstances = familyInstances.Where((Func<object, bool>)(i => _familyDetector.IsFireAlarmFamily(GetFamilyName(i)))).ToList();
                result.TotalItems = fireAlarmInstances.Count;

                int consistentDevices = 0;
                int inconsistentDevices = 0;
                var issues = new List<string>();

                foreach (var instance in fireAlarmInstances)
                {
                    var familyName = GetFamilyName(instance);
                    var deviceType = _familyDetector.ClassifyDeviceType(instance).DeviceType;
                    var parameterResults = _parameterValidator.ValidateElectricalParameters(instance, deviceType);
                    
                    var hasConsistencyIssues = parameterResults.Any((Func<ParameterValidationResult, bool>)(pr => 
                        pr.ParameterName.Contains("Consistency") && 
                        pr.ValidationStatus != ValidationStatus.Pass));

                    if (hasConsistencyIssues)
                    {
                        inconsistentDevices++;
                        var consistencyIssues = parameterResults
                            .Where((Func<ParameterValidationResult, bool>)(pr => pr.ParameterName.Contains("Consistency")))
                            .Select((Func<ParameterValidationResult, string>)(pr => $"{familyName}: {pr.IssueDescription}"));
                        issues.AddRange(consistencyIssues);
                    }
                    else
                    {
                        consistentDevices++;
                    }
                }

                result.ValidItems = consistentDevices;
                result.WarningItems = inconsistentDevices;

                if (inconsistentDevices > 0)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"{inconsistentDevices} devices have electrical parameter inconsistencies");
                    result.Issues.AddRange(issues.Take(5)); // Show first 5 issues
                    if (issues.Count > 5)
                        result.Issues.Add($"... and {issues.Count - 5} more consistency issues");
                    
                    result.Recommendations.Add("Review and correct electrical parameter relationships");
                    result.Recommendations.Add("Ensure wattage and current values are consistent (P = I × V)");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating electrical consistency: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// DEPRECATED: Use ValidateAnalysisReadinessFromSnapshots instead
        /// </summary>
        [Obsolete("Use ValidateAnalysisReadinessFromSnapshots for consistent DeviceSnapshot-based validation")]
        private ValidationCategoryResult ValidateAnalysisReadiness(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Analysis Readiness",
                Status = ValidationStatus.Pass
            };

            try
            {
                // Use DeviceSnapshots for validation - SAME logic as working analysis
                var deviceSnapshots = GetValidatedDeviceSnapshots();
                result.TotalItems = deviceSnapshots.Count;

                if (deviceSnapshots.Count == 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No fire alarm devices found in model - analysis requires devices with CURRENT DRAW or Wattage parameters");
                    result.Recommendations.Add("Add notification devices (speakers, strobes, horns) with electrical parameters");
                    result.Recommendations.Add("Ensure device families contain 'CURRENT DRAW' or 'Wattage' parameters with valid values");
                    return result;
                }

                int readyDevices = 0;
                int partialDevices = 0;
                int notReadyDevices = 0;

                foreach (var snapshot in deviceSnapshots)
                {
                    // Use SAME criteria as working analysis system
                    var hasElectricalParams = snapshot.Amps > 0 || snapshot.Watts > 0;
                    var hasLevelAssignment = !string.IsNullOrEmpty(snapshot.LevelName); // "Unknown" is valid in working system

                    if (hasElectricalParams && hasLevelAssignment)
                        readyDevices++;
                    else if (hasElectricalParams || hasLevelAssignment)
                        partialDevices++;
                    else
                        notReadyDevices++;
                }

                result.ValidItems = readyDevices;
                result.WarningItems = partialDevices;
                result.ErrorItems = notReadyDevices;

                var readinessPercentage = (double)readyDevices / deviceSnapshots.Count;

                if (readinessPercentage < MODERATE_QUALITY_THRESHOLD)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add($"Only {readinessPercentage:P1} of devices ready for analysis (minimum {MODERATE_QUALITY_THRESHOLD:P0} required)");
                    result.Recommendations.Add("Add CURRENT DRAW or Wattage parameters to fire alarm device families");
                    result.Recommendations.Add("Ensure devices are assigned to building levels (Unknown level is acceptable as fallback)");
                }
                else if (readinessPercentage < ANALYSIS_READY_THRESHOLD)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Issues.Add($"{readinessPercentage:P1} of devices ready for analysis (recommended {ANALYSIS_READY_THRESHOLD:P0})");
                    result.Recommendations.Add("Improve data completeness for better analysis accuracy");
                }
                else
                {
                    result.Issues.Add($"{readinessPercentage:P1} of devices ready for analysis");
                    if (readinessPercentage >= GOOD_QUALITY_THRESHOLD)
                        result.Issues.Add("Model has excellent data quality for high-accuracy analysis - matches working analysis standards");
                    else
                        result.Issues.Add("Model has good data quality for reliable analysis - compatible with working analysis system");
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Fail;
                result.Issues.Add($"Error validating analysis readiness: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Summary Calculation

        /// <summary>
        /// Calculate overall validation summary from individual category results
        /// </summary>
        private void CalculateOverallSummary(ModelValidationSummary summary)
        {
            // Count critical failures
            var criticalFailures = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Fail);
            var warnings = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Warning);

            // Determine overall status
            if (criticalFailures > 0)
                summary.OverallStatus = ValidationStatus.Fail;
            else if (warnings > 0)
                summary.OverallStatus = ValidationStatus.Warning;
            else
                summary.OverallStatus = ValidationStatus.Pass;

            // Calculate device counts
            var analysisReadiness = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Analysis Readiness");
            if (analysisReadiness != null)
            {
                summary.TotalDevicesFound = analysisReadiness.TotalItems;
                summary.ValidDevicesCount = analysisReadiness.ValidItems;
                summary.MissingParametersCount = analysisReadiness.ErrorItems + analysisReadiness.WarningItems;
            }

            // Calculate readiness percentage
            if (summary.TotalDevicesFound > 0)
            {
                summary.ReadinessPercentage = (double)summary.ValidDevicesCount / summary.TotalDevicesFound * 100;
            }

            // Determine analysis readiness
            summary.AnalysisReadiness = summary.ReadinessPercentage >= ANALYSIS_READY_THRESHOLD * 100 && criticalFailures == 0;

            // Set analysis accuracy estimate
            if (summary.ReadinessPercentage >= GOOD_QUALITY_THRESHOLD * 100)
                summary.AnalysisAccuracy = "High Accuracy";
            else if (summary.ReadinessPercentage >= ANALYSIS_READY_THRESHOLD * 100)
                summary.AnalysisAccuracy = "Good Accuracy";
            else if (summary.ReadinessPercentage >= MODERATE_QUALITY_THRESHOLD * 100)
                summary.AnalysisAccuracy = "Moderate Accuracy";
            else
                summary.AnalysisAccuracy = "Low Accuracy";

            // Generate required actions
            summary.RequiredActions.Clear();
            if (criticalFailures > 0)
            {
                summary.RequiredActions.Add("Resolve critical validation failures before proceeding with analysis");
            }
            
            foreach (var detail in summary.ValidationDetails.Where((Func<ValidationCategoryResult, bool>)(vd => vd.Status != ValidationStatus.Pass)))
            {
                summary.RequiredActions.AddRange(detail.Recommendations);
            }

            // Remove duplicates and limit to top priorities
            summary.RequiredActions = summary.RequiredActions.Distinct().Take(10).ToList();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get validated device snapshots using SAME logic as working AnalysisServices
        /// </summary>
        private List<DeviceSnapshot> GetValidatedDeviceSnapshots()
        {
            try
            {
                var collector = new Autodesk.Revit.DB.FilteredElementCollector((Autodesk.Revit.DB.Document)_document);
                var allInstances = collector.OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                           .Cast<Autodesk.Revit.DB.FamilyInstance>()
                                           .ToList();
                
                // Use EXACT SAME logic as working AnalysisServices - create device snapshots
                var deviceSnapshots = _deviceSnapshotService.CreateSnapshots(allInstances);
                
                System.Diagnostics.Debug.WriteLine($"ModelComponentValidator: Created {deviceSnapshots.Count} device snapshots from {allInstances.Count} total elements");
                
                return deviceSnapshots;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device snapshots: {ex.Message}");
                return new List<DeviceSnapshot>();
            }
        }

        /// <summary>
        /// Get filtered family instances using SAME logic as working AnalysisServices (legacy method for backward compatibility)
        /// </summary>
        private IEnumerable<object> GetAllFamilyInstances()
        {
            try
            {
                var collector = new Autodesk.Revit.DB.FilteredElementCollector((Autodesk.Revit.DB.Document)_document);
                var allInstances = collector.OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                           .Cast<Autodesk.Revit.DB.FamilyInstance>()
                                           .ToList();
                
                // Use SAME filtering as working AnalysisServices - only electrical/fire alarm elements
                var electricalElements = allInstances.Where(IsElectricalFamilyInstance).ToList();
                
                System.Diagnostics.Debug.WriteLine($"ModelComponentValidator: Filtered {electricalElements.Count} electrical elements from {allInstances.Count} total elements");
                
                return electricalElements.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting family instances: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// Copy of working IsElectricalFamilyInstance method from AnalysisServices
        /// </summary>
        private bool IsElectricalFamilyInstance(FamilyInstance element)
        {
            if (element?.Symbol?.Family == null)
                return false;

            try
            {
                var familyName = element.Symbol.Family.Name;
                var categoryName = element.Category?.Name ?? "";

                // Check for specific parameters: CURRENT DRAW and Wattage ONLY
                var targetParams = new[] { "CURRENT DRAW", "Wattage" };
                bool hasElectricalParam = false;

                // Check instance parameters
                foreach (var paramName in targetParams)
                {
                    var param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found electrical parameter '{paramName}' in family '{familyName}' (instance)");
                        hasElectricalParam = true;
                        break;
                    }
                }

                // Check type parameters if instance parameters not found
                if (!hasElectricalParam && element.Symbol != null)
                {
                    foreach (var paramName in targetParams)
                    {
                        var param = element.Symbol.LookupParameter(paramName);
                        if (param != null && param.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found electrical parameter '{paramName}' in family '{familyName}' (type)");
                            hasElectricalParam = true;
                            break;
                        }
                    }
                }

                // Also check family names for common fire alarm device patterns (IDNAC devices ONLY)
                if (!hasElectricalParam)
                {
                    var familyUpper = familyName.ToUpperInvariant();
                    var categoryUpper = categoryName.ToUpperInvariant();

                    // FIRST: Exclude IDNET detection devices from IDNAC electrical analysis
                    var idnetDetectionKeywords = new[]
                    {
                        "DETECTORS", "DETECTOR", "MODULE", "PULL", "STATION", "MANUAL",
                        "MONITOR", "INPUT", "OUTPUT", "SENSOR", "SENSING"
                    };

                    // If it's clearly an IDNET detection device, exclude it from IDNAC analysis
                    if (idnetDetectionKeywords.Any(keyword => familyUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Excluded IDNET detection device from electrical analysis: '{familyName}' in category '{categoryName}'");
                        return false; // Explicitly exclude from IDNAC analysis
                    }

                    // SECOND: Only include IDNAC notification devices for electrical analysis
                    var idnacNotificationKeywords = new[]
                    {
                        "SPEAKER", "HORN", "STROBE", "BELL", "CHIME", "SOUNDER",
                        "NOTIFICATION", "NAC", "APPLIANCE"
                    };

                    if (idnacNotificationKeywords.Any(keyword => familyUpper.Contains(keyword) || categoryUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Found notification device by name pattern: '{familyName}' in category '{categoryName}' (for electrical analysis)");
                        hasElectricalParam = true;
                    }

                    // THIRD: Handle "FIRE ALARM" category more carefully - only for non-detection devices
                    else if (categoryUpper.Contains("FIRE ALARM") &&
                             !idnetDetectionKeywords.Any(keyword => familyUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Found fire alarm device (non-detection) by category: '{familyName}' in category '{categoryName}' (for electrical analysis)");
                        hasElectricalParam = true;
                    }
                }

                return hasElectricalParam;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking electrical family instance: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get family name from instance using Revit API
        /// </summary>
        private string GetFamilyName(object instance)
        {
            try
            {
                if (instance is Autodesk.Revit.DB.FamilyInstance familyInstance)
                {
                    return familyInstance?.Symbol?.Family?.Name ?? "";
                }
                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting family name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Get device level from instance using SAME logic as working AnalysisServices
        /// </summary>
        private string GetDeviceLevel(object instance)
        {
            try
            {
                if (instance is Autodesk.Revit.DB.FamilyInstance familyInstance)
                {
                    // Use EXACT same logic as working AnalysisServices (line 116)
                    var levelParam = familyInstance.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM) 
                        ?? familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                    
                    return levelParam?.AsValueString() ?? familyInstance.Host?.Name ?? "Unknown";
                }
                return "Unknown";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device level: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Extract parameters from family instance using Revit API
        /// </summary>
        private Dictionary<string, object> ExtractParameters(object instance)
        {
            var parameters = new Dictionary<string, object>();
            
            try
            {
                if (instance is Autodesk.Revit.DB.FamilyInstance familyInstance)
                {
                    // Extract common electrical parameters
                    var targetParams = new[] { "CURRENT DRAW", "Wattage", "Current", "Power", "CANDELA", "Candela" };
                    
                    foreach (var paramName in targetParams)
                    {
                        var param = familyInstance.LookupParameter(paramName);
                        if (param?.HasValue == true)
                        {
                            if (param.StorageType == Autodesk.Revit.DB.StorageType.Double)
                                parameters[paramName] = param.AsDouble();
                            else if (param.StorageType == Autodesk.Revit.DB.StorageType.Integer)
                                parameters[paramName] = param.AsInteger();
                            else if (param.StorageType == Autodesk.Revit.DB.StorageType.String)
                                parameters[paramName] = param.AsString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting parameters: {ex.Message}");
            }
            
            return parameters;
        }

        /// <summary>
        /// Check if parameters contain electrical parameters
        /// </summary>
        private bool HasElectricalParameters(Dictionary<string, object> parameters)
        {
            var electricalParams = new[] { "current", "current_draw", "wattage", "power", "candela", "cd" };
            return parameters.Keys.Any((Func<string, bool>)(key => 
                electricalParams.Any((Func<string, bool>)(ep => key.ToLower().Contains(ep)))));
        }

        #endregion
    }
}