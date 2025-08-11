using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

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

        // Validation thresholds
        private const double ANALYSIS_READY_THRESHOLD = 0.80; // 80% complete data required
        private const double GOOD_QUALITY_THRESHOLD = 0.95; // 95% for high accuracy
        private const double MODERATE_QUALITY_THRESHOLD = 0.60; // 60% minimum for moderate accuracy

        public ModelComponentValidator(object document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyDetector = new FireAlarmFamilyDetector();
            _parameterValidator = new ParameterValidationEngine();
        }

        /// <summary>
        /// Validate the entire model for fire alarm analysis readiness
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
                // Get all family instances from the model
                var familyInstances = GetAllFamilyInstances();
                
                // 1. Fire Alarm Families Validation
                var fireAlarmFamiliesResult = ValidateFireAlarmFamilies(familyInstances);
                summary.ValidationDetails.Add(fireAlarmFamiliesResult);

                // 2. Family Parameters Validation
                var parametersResult = ValidateFamilyParameters(familyInstances);
                summary.ValidationDetails.Add(parametersResult);

                // 3. Parameter Values Validation
                var parameterValuesResult = ValidateParameterValues(familyInstances);
                summary.ValidationDetails.Add(parameterValuesResult);

                // 4. Device Classification Validation
                var classificationResult = ValidateDeviceClassification(familyInstances);
                summary.ValidationDetails.Add(classificationResult);

                // 5. Level Organization Validation
                var levelOrgResult = ValidateLevelOrganization(familyInstances);
                summary.ValidationDetails.Add(levelOrgResult);

                // 6. Electrical Consistency Validation
                var electricalResult = ValidateElectricalConsistency(familyInstances);
                summary.ValidationDetails.Add(electricalResult);

                // 7. Analysis Readiness Validation
                var analysisReadinessResult = ValidateAnalysisReadiness(familyInstances);
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

        #region Validation Category Methods

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
                    result.Issues.Add("No fire alarm device families detected in the model");
                    result.Recommendations.Add("Add fire alarm notification devices (speakers, strobes, horns) to the model");
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
                        result.Issues.Add($"Fire alarm family '{familyGroup.Key}' missing electrical parameters");
                        result.Recommendations.Add($"Add Current, Wattage, or Candela parameters to '{familyGroup.Key}' family");
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
                    if (string.IsNullOrEmpty(level) || level == "None" || level == "<None>")
                    {
                        unassignedDevices++;
                    }
                    else
                    {
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
                    result.Issues.Add("No devices are assigned to any building levels");
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
        /// Validate model has sufficient data for IDNAC analysis
        /// </summary>
        private ValidationCategoryResult ValidateAnalysisReadiness(IEnumerable<object> familyInstances)
        {
            var result = new ValidationCategoryResult
            {
                CategoryName = "Analysis Readiness",
                Status = ValidationStatus.Pass
            };

            try
            {
                var fireAlarmInstances = familyInstances.Where((Func<object, bool>)(i => _familyDetector.IsFireAlarmFamily(GetFamilyName(i)))).ToList();
                result.TotalItems = fireAlarmInstances.Count;

                if (fireAlarmInstances.Count == 0)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add("No fire alarm devices found in model");
                    result.Recommendations.Add("Add fire alarm notification devices to enable analysis");
                    return result;
                }

                int readyDevices = 0;
                int partialDevices = 0;
                int notReadyDevices = 0;

                foreach (var instance in fireAlarmInstances)
                {
                    var parameters = ExtractParameters(instance);
                    var level = GetDeviceLevel(instance);
                    var hasElectricalParams = HasElectricalParameters(parameters);
                    var hasLevelAssignment = !string.IsNullOrEmpty(level) && level != "None" && level != "<None>";

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

                var readinessPercentage = (double)readyDevices / fireAlarmInstances.Count;

                if (readinessPercentage < MODERATE_QUALITY_THRESHOLD)
                {
                    result.Status = ValidationStatus.Fail;
                    result.Issues.Add($"Only {readinessPercentage:P1} of devices ready for analysis (minimum {MODERATE_QUALITY_THRESHOLD:P0} required)");
                    result.Recommendations.Add("Add missing electrical parameters and level assignments");
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
                        result.Issues.Add("Model has excellent data quality for high-accuracy analysis");
                    else
                        result.Issues.Add("Model has good data quality for reliable analysis");
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
        /// Get all family instances from the document using Revit API
        /// </summary>
        private IEnumerable<object> GetAllFamilyInstances()
        {
            try
            {
                // Use FilteredElementCollector with Revit API to get all FamilyInstances
                var collector = new Autodesk.Revit.DB.FilteredElementCollector((Autodesk.Revit.DB.Document)_document);
                return collector.OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                               .Cast<Autodesk.Revit.DB.FamilyInstance>()
                               .Cast<object>()
                               .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting family instances: {ex.Message}");
                return new List<object>();
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
        /// Get device level from instance using Revit API
        /// </summary>
        private string GetDeviceLevel(object instance)
        {
            try
            {
                if (instance is Autodesk.Revit.DB.FamilyInstance familyInstance)
                {
                    // Get level from parameters
                    var levelParam = familyInstance.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM) 
                        ?? familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                    
                    if (levelParam?.HasValue == true)
                    {
                        return levelParam.AsValueString() ?? "";
                    }
                    
                    // Fallback to host if available
                    return familyInstance.Host?.Name ?? "";
                }
                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device level: {ex.Message}");
                return "";
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