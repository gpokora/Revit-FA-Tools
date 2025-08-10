using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// Multi-strategy parameter extraction from device names and properties
    /// Supports family name parsing, custom properties, and intelligent inference
    /// </summary>
    public class ParameterExtractor
    {
        // Compiled regex patterns for performance
        private static readonly Regex CandelaPattern = new Regex(@"(\d+)\s*c?d", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WattagePattern = new Regex(@"(\d+(?:\.\d+)?)\s*w", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VoltagePattern = new Regex(@"(\d+)v", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ModelNumberPattern = new Regex(@"[A-Z]{2,}-?\d{4,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        // Device characteristic patterns
        private static readonly string[] StrobeKeywords = { "strobe", "flash", "light", "candela", "cd" };
        private static readonly string[] SpeakerKeywords = { "speaker", "horn", "sound", "audio", "voice", "tone" };
        private static readonly string[] IsolatorKeywords = { "iso", "isolator", "isolation" };
        private static readonly string[] RepeaterKeywords = { "rep", "repeater", "booster", "amplifier" };
        
        // Electrical parameter lookup tables
        private static readonly Dictionary<string, int> CommonCandelaValues = new Dictionary<string, int>
        {
            { "15", 15 }, { "30", 30 }, { "75", 75 }, { "95", 95 }, 
            { "110", 110 }, { "135", 135 }, { "185", 185 }
        };
        
        private static readonly Dictionary<string, double> CommonWattageValues = new Dictionary<string, double>
        {
            { "1/8", 0.125 }, { "1/4", 0.25 }, { "1/2", 0.5 }, { "1", 1.0 },
            { "2", 2.0 }, { "4", 4.0 }, { "8", 8.0 }, { "15", 15.0 }, { "30", 30.0 }
        };
        
        public ParameterExtractor()
        {
        }
        
        /// <summary>
        /// Extract all available parameters using multiple strategies
        /// </summary>
        public Dictionary<string, object> ExtractAllParameters(DeviceSnapshot device)
        {
            var parameters = new Dictionary<string, object>();
            
            try
            {
                // Strategy 1: Extract from custom properties (highest priority)
                ExtractFromCustomProperties(device, parameters);
                
                // Strategy 2: Parse family name for embedded parameters
                ExtractFromFamilyName(device.FamilyName, parameters);
                
                // Strategy 3: Parse type name for additional context
                ExtractFromTypeName(device.TypeName, parameters);
                
                // Strategy 4: Infer from device characteristics
                ExtractFromCharacteristics(device, parameters);
                
                // Strategy 5: Electrical parameter inference
                ExtractElectricalParameters(device, parameters);
                
                // Strategy 6: Model number extraction
                ExtractModelNumber(device, parameters);
                
                // Strategy 7: Environmental and mounting inference
                ExtractEnvironmentalParameters(device, parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parameter extraction error: {ex.Message}");
            }
            
            return parameters;
        }
        
        /// <summary>
        /// Extract specific parameter with fallback strategies
        /// </summary>
        public T ExtractParameter<T>(DeviceSnapshot device, string parameterName, T defaultValue = default(T))
        {
            var allParameters = ExtractAllParameters(device);
            
            if (allParameters.TryGetValue(parameterName, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    // Type conversion attempts
                    if (typeof(T) == typeof(int) && int.TryParse(value.ToString(), out var intValue))
                        return (T)(object)intValue;
                    
                    if (typeof(T) == typeof(double) && double.TryParse(value.ToString(), out var doubleValue))
                        return (T)(object)doubleValue;
                    
                    if (typeof(T) == typeof(bool) && bool.TryParse(value.ToString(), out var boolValue))
                        return (T)(object)boolValue;
                    
                    if (typeof(T) == typeof(string))
                        return (T)(object)value.ToString();
                }
                catch
                {
                    // Fall through to default
                }
            }
            
            return defaultValue;
        }
        
        private void ExtractFromCustomProperties(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            if (device.CustomProperties == null) return;
            
            foreach (var kvp in device.CustomProperties)
            {
                var key = kvp.Key.ToUpper();
                var value = kvp.Value;
                
                // Direct parameter mapping
                switch (key)
                {
                    case "CANDELA":
                    case "CANDELA_RATING":
                        if (TryParseNumeric(value, out var candela))
                            parameters["CANDELA"] = candela;
                        break;
                        
                    case "WATTAGE":
                    case "WATTS":
                    case "POWER":
                        if (TryParseNumeric(value, out var watts))
                            parameters["WATTAGE"] = watts;
                        break;
                        
                    case "VOLTAGE":
                    case "VOLTS":
                        if (TryParseNumeric(value, out var volts))
                            parameters["VOLTAGE"] = volts;
                        break;
                        
                    case "SKU":
                    case "MODEL":
                    case "MODEL_NUMBER":
                        parameters["MODEL_NUMBER"] = value?.ToString();
                        break;
                        
                    case "MANUFACTURER":
                    case "MFG":
                        parameters["MANUFACTURER"] = value?.ToString();
                        break;
                        
                    default:
                        parameters[key] = value;
                        break;
                }
            }
        }
        
        private void ExtractFromFamilyName(string familyName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(familyName)) return;
            
            var normalizedName = familyName.ToLower();
            
            // Extract candela rating
            var candelaMatch = CandelaPattern.Match(familyName);
            if (candelaMatch.Success && int.TryParse(candelaMatch.Groups[1].Value, out var candela))
            {
                parameters["CANDELA"] = candela;
            }
            
            // Extract wattage
            var wattageMatch = WattagePattern.Match(familyName);
            if (wattageMatch.Success && double.TryParse(wattageMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var watts))
            {
                parameters["WATTAGE"] = watts;
            }
            
            // Extract voltage
            var voltageMatch = VoltagePattern.Match(familyName);
            if (voltageMatch.Success && int.TryParse(voltageMatch.Groups[1].Value, out var voltage))
            {
                parameters["VOLTAGE"] = voltage;
            }
            
            // Extract model number
            var modelMatch = ModelNumberPattern.Match(familyName);
            if (modelMatch.Success)
            {
                parameters["MODEL_NUMBER"] = modelMatch.Value;
            }
            
            // Device function inference
            if (ContainsAny(normalizedName, StrobeKeywords))
                parameters["HAS_STROBE"] = true;
            
            if (ContainsAny(normalizedName, SpeakerKeywords))
                parameters["HAS_SPEAKER"] = true;
            
            if (ContainsAny(normalizedName, IsolatorKeywords))
                parameters["IS_ISOLATOR"] = true;
            
            if (ContainsAny(normalizedName, RepeaterKeywords))
                parameters["IS_REPEATER"] = true;
            
            // Color extraction
            if (normalizedName.Contains("white") || normalizedName.Contains("wht"))
                parameters["COLOR"] = "WHITE";
            else if (normalizedName.Contains("red"))
                parameters["COLOR"] = "RED";
            
            // Mounting type inference
            if (normalizedName.Contains("ceiling") || normalizedName.Contains("ceil"))
                parameters["MOUNTING_TYPE"] = "CEILING";
            else if (normalizedName.Contains("wall"))
                parameters["MOUNTING_TYPE"] = "WALL";
        }
        
        private void ExtractFromTypeName(string typeName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(typeName)) return;
            
            var normalizedName = typeName.ToLower();
            
            // Additional candela extraction from type name
            var candelaMatch = CandelaPattern.Match(typeName);
            if (candelaMatch.Success && !parameters.ContainsKey("CANDELA"))
            {
                if (int.TryParse(candelaMatch.Groups[1].Value, out var candela))
                {
                    parameters["CANDELA"] = candela;
                }
            }
            
            // Additional wattage extraction
            var wattageMatch = WattagePattern.Match(typeName);
            if (wattageMatch.Success && !parameters.ContainsKey("WATTAGE"))
            {
                if (double.TryParse(wattageMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var watts))
                {
                    parameters["WATTAGE"] = watts;
                }
            }
        }
        
        private void ExtractFromCharacteristics(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            // Use device boolean properties to set parameters
            if (device.HasStrobe && !parameters.ContainsKey("HAS_STROBE"))
                parameters["HAS_STROBE"] = true;
            
            if (device.HasSpeaker && !parameters.ContainsKey("HAS_SPEAKER"))
                parameters["HAS_SPEAKER"] = true;
            
            if (device.IsIsolator && !parameters.ContainsKey("IS_ISOLATOR"))
                parameters["IS_ISOLATOR"] = true;
            
            if (device.IsRepeater && !parameters.ContainsKey("IS_REPEATER"))
                parameters["IS_REPEATER"] = true;
            
            // Infer device function
            var function = InferDeviceFunction(device);
            if (!string.IsNullOrEmpty(function))
                parameters["DEVICE_FUNCTION"] = function;
            
            // Infer missing electrical parameters
            if (device.HasStrobe && device.Amps <= 0)
            {
                parameters["CANDELA"] = InferTypicalCandela(device);
            }
            
            if (device.HasSpeaker && device.Watts <= 0)
            {
                parameters["WATTAGE"] = InferTypicalWattage(device);
            }
        }
        
        private void ExtractElectricalParameters(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            // Use existing device electrical data
            if (device.Watts > 0 && !parameters.ContainsKey("WATTAGE"))
                parameters["WATTAGE"] = device.Watts;
            
            if (device.Amps > 0 && !parameters.ContainsKey("CURRENT_DRAW"))
                parameters["CURRENT_DRAW"] = device.Amps;
            
            if (device.UnitLoads > 0 && !parameters.ContainsKey("UNIT_LOADS"))
                parameters["UNIT_LOADS"] = device.UnitLoads;
            
            // Calculate missing electrical parameters
            if (device.Watts > 0 && device.Amps <= 0)
            {
                var calculatedAmps = device.Watts / 24.0; // Standard 24V calculation
                parameters["CURRENT_DRAW"] = Math.Round(calculatedAmps, 3);
            }
            
            if (device.Amps > 0 && device.Watts <= 0)
            {
                var calculatedWatts = device.Amps * 24.0;
                parameters["WATTAGE"] = Math.Round(calculatedWatts, 2);
            }
        }
        
        private void ExtractModelNumber(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            // Extract from family name
            var modelMatch = ModelNumberPattern.Match(device.FamilyName ?? "");
            if (modelMatch.Success && !parameters.ContainsKey("MODEL_NUMBER"))
            {
                parameters["MODEL_NUMBER"] = modelMatch.Value;
            }
            
            // Extract from type name
            if (!parameters.ContainsKey("MODEL_NUMBER"))
            {
                modelMatch = ModelNumberPattern.Match(device.TypeName ?? "");
                if (modelMatch.Success)
                {
                    parameters["MODEL_NUMBER"] = modelMatch.Value;
                }
            }
        }
        
        private void ExtractEnvironmentalParameters(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            var familyName = (device.FamilyName ?? "").ToLower();
            var typeName = (device.TypeName ?? "").ToLower();
            var combined = $"{familyName} {typeName}";
            
            // Environmental rating inference
            if (combined.Contains("outdoor") || combined.Contains("exterior") || combined.Contains("weatherproof"))
                parameters["ENVIRONMENTAL_RATING"] = "OUTDOOR";
            else if (combined.Contains("indoor") || combined.Contains("interior"))
                parameters["ENVIRONMENTAL_RATING"] = "INDOOR";
            
            // T-Tap compatibility inference
            if (combined.Contains("ttap") || combined.Contains("t-tap") || combined.Contains("tap"))
                parameters["T_TAP_COMPATIBLE"] = true;
            
            // UL listing inference (assume true unless specified otherwise)
            parameters["UL_LISTED"] = true;
        }
        
        private string InferDeviceFunction(DeviceSnapshot device)
        {
            if (device.IsIsolator) return "ISOLATOR";
            if (device.IsRepeater) return "REPEATER";
            if (device.HasStrobe && device.HasSpeaker) return "SPEAKER_STROBE";
            if (device.HasStrobe) return "STROBE";
            if (device.HasSpeaker) return "SPEAKER";
            return null;
        }
        
        private int InferTypicalCandela(DeviceSnapshot device)
        {
            // Default candela values based on common patterns
            return 75; // Most common rating
        }
        
        private double InferTypicalWattage(DeviceSnapshot device)
        {
            // Default wattage values based on common patterns
            return 1.0; // Most common rating
        }
        
        private bool ContainsAny(string text, string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword));
        }
        
        private bool TryParseNumeric(object value, out double result)
        {
            result = 0;
            
            if (value == null) return false;
            
            if (value is double d)
            {
                result = d;
                return true;
            }
            
            if (value is int i)
            {
                result = i;
                return true;
            }
            
            if (value is float f)
            {
                result = f;
                return true;
            }
            
            return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}