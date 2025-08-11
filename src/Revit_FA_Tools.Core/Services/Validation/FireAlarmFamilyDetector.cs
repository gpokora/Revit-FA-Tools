using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Specialized detector for identifying and classifying fire alarm families in the Revit model
    /// </summary>
    public class FireAlarmFamilyDetector
    {
        // Keywords for device type classification
        private static readonly string[] SpeakerKeywords = { "speaker", "audio", "voice", "talk", "sound" };
        private static readonly string[] StrobeKeywords = { "strobe", "flash", "light", "visual", "beacon" };
        private static readonly string[] HornKeywords = { "horn", "sounder", "bell", "tone", "buzzer", "alarm" };
        private static readonly string[] ComboKeywords = { "combo", "combination", "multi", "dual", "speaker/strobe" };
        private static readonly string[] ControlKeywords = { "module", "relay", "control", "monitor", "isolator", "repeater" };
        private static readonly string[] FireAlarmKeywords = { "fire", "alarm", "notification", "emergency", "safety", "detector" };

        // Known fire alarm manufacturers
        private static readonly string[] FireAlarmManufacturers = { 
            "autocall", "system sensor", "gentex", "wheelock", "simplex", 
            "notifier", "edwards", "faraday", "honeywell", "siemens",
            "potter", "gamewell", "fire-lite", "silent knight", "bosch"
        };

        // Standard candela values
        private static readonly int[] StandardCandelaValues = { 15, 30, 75, 95, 110, 135, 177 };

        // Standard wattage values for speakers
        private static readonly double[] StandardWattageValues = { 0.125, 0.25, 0.5, 1, 2, 4, 8, 16, 25 };

        /// <summary>
        /// Detect and classify fire alarm families in the model
        /// </summary>
        /// <param name="familyInstances">List of family instances to analyze</param>
        /// <returns>Dictionary of family names to classification results</returns>
        public Dictionary<string, DeviceClassificationResult> DetectFireAlarmFamilies(
            IEnumerable<object> familyInstances)
        {
            var results = new Dictionary<string, DeviceClassificationResult>();
            
            foreach (var instance in familyInstances)
            {
                try
                {
                    var familyName = GetFamilyName(instance);
                    if (string.IsNullOrEmpty(familyName) || results.ContainsKey(familyName))
                        continue;

                    var classification = ClassifyDeviceType(instance);
                    if (classification.DeviceType != DeviceType.Unknown || 
                        IsFireAlarmFamily(familyName))
                    {
                        results[familyName] = classification;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing
                    System.Diagnostics.Debug.WriteLine($"Error classifying device {GetFamilyName(instance)}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Classify individual device type based on parameters and naming
        /// </summary>
        /// <param name="instance">Family instance to classify</param>
        /// <returns>Device classification result</returns>
        public DeviceClassificationResult ClassifyDeviceType(object instance)
        {
            var result = new DeviceClassificationResult
            {
                DeviceType = DeviceType.Unknown,
                ConfidenceLevel = ConfidenceLevel.Low,
                ClassificationReasoning = "No classification criteria met"
            };

            try
            {
                var familyName = GetFamilyName(instance)?.ToLower() ?? "";
                var typeName = GetTypeName(instance)?.ToLower() ?? "";
                var parameters = ExtractParameters(instance);
                
                result.ParameterValues = parameters;
                result.DetectedParameters = parameters.Keys.ToList();

                // Parameter-based classification (highest confidence)
                var parameterClassification = ClassifyByParameters(parameters);
                if (parameterClassification.DeviceType != DeviceType.Unknown)
                {
                    result = parameterClassification;
                    result.ConfidenceLevel = ConfidenceLevel.High;
                    result.ClassificationReasoning = "Classified by electrical parameters";
                }
                
                // Name-based classification (medium confidence)
                else if (ContainsKeywords(familyName + " " + typeName, ComboKeywords))
                {
                    result.DeviceType = DeviceType.Combination;
                    result.ConfidenceLevel = ConfidenceLevel.Medium;
                    result.ClassificationReasoning = "Classified by combination keywords in name";
                }
                else if (ContainsKeywords(familyName + " " + typeName, SpeakerKeywords))
                {
                    result.DeviceType = DeviceType.Speaker;
                    result.ConfidenceLevel = ConfidenceLevel.Medium;
                    result.ClassificationReasoning = "Classified by speaker keywords in name";
                }
                else if (ContainsKeywords(familyName + " " + typeName, StrobeKeywords))
                {
                    result.DeviceType = DeviceType.Strobe;
                    result.ConfidenceLevel = ConfidenceLevel.Medium;
                    result.ClassificationReasoning = "Classified by strobe keywords in name";
                }
                else if (ContainsKeywords(familyName + " " + typeName, HornKeywords))
                {
                    result.DeviceType = DeviceType.Horn;
                    result.ConfidenceLevel = ConfidenceLevel.Medium;
                    result.ClassificationReasoning = "Classified by horn keywords in name";
                }
                else if (ContainsKeywords(familyName + " " + typeName, ControlKeywords))
                {
                    result.DeviceType = DeviceType.Control;
                    result.ConfidenceLevel = ConfidenceLevel.Medium;
                    result.ClassificationReasoning = "Classified by control keywords in name";
                }
                
                // Manufacturer-based classification (low confidence)
                else if (IsKnownFireAlarmManufacturer(familyName + " " + typeName))
                {
                    result.DeviceType = DeviceType.Unknown; // Still unknown type, but likely fire alarm
                    result.ConfidenceLevel = ConfidenceLevel.Low;
                    result.ClassificationReasoning = "Known fire alarm manufacturer but unclear device type";
                }
            }
            catch (Exception ex)
            {
                result.ClassificationReasoning = $"Classification error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Determine if a family is fire alarm related
        /// </summary>
        /// <param name="familyName">Family name to check</param>
        /// <returns>True if family appears to be fire alarm related</returns>
        public bool IsFireAlarmFamily(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return false;

            var name = familyName.ToLower();
            
            // Check for fire alarm keywords
            if (ContainsKeywords(name, FireAlarmKeywords) ||
                ContainsKeywords(name, SpeakerKeywords) ||
                ContainsKeywords(name, StrobeKeywords) ||
                ContainsKeywords(name, HornKeywords) ||
                ContainsKeywords(name, ComboKeywords) ||
                ContainsKeywords(name, ControlKeywords))
            {
                return true;
            }

            // Check for known manufacturers
            return IsKnownFireAlarmManufacturer(name);
        }

        /// <summary>
        /// Validate that families are in appropriate Revit categories
        /// </summary>
        /// <param name="familyInstances">Family instances to validate</param>
        /// <returns>List of category validation issues</returns>
        public List<ValidationIssue> ValidateFamilyCategories(IEnumerable<object> familyInstances)
        {
            var issues = new List<ValidationIssue>();
            var validCategories = new[] { "Fire Alarm Devices", "Electrical Equipment", "Communication Devices", "Generic Models" };

            foreach (var instance in familyInstances)
            {
                try
                {
                    var category = GetCategory(instance);
                    var familyName = GetFamilyName(instance);
                    
                    if (IsFireAlarmFamily(familyName) && 
                        !string.IsNullOrEmpty(category) &&
                        !validCategories.Any(vc => category.Contains(vc)))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = "Family Categories",
                            Description = $"Fire alarm family '{familyName}' is in category '{category}' instead of expected fire alarm categories",
                            AffectedElementIds = new List<int> { GetElementId(instance) },
                            Resolution = "Consider changing family category to 'Fire Alarm Devices' or 'Electrical Equipment'",
                            Impact = "May affect device filtering and analysis accuracy"
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    System.Diagnostics.Debug.WriteLine($"Error validating category for {GetFamilyName(instance)}: {ex.Message}");
                }
            }

            return issues;
        }

        #region Private Helper Methods

        /// <summary>
        /// Classify device based on electrical parameters
        /// </summary>
        private DeviceClassificationResult ClassifyByParameters(Dictionary<string, object> parameters)
        {
            var result = new DeviceClassificationResult
            {
                DeviceType = DeviceType.Unknown,
                ConfidenceLevel = ConfidenceLevel.Low
            };

            var hasWattage = HasParameterWithValue(parameters, "wattage", "power");
            var hasCandela = HasParameterWithValue(parameters, "candela", "cd");
            var hasCurrent = HasParameterWithValue(parameters, "current", "current_draw");

            // Combination device: has both wattage and candela
            if (hasWattage && hasCandela)
            {
                result.DeviceType = DeviceType.Combination;
                result.ClassificationReasoning = "Has both wattage and candela parameters";
                return result;
            }

            // Speaker: has wattage but no candela
            if (hasWattage && !hasCandela)
            {
                var wattageValue = GetParameterValue(parameters, "wattage", "power");
                if (wattageValue > 0 && wattageValue <= 50) // Reasonable speaker wattage range
                {
                    result.DeviceType = DeviceType.Speaker;
                    result.ClassificationReasoning = $"Has wattage parameter ({wattageValue}W) without candela";
                    return result;
                }
            }

            // Strobe: has candela but no wattage
            if (hasCandela && !hasWattage)
            {
                var candelaValue = GetParameterValue(parameters, "candela", "cd");
                if (StandardCandelaValues.Contains((int)candelaValue))
                {
                    result.DeviceType = DeviceType.Strobe;
                    result.ClassificationReasoning = $"Has standard candela rating ({candelaValue} cd)";
                    return result;
                }
            }

            // Horn: has current but no wattage or candela
            if (hasCurrent && !hasWattage && !hasCandela)
            {
                var currentValue = GetParameterValue(parameters, "current", "current_draw");
                if (currentValue > 0 && currentValue <= 0.5) // Typical horn current range
                {
                    result.DeviceType = DeviceType.Horn;
                    result.ClassificationReasoning = $"Has current draw ({currentValue}A) without power parameters";
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Check if text contains any of the specified keywords
        /// </summary>
        private bool ContainsKeywords(string text, string[] keywords)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return keywords.Any(keyword => 
                text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Check if text contains known fire alarm manufacturer names
        /// </summary>
        private bool IsKnownFireAlarmManufacturer(string text)
        {
            return ContainsKeywords(text, FireAlarmManufacturers);
        }

        /// <summary>
        /// Check if parameters contain a specific parameter with a valid value
        /// </summary>
        private bool HasParameterWithValue(Dictionary<string, object> parameters, params string[] parameterNames)
        {
            foreach (var paramName in parameterNames)
            {
                if (parameters.ContainsKey(paramName) && 
                    parameters[paramName] != null &&
                    double.TryParse(parameters[paramName].ToString(), out double value) &&
                    value > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get parameter value as double
        /// </summary>
        private double GetParameterValue(Dictionary<string, object> parameters, params string[] parameterNames)
        {
            foreach (var paramName in parameterNames)
            {
                if (parameters.ContainsKey(paramName) && 
                    double.TryParse(parameters[paramName]?.ToString(), out double value))
                {
                    return value;
                }
            }
            return 0;
        }

        /// <summary>
        /// Extract parameters from family instance (placeholder - would need actual Revit API implementation)
        /// </summary>
        private Dictionary<string, object> ExtractParameters(object instance)
        {
            var parameters = new Dictionary<string, object>();
            
            // This would be implemented with actual Revit API calls
            // For now, returning empty dictionary as placeholder
            // Real implementation would use FamilyInstance.Parameters and iterate through them
            
            return parameters;
        }

        /// <summary>
        /// Get family name from instance (placeholder)
        /// </summary>
        private string GetFamilyName(object instance)
        {
            // Placeholder - would use actual Revit API
            // Real implementation: ((FamilyInstance)instance)?.Symbol?.Family?.Name
            return "";
        }

        /// <summary>
        /// Get type name from instance (placeholder)
        /// </summary>
        private string GetTypeName(object instance)
        {
            // Placeholder - would use actual Revit API
            // Real implementation: ((FamilyInstance)instance)?.Symbol?.Name
            return "";
        }

        /// <summary>
        /// Get category from instance (placeholder)
        /// </summary>
        private string GetCategory(object instance)
        {
            // Placeholder - would use actual Revit API
            // Real implementation: ((FamilyInstance)instance)?.Category?.Name
            return "";
        }

        /// <summary>
        /// Get element ID from instance (placeholder)
        /// </summary>
        private int GetElementId(object instance)
        {
            // Placeholder - would use actual Revit API
            // Real implementation: ((FamilyInstance)instance)?.Id?.IntegerValue ?? 0
            return 0;
        }

        #endregion
    }
}