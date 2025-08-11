using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Comprehensive parameter validation engine for fire alarm analysis
    /// </summary>
    public class ParameterValidationEngine
    {
        // Parameter validation ranges
        private const double MIN_CURRENT_DRAW = 0.001;
        private const double MAX_CURRENT_DRAW = 1.0;
        private const double HIGH_CURRENT_WARNING = 0.5;
        
        private const double MIN_WATTAGE = 0.1;
        private const double MAX_WATTAGE = 50.0;
        private const double HIGH_WATTAGE_WARNING = 25.0;
        
        private const double MIN_UNIT_LOADS = 0.5;
        private const double MAX_UNIT_LOADS = 4.0;
        
        private const double NOMINAL_VOLTAGE = 24.0; // Standard fire alarm voltage
        private const double POWER_CURRENT_TOLERANCE = 0.20; // 20% tolerance for P=I*V relationship

        // Standard candela values for strobes
        private static readonly int[] StandardCandelaValues = { 15, 30, 75, 95, 110, 135, 177 };
        
        // Standard wattage values for speakers
        private static readonly double[] StandardWattageValues = { 0.125, 0.25, 0.5, 1, 2, 4, 8, 16, 25 };

        /// <summary>
        /// Validate electrical parameters for a single device
        /// </summary>
        /// <param name="instance">Family instance to validate</param>
        /// <param name="deviceType">Classified device type</param>
        /// <returns>List of parameter validation results</returns>
        public List<ParameterValidationResult> ValidateElectricalParameters(
            object instance, DeviceType deviceType)
        {
            var results = new List<ParameterValidationResult>();
            var parameters = ExtractParameters(instance);

            // Validate current draw
            var currentResult = ValidateCurrentDraw(parameters, deviceType);
            if (currentResult != null)
                results.Add(currentResult);

            // Validate wattage
            var wattageResult = ValidateWattage(parameters, deviceType);
            if (wattageResult != null)
                results.Add(wattageResult);

            // Validate candela
            var candelaResult = ValidateCandela(parameters, deviceType);
            if (candelaResult != null)
                results.Add(candelaResult);

            // Validate unit loads
            var unitLoadResult = ValidateUnitLoads(parameters, deviceType);
            if (unitLoadResult != null)
                results.Add(unitLoadResult);

            // Validate parameter consistency
            var consistencyResults = ValidateParameterConsistency(parameters, deviceType);
            results.AddRange(consistencyResults);

            return results;
        }

        /// <summary>
        /// Check parameter completeness across all instances
        /// </summary>
        /// <param name="instances">List of family instances</param>
        /// <param name="classifications">Device classifications</param>
        /// <returns>Parameter completeness validation results</returns>
        public List<ValidationIssue> ValidateParameterCompleteness(
            IEnumerable<object> instances, 
            Dictionary<string, DeviceClassificationResult> classifications)
        {
            var issues = new List<ValidationIssue>();
            var completenessStats = new Dictionary<string, int>();
            var totalDevices = 0;

            foreach (var instance in instances)
            {
                try
                {
                    var familyName = GetFamilyName(instance);
                    var parameters = ExtractParameters(instance);
                    var deviceType = classifications.ContainsKey(familyName) 
                        ? classifications[familyName].DeviceType 
                        : DeviceType.Unknown;

                    totalDevices++;
                    var missingParams = GetMissingRequiredParameters(parameters, deviceType);
                    
                    foreach (var param in missingParams)
                    {
                        var key = $"Missing_{param}";
                        completenessStats[key] = completenessStats.ContainsKey(key) ? completenessStats[key] + 1 : 1;
                    }

                    // Check for devices with no electrical parameters at all
                    if (!HasAnyElectricalParameters(parameters))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Critical,
                            Category = "Parameter Completeness",
                            Description = $"Device '{familyName}' has no electrical parameters",
                            AffectedElementIds = new List<int> { GetElementId(instance) },
                            Resolution = "Add at least one electrical parameter (Current, Wattage, or Candela)",
                            Impact = "Device cannot be included in electrical analysis",
                            CanAutoFix = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error validating completeness: {ex.Message}");
                }
            }

            // Generate summary issues for missing parameters
            foreach (var stat in completenessStats)
            {
                var paramName = stat.Key.Replace("Missing_", "");
                var count = stat.Value;
                var percentage = (double)count / totalDevices * 100;

                if (percentage > 50) // More than 50% missing
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = "Parameter Completeness",
                        Description = $"{percentage:F1}% of devices missing '{paramName}' parameter ({count} of {totalDevices})",
                        Resolution = $"Add '{paramName}' parameter to affected device families",
                        Impact = "Significantly reduces analysis accuracy",
                        EstimatedFixTimeMinutes = count * 2 // Estimate 2 minutes per device
                    });
                }
                else if (percentage > 25) // 25-50% missing
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Parameter Completeness",
                        Description = $"{percentage:F1}% of devices missing '{paramName}' parameter ({count} of {totalDevices})",
                        Resolution = $"Consider adding '{paramName}' parameter to improve accuracy",
                        Impact = "Moderately affects analysis accuracy"
                    });
                }
            }

            return issues;
        }

        /// <summary>
        /// Validate parameter value ranges for a device
        /// </summary>
        /// <param name="instance">Family instance to validate</param>
        /// <returns>List of range validation issues</returns>
        public List<ParameterValidationResult> ValidateParameterRanges(object instance)
        {
            var results = new List<ParameterValidationResult>();
            var parameters = ExtractParameters(instance);

            foreach (var param in parameters)
            {
                var result = ValidateParameterRange(param.Key, param.Value);
                if (result != null)
                    results.Add(result);
            }

            return results;
        }

        #region Private Validation Methods

        /// <summary>
        /// Validate current draw parameter
        /// </summary>
        private ParameterValidationResult ValidateCurrentDraw(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var currentValue = GetParameterValue(parameters, "current", "current_draw");
            if (currentValue == 0)
                return null; // Parameter not present

            var result = new ParameterValidationResult
            {
                ParameterName = "Current Draw",
                ParameterValue = currentValue,
                ExpectedRange = $"{MIN_CURRENT_DRAW:F3}A to {MAX_CURRENT_DRAW:F1}A"
            };

            if (currentValue <= 0)
            {
                result.ValidationStatus = ValidationStatus.Fail;
                result.IssueDescription = "Current draw cannot be zero or negative";
                result.SuggestedValue = GetTypicalCurrent(deviceType);
            }
            else if (currentValue < MIN_CURRENT_DRAW)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Current draw is unusually low for fire alarm device";
            }
            else if (currentValue > MAX_CURRENT_DRAW)
            {
                result.ValidationStatus = ValidationStatus.Fail;
                result.IssueDescription = "Current draw exceeds maximum for notification devices";
                result.SuggestedValue = GetTypicalCurrent(deviceType);
            }
            else if (currentValue > HIGH_CURRENT_WARNING)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "High current draw - verify this is correct";
            }
            else
            {
                result.ValidationStatus = ValidationStatus.Pass;
            }

            return result;
        }

        /// <summary>
        /// Validate wattage parameter
        /// </summary>
        private ParameterValidationResult ValidateWattage(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var wattageValue = GetParameterValue(parameters, "wattage", "power");
            if (wattageValue == 0)
                return null;

            var result = new ParameterValidationResult
            {
                ParameterName = "Wattage",
                ParameterValue = wattageValue,
                ExpectedRange = $"{MIN_WATTAGE:F1}W to {MAX_WATTAGE:F1}W"
            };

            if (wattageValue <= 0)
            {
                result.ValidationStatus = ValidationStatus.Fail;
                result.IssueDescription = "Wattage cannot be zero or negative";
                result.SuggestedValue = GetTypicalWattage(deviceType);
            }
            else if (wattageValue < MIN_WATTAGE)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Wattage is unusually low for speaker device";
            }
            else if (wattageValue > MAX_WATTAGE)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Wattage is unusually high - verify this is correct";
            }
            else if (wattageValue > HIGH_WATTAGE_WARNING)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "High wattage speaker - confirm this is intended";
            }
            else if (!StandardWattageValues.Any(sw => Math.Abs(sw - wattageValue) < 0.01))
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Non-standard wattage value";
                result.SuggestedValue = GetNearestStandardWattage(wattageValue);
            }
            else
            {
                result.ValidationStatus = ValidationStatus.Pass;
            }

            return result;
        }

        /// <summary>
        /// Validate candela parameter
        /// </summary>
        private ParameterValidationResult ValidateCandela(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var candelaValue = GetParameterValue(parameters, "candela", "cd");
            if (candelaValue == 0)
                return null;

            var result = new ParameterValidationResult
            {
                ParameterName = "Candela",
                ParameterValue = candelaValue,
                ExpectedRange = "Standard values: 15, 30, 75, 95, 110, 135, 177 cd"
            };

            if (candelaValue <= 0)
            {
                result.ValidationStatus = ValidationStatus.Fail;
                result.IssueDescription = "Candela rating cannot be zero or negative";
                result.SuggestedValue = 75; // Common default
            }
            else if (!StandardCandelaValues.Contains((int)candelaValue))
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Non-standard candela rating";
                result.SuggestedValue = GetNearestStandardCandela(candelaValue);
            }
            else
            {
                result.ValidationStatus = ValidationStatus.Pass;
            }

            return result;
        }

        /// <summary>
        /// Validate unit loads parameter
        /// </summary>
        private ParameterValidationResult ValidateUnitLoads(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var unitLoadsValue = GetParameterValue(parameters, "unit_loads", "unitloads");
            if (unitLoadsValue == 0)
                return null;

            var result = new ParameterValidationResult
            {
                ParameterName = "Unit Loads",
                ParameterValue = unitLoadsValue,
                ExpectedRange = $"{MIN_UNIT_LOADS:F1} to {MAX_UNIT_LOADS:F1}"
            };

            if (unitLoadsValue <= 0)
            {
                result.ValidationStatus = ValidationStatus.Fail;
                result.IssueDescription = "Unit loads cannot be zero or negative";
                result.SuggestedValue = GetTypicalUnitLoads(deviceType);
            }
            else if (unitLoadsValue < MIN_UNIT_LOADS || unitLoadsValue > MAX_UNIT_LOADS)
            {
                result.ValidationStatus = ValidationStatus.Warning;
                result.IssueDescription = "Unit loads value is outside typical range";
                result.SuggestedValue = GetTypicalUnitLoads(deviceType);
            }
            else
            {
                result.ValidationStatus = ValidationStatus.Pass;
            }

            return result;
        }

        /// <summary>
        /// Validate consistency between parameters (e.g., P = I * V)
        /// </summary>
        private List<ParameterValidationResult> ValidateParameterConsistency(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var results = new List<ParameterValidationResult>();

            var current = GetParameterValue(parameters, "current", "current_draw");
            var wattage = GetParameterValue(parameters, "wattage", "power");

            // Check wattage vs current consistency (P = I * V)
            if (current > 0 && wattage > 0)
            {
                var expectedWattage = current * NOMINAL_VOLTAGE;
                var tolerance = expectedWattage * POWER_CURRENT_TOLERANCE;

                if (Math.Abs(wattage - expectedWattage) > tolerance)
                {
                    results.Add(new ParameterValidationResult
                    {
                        ParameterName = "Power-Current Consistency",
                        ParameterValue = $"{wattage:F1}W vs {expectedWattage:F1}W expected",
                        ValidationStatus = ValidationStatus.Warning,
                        IssueDescription = $"Wattage ({wattage:F1}W) doesn't match current draw ({current:F3}A) assuming 24V",
                        SuggestedValue = $"Either {expectedWattage:F1}W or {(wattage / NOMINAL_VOLTAGE):F3}A"
                    });
                }
            }

            // Check device type parameter consistency
            if (deviceType == DeviceType.Speaker && wattage == 0)
            {
                results.Add(new ParameterValidationResult
                {
                    ParameterName = "Speaker Wattage",
                    ValidationStatus = ValidationStatus.Warning,
                    IssueDescription = "Speaker device should have wattage parameter",
                    SuggestedValue = GetTypicalWattage(DeviceType.Speaker)
                });
            }

            if (deviceType == DeviceType.Strobe && GetParameterValue(parameters, "candela", "cd") == 0)
            {
                results.Add(new ParameterValidationResult
                {
                    ParameterName = "Strobe Candela",
                    ValidationStatus = ValidationStatus.Warning,
                    IssueDescription = "Strobe device should have candela rating",
                    SuggestedValue = 75 // Common default
                });
            }

            return results;
        }

        /// <summary>
        /// Validate individual parameter range
        /// </summary>
        private ParameterValidationResult ValidateParameterRange(string parameterName, object parameterValue)
        {
            if (!double.TryParse(parameterValue?.ToString(), out double value))
                return null;

            var lowerName = parameterName.ToLower();
            
            if (lowerName.Contains("current"))
            {
                return ValidateCurrentRange(parameterName, value);
            }
            else if (lowerName.Contains("wattage") || lowerName.Contains("power"))
            {
                return ValidateWattageRange(parameterName, value);
            }
            else if (lowerName.Contains("candela"))
            {
                return ValidateCandelaRange(parameterName, value);
            }
            else if (lowerName.Contains("unit") && lowerName.Contains("load"))
            {
                return ValidateUnitLoadRange(parameterName, value);
            }

            return null;
        }

        /// <summary>
        /// Get missing required parameters for a device type
        /// </summary>
        private List<string> GetMissingRequiredParameters(Dictionary<string, object> parameters, DeviceType deviceType)
        {
            var missing = new List<string>();

            switch (deviceType)
            {
                case DeviceType.Speaker:
                    if (GetParameterValue(parameters, "wattage", "power") == 0)
                        missing.Add("Wattage");
                    break;

                case DeviceType.Strobe:
                    if (GetParameterValue(parameters, "candela", "cd") == 0)
                        missing.Add("Candela");
                    if (GetParameterValue(parameters, "current", "current_draw") == 0)
                        missing.Add("Current");
                    break;

                case DeviceType.Horn:
                    if (GetParameterValue(parameters, "current", "current_draw") == 0)
                        missing.Add("Current");
                    break;

                case DeviceType.Combination:
                    if (GetParameterValue(parameters, "wattage", "power") == 0)
                        missing.Add("Wattage");
                    if (GetParameterValue(parameters, "candela", "cd") == 0)
                        missing.Add("Candela");
                    if (GetParameterValue(parameters, "current", "current_draw") == 0)
                        missing.Add("Current");
                    break;

                default:
                    // For unknown devices, at least one electrical parameter is required
                    if (!HasAnyElectricalParameters(parameters))
                        missing.Add("Any electrical parameter (Current, Wattage, or Candela)");
                    break;
            }

            return missing;
        }

        #endregion

        #region Helper Methods

        private bool HasAnyElectricalParameters(Dictionary<string, object> parameters)
        {
            return GetParameterValue(parameters, "current", "current_draw") > 0 ||
                   GetParameterValue(parameters, "wattage", "power") > 0 ||
                   GetParameterValue(parameters, "candela", "cd") > 0;
        }

        private double GetParameterValue(Dictionary<string, object> parameters, params string[] names)
        {
            foreach (var name in names)
            {
                if (parameters.ContainsKey(name) && 
                    double.TryParse(parameters[name]?.ToString(), out double value))
                {
                    return value;
                }
            }
            return 0;
        }

        private double GetTypicalCurrent(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Speaker: return 0.05;
                case DeviceType.Strobe: return 0.1;
                case DeviceType.Horn: return 0.05;
                case DeviceType.Combination: return 0.15;
                default: return 0.05;
            }
        }

        private double GetTypicalWattage(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Speaker: return 2.0;
                case DeviceType.Combination: return 2.0;
                default: return 1.0;
            }
        }

        private double GetTypicalUnitLoads(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Control: return 4.0; // Isolators/repeaters
                default: return 1.0; // Standard devices
            }
        }

        private double GetNearestStandardWattage(double value)
        {
            return StandardWattageValues.OrderBy(sw => Math.Abs(sw - value)).First();
        }

        private int GetNearestStandardCandela(double value)
        {
            return StandardCandelaValues.OrderBy(sc => Math.Abs(sc - value)).First();
        }

        private ParameterValidationResult ValidateCurrentRange(string name, double value)
        {
            return new ParameterValidationResult
            {
                ParameterName = name,
                ParameterValue = value,
                ValidationStatus = value >= MIN_CURRENT_DRAW && value <= MAX_CURRENT_DRAW 
                    ? ValidationStatus.Pass : ValidationStatus.Warning,
                IssueDescription = value < MIN_CURRENT_DRAW || value > MAX_CURRENT_DRAW
                    ? $"Current value {value:F3}A is outside typical range"
                    : null
            };
        }

        private ParameterValidationResult ValidateWattageRange(string name, double value)
        {
            return new ParameterValidationResult
            {
                ParameterName = name,
                ParameterValue = value,
                ValidationStatus = value >= MIN_WATTAGE && value <= MAX_WATTAGE 
                    ? ValidationStatus.Pass : ValidationStatus.Warning,
                IssueDescription = value < MIN_WATTAGE || value > MAX_WATTAGE
                    ? $"Wattage value {value:F1}W is outside typical range"
                    : null
            };
        }

        private ParameterValidationResult ValidateCandelaRange(string name, double value)
        {
            return new ParameterValidationResult
            {
                ParameterName = name,
                ParameterValue = value,
                ValidationStatus = StandardCandelaValues.Contains((int)value) 
                    ? ValidationStatus.Pass : ValidationStatus.Warning,
                IssueDescription = !StandardCandelaValues.Contains((int)value)
                    ? $"Candela value {value} is not a standard rating"
                    : null
            };
        }

        private ParameterValidationResult ValidateUnitLoadRange(string name, double value)
        {
            return new ParameterValidationResult
            {
                ParameterName = name,
                ParameterValue = value,
                ValidationStatus = value >= MIN_UNIT_LOADS && value <= MAX_UNIT_LOADS 
                    ? ValidationStatus.Pass : ValidationStatus.Warning,
                IssueDescription = value < MIN_UNIT_LOADS || value > MAX_UNIT_LOADS
                    ? $"Unit loads value {value} is outside typical range"
                    : null
            };
        }

        // Placeholder methods for Revit API integration
        private Dictionary<string, object> ExtractParameters(object instance)
        {
            // Placeholder - would implement actual Revit parameter extraction
            // Real implementation would use FamilyInstance.Parameters
            return new Dictionary<string, object>();
        }

        private string GetFamilyName(object instance)
        {
            // Placeholder - would use actual Revit API
            // Real implementation: ((FamilyInstance)instance)?.Symbol?.Family?.Name ?? ""
            return "";
        }

        private int GetElementId(object instance)
        {
            // Placeholder - would use actual Revit API  
            // Real implementation: ((FamilyInstance)instance)?.Id?.IntegerValue ?? 0
            return 0;
        }

        #endregion
    }
}