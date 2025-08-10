using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Services.Integration;

namespace Revit_FA_Tools
{
    public class ElectricalCalculator
    {
        private ParameterMappingIntegrationService _parameterMappingService;
        
        public ElectricalCalculator()
        {
            _parameterMappingService = new ParameterMappingIntegrationService();
        }
        
        public ElectricalResults CalculateElectricalLoads(List<FamilyInstance> elements)
        {
            var results = new ElectricalResults();
            results.Totals["wattage"] = 0.0;
            results.Totals["current"] = 0.0;

            if (elements == null || !elements.Any())
            {
                return results;
            }

            foreach (var element in elements.Where(e => e != null))
            {
                try
                {
                    var electricalData = GetElectricalParametersWithMapping(element);
                    var familyName = GetFamilyName(element);
                    var levelName = GetLevelName(element);

                    var elementData = new ElementData
                    {
                        Id = element.Id.Value,
                        Element = element,
                        FamilyName = familyName,
                        Wattage = electricalData.Wattage,
                        Current = electricalData.Current,
                        Voltage = electricalData.Voltage,
                        FoundParams = electricalData.FoundParams,
                        CalculatedParams = electricalData.CalculatedParams,
                        LevelName = levelName
                    };

                    results.Elements.Add(elementData);

                    // Update totals
                    results.Totals["wattage"] += electricalData.Wattage;
                    results.Totals["current"] += electricalData.Current;

                    // Update by family totals
                    if (!results.ByFamily.ContainsKey(familyName))
                    {
                        results.ByFamily[familyName] = new FamilyData();
                    }
                    results.ByFamily[familyName].Count++;
                    results.ByFamily[familyName].Wattage += electricalData.Wattage;
                    results.ByFamily[familyName].Current += electricalData.Current;

                    // Update by level totals
                    if (!results.ByLevel.ContainsKey(levelName))
                    {
                        results.ByLevel[levelName] = new LevelData();
                    }
                    results.ByLevel[levelName].Devices++;
                    results.ByLevel[levelName].Current += electricalData.Current;
                    results.ByLevel[levelName].Wattage += electricalData.Wattage;

                    if (!results.ByLevel[levelName].Families.ContainsKey(familyName))
                    {
                        results.ByLevel[levelName].Families[familyName] = 0;
                    }
                    results.ByLevel[levelName].Families[familyName]++;
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other elements
                    System.Diagnostics.Debug.WriteLine($"Error processing element {element.Id}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Determines the appropriate voltage for device based on fire alarm system specifications
        /// </summary>
        private double DetermineDeviceVoltage(FamilyInstance element)
        {
            if (element == null) return 24.0; // Default fire alarm voltage
            
            try
            {
                var familyName = GetFamilyName(element)?.ToUpperInvariant() ?? "";
                var typeName = element.Symbol?.Name?.ToUpperInvariant() ?? "";
                var combinedName = $"{familyName} {typeName}";

                // Check if this is an IDNAC (addressable notification) device
                if (IsIDNACDevice(combinedName))
                {
                    return 29.0; // IDNAC regulated voltage per fire alarm specs
                }
                
                // Check if this is a speaker device (may use 25V or 70.7V)
                if (IsSpeakerDevice(combinedName))
                {
                    // Try to extract voltage from parameters or family name
                    var speakerVoltage = ExtractSpeakerVoltage(element, combinedName);
                    if (speakerVoltage > 0) return speakerVoltage;
                }

                // Use configured nominal voltage for fire alarm devices
                return ConfigurationService.Current.IDNAC.NominalVoltage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error determining device voltage: {ex.Message}");
                return ConfigurationService.Current.IDNAC.NominalVoltage; // Use configured default
            }
        }

        /// <summary>
        /// Determines if device is an IDNAC (addressable notification) device
        /// </summary>
        private bool IsIDNACDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;
            
            return deviceName.Contains("ADDRESSABLE") ||
                   deviceName.Contains("IDNAC") ||
                   (deviceName.Contains("NOTIFICATION") && 
                    (deviceName.Contains("STROBE") || deviceName.Contains("HORN") || deviceName.Contains("SPEAKER")));
        }

        /// <summary>
        /// Determines if device is a speaker device
        /// </summary>
        private bool IsSpeakerDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;
            
            return deviceName.Contains("SPEAKER") ||
                   deviceName.Contains("AUDIO") ||
                   deviceName.Contains("VOICE");
        }

        /// <summary>
        /// Extracts speaker voltage from device parameters or naming
        /// </summary>
        private double ExtractSpeakerVoltage(FamilyInstance element, string deviceName)
        {
            try
            {
                // Check for voltage in family/type name
                if (deviceName.Contains("70V") || deviceName.Contains("70.7V"))
                    return 70.7;
                if (deviceName.Contains("25V"))
                    return 25.0;
                
                // Try to get voltage parameter
                var voltageParams = new[] { "Voltage", "Operating Voltage", "Input Voltage", "Rated Voltage" };
                var voltageResult = GetParameterValueAndName(element, voltageParams);
                
                if (voltageResult.Item1 > 0)
                {
                    var voltage = voltageResult.Item1;
                    // Normalize common speaker voltages
                    if (voltage >= 70.0 && voltage <= 71.0) return 70.7;
                    if (voltage >= 24.5 && voltage <= 25.5) return 25.0;
                    return voltage;
                }

                // Default speaker voltage if not specified
                return 25.0; // 25V is more common for smaller installations
            }
            catch
            {
                return 25.0; // Safe default for speakers
            }
        }

        /// <summary>
        /// Enhanced electrical parameters extraction with parameter mapping integration
        /// </summary>
        private ElectricalData GetElectricalParametersWithMapping(FamilyInstance element)
        {
            try
            {
                // 1. Create basic device snapshot from Revit element
                var basicSnapshot = CreateBasicDeviceSnapshot(element);
                
                // 2. Apply parameter mapping for enhanced specifications
                var comprehensiveResult = _parameterMappingService.ProcessDeviceComprehensively(basicSnapshot);
                
                // 3. Convert enhanced snapshot to ElectricalData
                if (comprehensiveResult.Success && comprehensiveResult.ParameterMapping?.EnhancedSnapshot != null)
                {
                    var enhancedSnapshot = comprehensiveResult.ParameterMapping.EnhancedSnapshot;
                    var electricalData = new ElectricalData
                    {
                        Wattage = enhancedSnapshot.Watts,
                        Current = enhancedSnapshot.Amps,
                        Voltage = DetermineDeviceVoltage(element)
                    };
                    
                    // Add enhanced parameter information
                    electricalData.FoundParams.Add($"Enhanced Wattage: {enhancedSnapshot.Watts} W (from repository)");
                    electricalData.FoundParams.Add($"Enhanced Current: {enhancedSnapshot.Amps:F3} A (from repository)");
                    electricalData.FoundParams.Add($"Voltage: {electricalData.Voltage} V");
                    
                    // Add device specifications if available
                    if (comprehensiveResult.ElectricalSpecifications != null)
                    {
                        var spec = comprehensiveResult.ElectricalSpecifications;
                        electricalData.FoundParams.Add($"SKU: {spec.SKU}");
                        electricalData.FoundParams.Add($"Manufacturer: {spec.Manufacturer}");
                        if (spec.IsTTapCompatible)
                            electricalData.FoundParams.Add("T-Tap Compatible");
                    }
                    
                    return electricalData;
                }
                else
                {
                    // Fallback to basic extraction if parameter mapping fails
                    System.Diagnostics.Debug.WriteLine($"Parameter mapping failed for {GetFamilyName(element)}, using basic extraction");
                    return GetElectricalParameters(element);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parameter mapping integration error: {ex.Message}");
                return GetElectricalParameters(element);
            }
        }
        
        /// <summary>
        /// Create basic DeviceSnapshot from Revit FamilyInstance
        /// </summary>
        private Models.DeviceSnapshot CreateBasicDeviceSnapshot(FamilyInstance element)
        {
            var elementId = (int)element.Id.Value;
            var levelParam = element.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            var levelName = levelParam?.AsValueString() ?? GetLevelName(element);
            var familyName = GetFamilyName(element);
            var typeName = element.Symbol?.Name ?? "Unknown";
            
            // Extract basic parameters from Revit
            var customProps = new Dictionary<string, object>();
            
            // Try to extract CANDELA parameter
            var candelaValue = GetCandelaParameter(element);
            if (!string.IsNullOrEmpty(candelaValue))
                customProps["CANDELA"] = candelaValue;
            
            // Try to extract WATTAGE parameter
            var wattageValue = ExtractParameterValue(element, "Wattage") ?? ExtractParameterValue(element, "WATTAGE");
            if (wattageValue.HasValue)
                customProps["WATTAGE"] = wattageValue.Value;
            
            // Try to extract current draw
            var currentValue = ExtractParameterValue(element, "CURRENT DRAW");
            if (currentValue.HasValue)
                customProps["CURRENT_DRAW"] = currentValue.Value;
            
            return new Models.DeviceSnapshot(
                ElementId: elementId,
                LevelName: levelName,
                FamilyName: familyName,
                TypeName: typeName,
                Watts: wattageValue ?? 0,
                Amps: currentValue ?? 0,
                UnitLoads: 1, // Default
                HasStrobe: DetermineHasStrobe(familyName, typeName),
                HasSpeaker: DetermineHasSpeaker(familyName, typeName),
                IsIsolator: DetermineIsIsolator(familyName, typeName),
                IsRepeater: DetermineIsRepeater(familyName, typeName),
                CustomProperties: customProps
            );
        }
        
        /// <summary>
        /// Helper method to get CANDELA parameter value
        /// </summary>
        private string GetCandelaParameter(FamilyInstance element)
        {
            var candelaParams = new[] { "CANDELA", "Candela", "candela" };
            return GetCandelaValueAsString(element, candelaParams);
        }
        
        /// <summary>
        /// Helper method to extract numeric parameter value
        /// </summary>
        private double? ExtractParameterValue(Element element, string paramName)
        {
            var result = GetParameterValueAndName(element, new[] { paramName });
            return result.Item1 > 0 ? result.Item1 : null;
        }
        
        /// <summary>
        /// Determine if device has strobe functionality
        /// </summary>
        private bool DetermineHasStrobe(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpper();
            return combined.Contains("STROBE") || combined.Contains("FLASH") || combined.Contains("LIGHT");
        }
        
        /// <summary>
        /// Determine if device has speaker functionality  
        /// </summary>
        private bool DetermineHasSpeaker(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpper();
            return combined.Contains("SPEAKER") || combined.Contains("HORN") || combined.Contains("AUDIO") || combined.Contains("VOICE");
        }
        
        /// <summary>
        /// Determine if device is an isolator
        /// </summary>
        private bool DetermineIsIsolator(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpper();
            return combined.Contains("ISOLATOR") || combined.Contains("ISO");
        }
        
        /// <summary>
        /// Determine if device is a repeater
        /// </summary>
        private bool DetermineIsRepeater(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpper();
            return combined.Contains("REPEATER") || combined.Contains("REP") || combined.Contains("BOOSTER");
        }

        private ElectricalData GetElectricalParameters(FamilyInstance element)
        {
            var electricalData = new ElectricalData();

            if (element == null)
                return electricalData;

            try
            {
                // Check for CANDELA parameter first and calculate current if needed
                ProcessCandelaParameter(element);
                
                // Only use specific parameters: CURRENT DRAW and Wattage
                var wattageParams = new[] { "Wattage" };
                var currentParams = new[] { "CURRENT DRAW" };

                // Extract values and track what was found
                var wattageResult = GetParameterValueAndName(element, wattageParams);
                var currentResult = GetParameterValueAndName(element, currentParams);

                electricalData.Wattage = wattageResult.Item1;
                electricalData.Current = currentResult.Item1;
                
                // Set voltage based on device type for accurate calculations
                electricalData.Voltage = DetermineDeviceVoltage(element);

                // Track which parameters were actually found in the family
                if (!string.IsNullOrEmpty(wattageResult.Item2))
                {
                    electricalData.FoundParams.Add($"Wattage: {wattageResult.Item1} W");
                }
                if (!string.IsNullOrEmpty(currentResult.Item2))
                {
                    electricalData.FoundParams.Add($"Current: {currentResult.Item1} A");
                }
                electricalData.FoundParams.Add($"Voltage: {electricalData.Voltage} V");

                // Try to get values from type parameters if instance parameters are missing
                if ((electricalData.Wattage == 0 || electricalData.Current == 0) && element.Symbol != null)
                {
                    var typeElement = element.Symbol;

                    if (electricalData.Wattage == 0)
                    {
                        var typeWattageResult = GetParameterValueAndName(typeElement, wattageParams);
                        if (typeWattageResult.Item1 > 0)
                        {
                            electricalData.Wattage = typeWattageResult.Item1;
                            electricalData.FoundParams.Add($"Wattage: {typeWattageResult.Item1} W (from type)");
                        }
                    }

                    if (electricalData.Current == 0)
                    {
                        var typeCurrentResult = GetParameterValueAndName(typeElement, currentParams);
                        if (typeCurrentResult.Item1 > 0)
                        {
                            electricalData.Current = typeCurrentResult.Item1;
                            electricalData.FoundParams.Add($"Current: {typeCurrentResult.Item1} A (from type)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but return what we have
                System.Diagnostics.Debug.WriteLine($"Error getting electrical parameters: {ex.Message}");
            }

            return electricalData;
        }

        private Tuple<double, string> GetParameterValueAndName(Element element, string[] paramNames)
        {
            if (element == null || paramNames == null)
                return new Tuple<double, string>(0.0, null);

            foreach (var paramName in paramNames)
            {
                if (string.IsNullOrEmpty(paramName))
                    continue;

                try
                {
                    var param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        double value = 0.0;

                        switch (param.StorageType)
                        {
                            case StorageType.Double:
                                // Always use AsValueString() to get the properly formatted value with units
                                var valueString = param.AsValueString();
                                if (!string.IsNullOrEmpty(valueString))
                                {
                                    value = ParseNumericValue(valueString);
                                }
                                else
                                {
                                    // If AsValueString() is empty, use raw value
                                    value = param.AsDouble();
                                }
                                break;

                            case StorageType.Integer:
                                value = param.AsInteger();
                                break;

                            case StorageType.String:
                                var stringValue = param.AsString();
                                if (!string.IsNullOrEmpty(stringValue))
                                {
                                    value = ParseNumericValue(stringValue);
                                }
                                break;

                            default:
                                continue;
                        }

                        // Convert KVA to VA if needed
                        if (paramName.ToUpper().Contains("KVA"))
                        {
                            value = value * 1000;
                        }

                        if (value > 0)
                        {
                            return new Tuple<double, string>(value, paramName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log parameter access error but continue
                    System.Diagnostics.Debug.WriteLine($"Error accessing parameter {paramName}: {ex.Message}");
                    continue;
                }
            }

            return new Tuple<double, string>(0.0, null);
        }

        private double ParseNumericValue(string valueString)
        {
            if (string.IsNullOrEmpty(valueString))
                return 0.0;

            try
            {
                // Remove common unit symbols and extra whitespace
                var cleanValue = Regex.Replace(valueString, @"[WwAaVv]", "")
                                     .Replace("VA", "")
                                     .Replace("KVA", "")
                                     .Replace("kva", "")
                                     .Replace("kVA", "")
                                     .Trim();

                // Handle different decimal separators
                if (double.TryParse(cleanValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }

                // Try with current culture if invariant fails
                if (double.TryParse(cleanValue, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                {
                    return result;
                }

                // Extract just the numeric part using regex
                var match = Regex.Match(cleanValue, @"[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?");
                if (match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing numeric value '{valueString}': {ex.Message}");
            }

            return 0.0;
        }

        private string GetFamilyName(FamilyInstance element)
        {
            try
            {
                if (element?.Symbol?.Family != null)
                {
                    return element.Symbol.Family.Name;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting family name: {ex.Message}");
            }

            return "Unknown";
        }

        private string GetLevelName(FamilyInstance element)
        {
            if (element == null)
                return "Unknown Level";

            try
            {
                // Try to get level from element host first
                if (element.Host is Level hostLevel)
                {
                    return hostLevel.Name ?? "Unknown Level";
                }

                // Try to get level from LevelId
                if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
                {
                    var levelElement = element.Document?.GetElement(element.LevelId);
                    if (levelElement is Level validLevel)
                    {
                        return validLevel.Name ?? "Unknown Level";
                    }
                }

                // Try to get level parameter directly
                var levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam != null && levelParam.HasValue)
                {
                    var levelId = levelParam.AsElementId();
                    if (levelId != null && levelId != ElementId.InvalidElementId)
                    {
                        var levelElement = element.Document?.GetElement(levelId);
                        if (levelElement is Level paramLevel)
                        {
                            return paramLevel.Name ?? "Unknown Level";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting level name: {ex.Message}");
            }

            return "Unknown Level";
        }

        /// <summary>
        /// Process CANDELA parameter for notification devices and set corresponding current draw
        /// </summary>
        private void ProcessCandelaParameter(FamilyInstance element)
        {
            try
            {
                // Check if this is a notification device that might have candela settings
                var familyName = element.Symbol?.FamilyName?.ToUpper() ?? "";
                var typeName = element.Symbol?.Name?.ToUpper() ?? "";
                var combinedName = $"{familyName} {typeName}";

                if (!IsNotificationDevice(combinedName))
                    return;

                // Look for CANDELA parameter
                var candelaParams = new[] { "CANDELA", "Candela", "candela" };
                var candelaResult = GetCandelaValueAsString(element, candelaParams);

                if (!string.IsNullOrEmpty(candelaResult))
                {
                    // Get corresponding current draw from fire alarm spec
                    var currentDraw = GetCurrentDrawFromCandela(candelaResult, combinedName);
                    
                    if (currentDraw > 0)
                    {
                        // Set the CURRENT DRAW parameter
                        SetCurrentDrawParameter(element, currentDraw);
                        System.Diagnostics.Debug.WriteLine($"CANDELA Integration: Set {currentDraw}A for {candelaResult} candela on {element.Symbol?.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing CANDELA parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if device is a notification device that would have candela settings
        /// </summary>
        private bool IsNotificationDevice(string deviceName)
        {
            return deviceName.Contains("STROBE") || 
                   deviceName.Contains("SPEAKER") && deviceName.Contains("STROBE") ||
                   deviceName.Contains("HORN") && deviceName.Contains("STROBE") ||
                   deviceName.Contains("NOTIFICATION") ||
                   deviceName.Contains("COMBO");
        }

        /// <summary>
        /// Get CANDELA parameter value as string (preserving the AsValueString format)
        /// </summary>
        private string GetCandelaValueAsString(Element element, string[] paramNames)
        {
            if (element == null || paramNames == null)
                return null;

            foreach (var paramName in paramNames)
            {
                try
                {
                    var param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        var valueString = param.AsValueString();
                        if (!string.IsNullOrEmpty(valueString))
                        {
                            return valueString.Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing CANDELA parameter {paramName}: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Get current draw based on candela setting according to fire alarm specifications
        /// </summary>
        private double GetCurrentDrawFromCandela(string candelaValue, string deviceName)
        {
            // Load candela to current mapping from JSON configuration
            var config = CandelaConfigurationService.LoadConfiguration();
            if (config?.DeviceTypes == null)
            {
                System.Diagnostics.Debug.WriteLine("CANDELA Integration: No configuration available");
                return 0.0;
            }

            // Clean up candela value (remove "cd", "CD", or other units)
            var cleanCandela = Regex.Replace(candelaValue.ToUpper(), @"[^0-9]", "").Trim();
            
            // Determine device type based on mounting and environmental characteristics
            string deviceType = DetermineMountingAndDeviceType(deviceName, config.RecognitionPatterns);

            // Look up current draw with fallback hierarchy
            var currentDraw = LookupCurrentDrawWithFallback(config, deviceType, cleanCandela);
            if (currentDraw > 0)
                return currentDraw;

            System.Diagnostics.Debug.WriteLine($"CANDELA Integration: No mapping found for {candelaValue} on {deviceType}");
            return 0.0;
        }

        /// <summary>
        /// Lookup current draw with fallback to similar device types if exact match not found
        /// </summary>
        private double LookupCurrentDrawWithFallback(CandelaConfiguration config, string deviceType, string cleanCandela)
        {
            try
            {
                // Validate inputs
                if (config?.DeviceTypes == null || string.IsNullOrEmpty(deviceType) || string.IsNullOrEmpty(cleanCandela))
                {
                    System.Diagnostics.Debug.WriteLine("CANDELA Integration: Invalid parameters for lookup");
                    return 0.0;
                }
                
                // Try exact device type match first
                if (config.DeviceTypes.ContainsKey(deviceType) && 
                    config.DeviceTypes[deviceType]?.CandelaCurrentMap != null &&
                    config.DeviceTypes[deviceType].CandelaCurrentMap.ContainsKey(cleanCandela))
                {
                    System.Diagnostics.Debug.WriteLine($"CANDELA Integration: Exact match found for {deviceType} at {cleanCandela}cd");
                    return config.DeviceTypes[deviceType].CandelaCurrentMap[cleanCandela];
                }

                // Try to find closest candela value for the exact device type
                if (config.DeviceTypes.ContainsKey(deviceType) && config.DeviceTypes[deviceType]?.CandelaCurrentMap != null)
                {
                    var deviceMap = config.DeviceTypes[deviceType].CandelaCurrentMap;
                    var closest = FindClosestCandela(deviceMap, cleanCandela);
                    if (closest.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"CANDELA Integration: Using closest match {closest.Value.Key}cd for requested {cleanCandela}cd on {deviceType}");
                        return closest.Value.Value;
                    }
                }

                // Fallback hierarchy: try similar device types
                var fallbackTypes = GetFallbackDeviceTypes(config, deviceType);
                foreach (var fallbackType in fallbackTypes)
                {
                    if (config.DeviceTypes.ContainsKey(fallbackType) && 
                        config.DeviceTypes[fallbackType]?.CandelaCurrentMap != null &&
                        config.DeviceTypes[fallbackType].CandelaCurrentMap.ContainsKey(cleanCandela))
                    {
                        System.Diagnostics.Debug.WriteLine($"CANDELA Integration: Using fallback {fallbackType} for {deviceType} at {cleanCandela}cd");
                        return config.DeviceTypes[fallbackType].CandelaCurrentMap[cleanCandela];
                    }

                    // Try closest match on fallback type
                    if (config.DeviceTypes.ContainsKey(fallbackType) && config.DeviceTypes[fallbackType]?.CandelaCurrentMap != null)
                    {
                        var deviceMap = config.DeviceTypes[fallbackType].CandelaCurrentMap;
                        var closest = FindClosestCandela(deviceMap, cleanCandela);
                        if (closest.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"CANDELA Integration: Using fallback {fallbackType} closest match {closest.Value.Key}cd for {deviceType} requested {cleanCandela}cd");
                            return closest.Value.Value;
                        }
                    }
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in candela lookup: {ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// Find closest candela value in device map
        /// </summary>
        private KeyValuePair<string, double>? FindClosestCandela(Dictionary<string, double> deviceMap, string cleanCandela)
        {
            try
            {
                if (deviceMap == null || deviceMap.Count == 0 || string.IsNullOrEmpty(cleanCandela))
                    return null;
                    
                if (int.TryParse(cleanCandela, out int candelaInt))
                {
                    var closest = deviceMap
                        .Select(kvp => new { 
                            Key = kvp.Key, 
                            Value = kvp.Value, 
                            IntValue = int.TryParse(kvp.Key, out int v) ? v : 0, 
                            Diff = Math.Abs((int.TryParse(kvp.Key, out int v2) ? v2 : 0) - candelaInt) 
                        })
                        .Where(x => x.IntValue > 0)
                        .OrderBy(x => x.Diff)
                        .FirstOrDefault();

                    if (closest != null)
                    {
                        return new KeyValuePair<string, double>(closest.Key, closest.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding closest candela match: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get fallback device types in order of preference using JSON configuration
        /// </summary>
        private List<string> GetFallbackDeviceTypes(CandelaConfiguration config, string deviceType)
        {
            var fallbacks = new List<string>();

            // First try to get fallbacks from JSON configuration
            if (config.FallbackHierarchy != null && config.FallbackHierarchy.ContainsKey(deviceType))
            {
                fallbacks.AddRange(config.FallbackHierarchy[deviceType]);
            }
            else
            {
                // Fall back to hard-coded logic if not in JSON
                // For ceiling devices, try wall then weatherproof
                if (deviceType.StartsWith("CEILING_"))
                {
                    var baseType = deviceType.Substring("CEILING_".Length);
                    fallbacks.Add("WALL_" + baseType);
                    fallbacks.Add("WEATHERPROOF_" + baseType);
                }
                // For weatherproof devices, try wall then ceiling
                else if (deviceType.StartsWith("WEATHERPROOF_"))
                {
                    var baseType = deviceType.Substring("WEATHERPROOF_".Length);
                    fallbacks.Add("WALL_" + baseType);
                    fallbacks.Add("CEILING_" + baseType);
                }
                // For wall devices, try ceiling then weatherproof
                else if (deviceType.StartsWith("WALL_"))
                {
                    var baseType = deviceType.Substring("WALL_".Length);
                    fallbacks.Add("CEILING_" + baseType);
                    fallbacks.Add("WEATHERPROOF_" + baseType);
                }

                // Add generic fallbacks for device function types
                if (deviceType.Contains("SPEAKER_STROBE"))
                {
                    fallbacks.Add("WALL_SPEAKER_STROBE");
                    fallbacks.Add("CEILING_SPEAKER_STROBE");
                    fallbacks.Add("WEATHERPROOF_SPEAKER_STROBE");
                }
                else if (deviceType.Contains("HORN_STROBE"))
                {
                    fallbacks.Add("WALL_HORN_STROBE");
                    fallbacks.Add("CEILING_HORN_STROBE");
                    fallbacks.Add("WEATHERPROOF_HORN_STROBE");
                }
                else if (deviceType.Contains("STROBE"))
                {
                    fallbacks.Add("WALL_STROBE");
                    fallbacks.Add("CEILING_STROBE");
                    fallbacks.Add("WEATHERPROOF_STROBE");
                }
            }

            return fallbacks.Distinct().Where(f => f != deviceType).ToList();
        }

        /// <summary>
        /// Determine device type based on mounting configuration and environmental characteristics using JSON patterns
        /// </summary>
        private string DetermineMountingAndDeviceType(string deviceName, DeviceRecognitionPatterns patterns = null)
        {
            var upperDeviceName = deviceName.ToUpper();
            
            // Determine mounting type using JSON patterns if available
            bool isCeiling = false;
            bool isWeatherproof = false;
            
            if (patterns?.MountingTypes != null)
            {
                // Check for ceiling patterns from JSON
                if (patterns.MountingTypes.ContainsKey("Ceiling"))
                {
                    isCeiling = patterns.MountingTypes["Ceiling"].Any(pattern => upperDeviceName.Contains(pattern.ToUpper()));
                }
                
                // Check for weatherproof patterns from JSON
                if (patterns.MountingTypes.ContainsKey("Weatherproof"))
                {
                    isWeatherproof = patterns.MountingTypes["Weatherproof"].Any(pattern => upperDeviceName.Contains(pattern.ToUpper()));
                }
            }
            else
            {
                // Fallback to hard-coded patterns if JSON not available
                isCeiling = upperDeviceName.Contains("CEILING") || 
                           upperDeviceName.Contains("RECESSED") ||
                           upperDeviceName.Contains("SURFACE") && upperDeviceName.Contains("MOUNT") ||
                           upperDeviceName.Contains("FLUSH");
                           
                isWeatherproof = upperDeviceName.Contains("WEATHERPROOF") ||
                               upperDeviceName.Contains("WEATHER RESISTANT") ||
                               upperDeviceName.Contains("OUTDOOR") ||
                               upperDeviceName.Contains("EXTERIOR") ||
                               upperDeviceName.Contains("WP") ||
                               upperDeviceName.Contains("IP65") ||
                               upperDeviceName.Contains("IP66") ||
                               upperDeviceName.Contains("IP67") ||
                               upperDeviceName.Contains("NEMA") ||
                               upperDeviceName.Contains("MARINE") ||
                               upperDeviceName.Contains("SEALED");
            }

            // Determine device function using JSON patterns if available
            bool hasSpeaker = false;
            bool hasHorn = false;
            
            if (patterns?.DeviceFunctions != null)
            {
                if (patterns.DeviceFunctions.ContainsKey("Speaker"))
                {
                    hasSpeaker = patterns.DeviceFunctions["Speaker"].Any(pattern => upperDeviceName.Contains(pattern.ToUpper()));
                }
                
                if (patterns.DeviceFunctions.ContainsKey("Horn"))
                {
                    hasHorn = patterns.DeviceFunctions["Horn"].Any(pattern => upperDeviceName.Contains(pattern.ToUpper()) && !upperDeviceName.Contains("STROBE"));
                }
            }
            else
            {
                // Fallback to hard-coded patterns
                hasSpeaker = upperDeviceName.Contains("SPEAKER") || 
                            upperDeviceName.Contains("VOICE") ||
                            upperDeviceName.Contains("AUDIO");
                            
                hasHorn = upperDeviceName.Contains("HORN") && !upperDeviceName.Contains("STROBE");
            }
            
            // Build device type string
            string mountPrefix = "";
            if (isWeatherproof)
                mountPrefix = "WEATHERPROOF_";
            else if (isCeiling)
                mountPrefix = "CEILING_";
            else
                mountPrefix = "WALL_"; // Default to wall mount
                
            // Determine audio component
            if (hasSpeaker)
                return mountPrefix + "SPEAKER_STROBE";
            else if (hasHorn)
                return mountPrefix + "HORN_STROBE";
            else
                return mountPrefix + "STROBE"; // Default to strobe only
        }

        /// <summary>
        /// Set the CURRENT DRAW parameter on the family instance
        /// </summary>
        private void SetCurrentDrawParameter(FamilyInstance element, double currentDraw)
        {
            try
            {
                var currentParam = element.LookupParameter("CURRENT DRAW");
                if (currentParam != null && !currentParam.IsReadOnly)
                {
                    // Set the current draw value directly
                    currentParam.Set(currentDraw);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"CANDELA Integration: CURRENT DRAW parameter not found or read-only on {element.Symbol?.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting CURRENT DRAW parameter: {ex.Message}");
            }
        }
    }
}