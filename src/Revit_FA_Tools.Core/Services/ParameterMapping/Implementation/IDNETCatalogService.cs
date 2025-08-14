using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Revit_FA_Tools.Core.Services.ParameterMapping.Implementation
{
    /// <summary>
    /// Service for loading and querying the AutoCall 4100ES IDNET device catalog
    /// Maps IDNET initiating device family names and types to specific device specifications with current draws
    /// </summary>
    public class IDNETCatalogService : IFireAlarmCatalogService
    {
        private static IDNETDeviceCatalog _idnetCatalog;
        private static Dictionary<string, IDNETDeviceSpec> _idnetDeviceLookup;
        private static Dictionary<string, string> _idnetFamilyMappings;
        private static readonly object _lockObject = new object();
        
        public IDNETCatalogService()
        {
            EnsureIDNETCatalogLoaded();
        }
        
        #region IFireAlarmCatalogService Implementation
        
        /// <summary>
        /// Interface implementation for generic device spec finding
        /// </summary>
        public IDeviceSpecResult FindDeviceSpec(string familyName, string typeName, string candela = null, double wattage = 0)
        {
            return FindIDNETDeviceSpec(familyName, typeName, candela, wattage);
        }
        
        /// <summary>
        /// Interface implementation for catalog stats
        /// </summary>
        public ICatalogStats GetCatalogStats()
        {
            return GetIDNETCatalogStats();
        }
        
        /// <summary>
        /// Interface implementation for catalog testing
        /// </summary>
        public string TestCatalogLoading()
        {
            return TestIDNETCatalogLoading();
        }
        
        #endregion
        
        /// <summary>
        /// Find IDNET device specification by family name and type name
        /// </summary>
        public IDNETDeviceSpecResult FindIDNETDeviceSpec(string familyName, string typeName, string candela = null, double wattage = 0)
        {
            try
            {
                EnsureIDNETCatalogLoaded();
                
                var result = new IDNETDeviceSpecResult
                {
                    FamilyName = familyName,
                    TypeName = typeName,
                    FoundMatch = false,
                    Current = 0,
                    UnitLoads = 1,
                    Source = "Unknown"
                };
                
                // Strategy 1: Direct lookup by combined name
                var combinedName = $"{familyName} {typeName}".Trim();
                var spec = FindIDNETByDescription(combinedName, candela);
                if (spec != null)
                {
                    result.FoundMatch = true;
                    result.Current = spec.StandbyCurrent;
                    result.UnitLoads = spec.UnitLoads;
                    result.Source = "IDNET Direct Match";
                    result.IDNETDeviceSpec = spec;
                    return result;
                }
                
                // Strategy 2: Pattern-based matching
                spec = FindIDNETByPatterns(familyName, typeName, candela);
                if (spec != null)
                {
                    result.FoundMatch = true;
                    result.Current = spec.StandbyCurrent;
                    result.UnitLoads = spec.UnitLoads;
                    result.Source = "IDNET Pattern Match";
                    result.IDNETDeviceSpec = spec;
                    return result;
                }
                
                // Strategy 3: Family mapping lookup
                if (_idnetFamilyMappings != null)
                {
                    var familyKey = GetBestIDNETFamilyMatch(familyName, typeName);
                    if (!string.IsNullOrEmpty(familyKey))
                    {
                        spec = GetDefaultIDNETSpecForFamily(familyKey, candela);
                        if (spec != null)
                        {
                            result.FoundMatch = true;
                            result.Current = spec.StandbyCurrent;
                            result.UnitLoads = spec.UnitLoads;
                            result.Source = "IDNET Family Mapping";
                            result.IDNETDeviceSpec = spec;
                            return result;
                        }
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding IDNET device spec: {ex.Message}");
                return new IDNETDeviceSpecResult
                {
                    FamilyName = familyName,
                    TypeName = typeName,
                    FoundMatch = false,
                    Current = 0,
                    UnitLoads = 1,
                    Source = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Get IDNET catalog statistics
        /// </summary>
        public IDNETCatalogStats GetIDNETCatalogStats()
        {
            EnsureIDNETCatalogLoaded();
            
            return new IDNETCatalogStats
            {
                TotalIDNETDevices = _idnetDeviceLookup?.Count ?? 0,
                IDNETCatalogLoaded = _idnetCatalog != null,
                Version = _idnetCatalog?.Version ?? "Unknown",
                LastUpdated = _idnetCatalog?.LastUpdated ?? DateTime.MinValue
            };
        }
        
        /// <summary>
        /// Test the IDNET catalog loading and provide diagnostic information
        /// </summary>
        public string TestIDNETCatalogLoading()
        {
            try
            {
                EnsureIDNETCatalogLoaded();
                
                var stats = GetIDNETCatalogStats();
                var result = new List<string>
                {
                    "=== IDNET Device Catalog Test Results ===",
                    $"IDNET Catalog Loaded: {stats.IDNETCatalogLoaded}",
                    $"Version: {stats.Version}",
                    $"Last Updated: {stats.LastUpdated:yyyy-MM-dd}",
                    $"Total IDNET Device Specs: {stats.TotalIDNETDevices}",
                    $"IDNET Family Mappings: {_idnetFamilyMappings?.Count ?? 0}"
                };
                
                // Test a few sample IDNET lookups
                if (stats.IDNETCatalogLoaded)
                {
                    result.Add("--- Sample IDNET Lookups ---");
                    
                    var testCases = new[]
                    {
                        new { Family = "detectors", Type = "smoke", Expected = "0.8mA (1 UL)" },
                        new { Family = "detectors", Type = "heat", Expected = "0.8mA (1 UL)" },
                        new { Family = "pullStations", Type = "standard", Expected = "0.8mA (1 UL)" }
                    };
                    
                    foreach (var test in testCases)
                    {
                        var testResult = FindIDNETDeviceSpec(test.Family, test.Type);
                        result.Add($"{test.Family} {test.Type}: Match={testResult.FoundMatch}, Current={testResult.Current:F3}A, Source={testResult.Source}");
                    }
                }
                
                result.Add("==========================================");
                return string.Join("\n", result);
            }
            catch (Exception ex)
            {
                return $"IDNET Device Catalog Test FAILED: {ex.Message}";
            }
        }
        
        private void EnsureIDNETCatalogLoaded()
        {
            if (_idnetCatalog != null) return;
            
            lock (_lockObject)
            {
                if (_idnetCatalog != null) return;
                
                LoadIDNETDeviceCatalog();
                BuildIDNETLookupTables();
            }
        }
        
        private void LoadIDNETDeviceCatalog()
        {
            try
            {
                string catalogJson = null;
                
                // Try loading from file system first
                var catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DeviceCatalogs", "IDNET_Catalog.json");
                if (File.Exists(catalogPath))
                {
                    catalogJson = File.ReadAllText(catalogPath);
                    System.Diagnostics.Debug.WriteLine($"Loaded IDNET device catalog from: {catalogPath}");
                }
                
                // Fallback: try embedded resource
                if (string.IsNullOrEmpty(catalogJson))
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(r => r.EndsWith("IDNET_Catalog.json"));
                    
                    if (resourceName != null)
                    {
                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        using (var reader = new StreamReader(stream))
                        {
                            catalogJson = reader.ReadToEnd();
                            System.Diagnostics.Debug.WriteLine($"Loaded IDNET device catalog from embedded resource: {resourceName}");
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(catalogJson))
                {
                    var jObject = JObject.Parse(catalogJson);
                    _idnetCatalog = ParseIDNETDeviceCatalog(jObject);
                    System.Diagnostics.Debug.WriteLine($"IDNET device catalog loaded successfully. Version: {_idnetCatalog.Version}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: IDNET device catalog not found. Creating minimal fallback catalog.");
                    _idnetCatalog = CreateFallbackIDNETCatalog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to load IDNET device catalog: {ex.Message}");
                _idnetCatalog = CreateFallbackIDNETCatalog();
            }
        }
        
        private IDNETDeviceCatalog ParseIDNETDeviceCatalog(JObject jObject)
        {
            var catalog = new IDNETDeviceCatalog
            {
                Version = jObject["version"]?.ToString() ?? "unknown",
                LastUpdated = DateTime.TryParse(jObject["lastUpdated"]?.ToString(), out var date) ? date : DateTime.MinValue,
                Description = jObject["description"]?.ToString() ?? "",
                IDNETDevices = new List<IDNETDeviceSpec>()
            };
            
            // Parse device families for IDNET devices
            var deviceFamilies = jObject["deviceFamilies"] as JObject;
            if (deviceFamilies != null)
            {
                foreach (var familyKvp in deviceFamilies)
                {
                    var familyName = familyKvp.Key;
                    var familyObj = familyKvp.Value as JObject;
                    ParseIDNETDeviceFamily(catalog, familyName, familyObj);
                }
            }
            
            // Parse family mappings for IDNET devices - create from device families structure
            _idnetFamilyMappings = new Dictionary<string, string>();
            if (deviceFamilies != null)
            {
                foreach (var familyKvp in deviceFamilies)
                {
                    var familyName = familyKvp.Key;
                    _idnetFamilyMappings[familyName.ToLowerInvariant()] = familyName;
                    
                    // Add common aliases for IDNET device families
                    switch (familyName.ToLowerInvariant())
                    {
                        case "detectors":
                            _idnetFamilyMappings["detector"] = familyName;
                            _idnetFamilyMappings["smoke"] = familyName;
                            _idnetFamilyMappings["heat"] = familyName;
                            _idnetFamilyMappings["beam"] = familyName;
                            break;
                        case "pullstations":
                            _idnetFamilyMappings["pull"] = familyName;
                            _idnetFamilyMappings["manual"] = familyName;
                            _idnetFamilyMappings["station"] = familyName;
                            break;
                        case "inputoutputmodules":
                            _idnetFamilyMappings["relay"] = familyName;
                            _idnetFamilyMappings["input"] = familyName;
                            _idnetFamilyMappings["output"] = familyName;
                            _idnetFamilyMappings["module"] = familyName;
                            break;
                    }
                }
            }
            
            return catalog;
        }
        
        private void ParseIDNETDeviceFamily(IDNETDeviceCatalog catalog, string familyName, JObject familyObj)
        {
            try
            {
                var deviceTypes = familyObj["deviceTypes"] as JObject;
                if (deviceTypes == null) return;
                
                foreach (var typeKvp in deviceTypes)
                {
                    var typeName = typeKvp.Key; // photoelectric, ionization, etc.
                    var typeObj = typeKvp.Value as JObject;
                    ParseIDNETDeviceType(catalog, familyName, typeName, typeObj);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNET device family {familyName}: {ex.Message}");
            }
        }
        
        private void ParseIDNETDeviceType(IDNETDeviceCatalog catalog, string familyName, string typeName, JObject typeObj)
        {
            try
            {
                // Navigate through the nested structure for IDNET devices
                ParseNestedIDNETDeviceSpecs(catalog, familyName, typeName, typeObj, "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNET device type {familyName}/{typeName}: {ex.Message}");
            }
        }
        
        private void ParseNestedIDNETDeviceSpecs(IDNETDeviceCatalog catalog, string familyName, string typeName, JObject obj, string path)
        {
            foreach (var kvp in obj)
            {
                var key = kvp.Key;
                var value = kvp.Value;
                
                if (value is JObject deviceObj)
                {
                    // Check if this object looks like a device specification (has SKU and description)
                    if (deviceObj["sku"] != null || deviceObj["description"] != null)
                    {
                        var device = ParseIDNETDeviceSpec(familyName, typeName, deviceObj, path);
                        if (device != null)
                        {
                            catalog.IDNETDevices.Add(device);
                        }
                    }
                    else
                    {
                        // Continue drilling down - this is another nested level
                        var newPath = string.IsNullOrEmpty(path) ? key : $"{path}/{key}";
                        ParseNestedIDNETDeviceSpecs(catalog, familyName, typeName, deviceObj, newPath);
                    }
                }
            }
        }
        
        private IDNETDeviceSpec ParseIDNETDeviceSpec(string familyName, string typeName, JObject deviceObj, string path)
        {
            try
            {
                var unitLoads = (int)(deviceObj["unitLoads"] ?? 1);
                var addresses = (int)(deviceObj["addresses"] ?? 1);
                
                // Calculate current from unit loads using IDNET specification: 0.8mA per unit load
                var standbyCurrent = unitLoads * 0.0008; // 0.8mA = 0.0008A per unit load
                var alarmCurrent = standbyCurrent; // For IDNET devices, alarm current is same as standby
                
                return new IDNETDeviceSpec
                {
                    FamilyName = familyName,
                    TypeName = typeName,
                    SKU = deviceObj["sku"]?.ToString() ?? "",
                    PartCode = deviceObj["partCode"]?.ToString() ?? "",
                    Description = deviceObj["description"]?.ToString() ?? "",
                    StandbyCurrent = standbyCurrent,
                    AlarmCurrent = alarmCurrent,
                    UnitLoads = unitLoads,
                    DeviceType = deviceObj["deviceType"]?.ToString() ?? deviceObj["sensorType"]?.ToString() ?? deviceObj["moduleType"]?.ToString() ?? typeName,
                    Sensitivity = deviceObj["sensitivity"]?.ToString() ?? deviceObj["activationTemp"]?.ToString() ?? "",
                    OperatingVoltage = "24VDC", // IDNET standard
                    EnvironmentalRating = deviceObj["environmentalRating"]?.ToString() ?? "indoor",
                    PointType = deviceObj["pointType"]?.ToString() ?? "",
                    Addresses = addresses,
                    Path = path
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNET device spec: {ex.Message}");
                return null;
            }
        }
        
        private double? ParseCurrentValue(JToken currentToken)
        {
            if (currentToken == null) return null;
            
            var currentStr = currentToken.ToString();
            if (string.IsNullOrEmpty(currentStr)) return null;
            
            // Handle various current formats (mA, A, etc.)
            currentStr = currentStr.ToLowerInvariant().Replace("ma", "").Replace("a", "").Trim();
            
            if (double.TryParse(currentStr, out var value))
            {
                // If the original contained "ma", it's in milliamps, convert to amps
                if (currentToken.ToString().ToLowerInvariant().Contains("ma"))
                {
                    return value / 1000.0;
                }
                return value;
            }
            
            return null;
        }
        
        private void BuildIDNETLookupTables()
        {
            _idnetDeviceLookup = new Dictionary<string, IDNETDeviceSpec>();
            
            if (_idnetCatalog?.IDNETDevices == null) return;
            
            foreach (var device in _idnetCatalog.IDNETDevices)
            {
                // Build lookup keys for IDNET devices
                var keys = new List<string>
                {
                    device.Description?.ToLowerInvariant(),
                    $"{device.FamilyName} {device.TypeName}".ToLowerInvariant(),
                    $"{device.FamilyName} {device.DeviceType}".ToLowerInvariant(),
                    device.SKU?.ToLowerInvariant(),
                    device.PartCode?.ToLowerInvariant()
                };
                
                foreach (var key in keys.Where(k => !string.IsNullOrEmpty(k)))
                {
                    if (!_idnetDeviceLookup.ContainsKey(key))
                    {
                        _idnetDeviceLookup[key] = device;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Built IDNET lookup table with {_idnetDeviceLookup.Count} entries from {_idnetCatalog.IDNETDevices.Count} IDNET devices");
        }
        
        private IDNETDeviceSpec FindIDNETByDescription(string description, string candela)
        {
            var key = description.ToLowerInvariant();
            
            // Try exact match first
            if (_idnetDeviceLookup.TryGetValue(key, out var device))
            {
                return device;
            }
            
            // Try with device type or sensitivity
            if (!string.IsNullOrEmpty(candela))
            {
                var keyWithExtra = $"{key} {candela}";
                if (_idnetDeviceLookup.TryGetValue(keyWithExtra, out device))
                {
                    return device;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// ENHANCED: Hierarchical keyword matching for IDNET devices
        /// </summary>
        private IDNETDeviceSpec FindIDNETByPatterns(string familyName, string typeName, string sensitivity)
        {
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            System.Diagnostics.Debug.WriteLine($"\n=== IDNET HIERARCHICAL KEYWORD MATCHING ===");
            System.Diagnostics.Debug.WriteLine($"Input: '{combined}' with sensitivity '{sensitivity}'");
            
            // STEP 1: PRIMARY DEVICE CLASSIFICATION
            var deviceIdentity = DetermineIDNETPrimaryDeviceIdentity(combined);
            System.Diagnostics.Debug.WriteLine($"Primary Identity: {deviceIdentity}");
            
            if (deviceIdentity == "UNKNOWN")
            {
                System.Diagnostics.Debug.WriteLine($"✗ Cannot classify IDNET device type from keywords");
                return null;
            }
            
            // STEP 2: SECONDARY CHARACTERISTICS
            var characteristics = ExtractIDNETSecondaryCharacteristics(combined);
            System.Diagnostics.Debug.WriteLine($"Characteristics: Mounting={characteristics.Mounting}, Environmental={characteristics.Environmental}");
            
            // STEP 3: VALIDATE CLASSIFICATION
            if (!ValidateIDNETClassification(deviceIdentity, characteristics, combined))
            {
                System.Diagnostics.Debug.WriteLine($"✗ IDNET classification failed validation");
                return null;
            }
            
            // STEP 4: FIND PRECISE SPECIFICATION
            var spec = FindIDNETSpecificationByClassification(deviceIdentity, characteristics, sensitivity);
            if (spec != null)
            {
                System.Diagnostics.Debug.WriteLine($"✓ IDNET MATCHED: {spec.Description} → {spec.StandbyCurrent}A");
            }
            
            return spec;
        }

        /// <summary>
        /// Determine primary IDNET device identity using keyword hierarchy
        /// </summary>
        private string DetermineIDNETPrimaryDeviceIdentity(string combined)
        {
            // Keyword sets for IDNET device types (initiating devices)
            var deviceKeywords = new Dictionary<string, string[]>
            {
                ["SMOKE"] = new[] { "SMOKE", "PHOTOELECTRIC", "PHOTO", "IONIZATION", "ION" },
                ["HEAT"] = new[] { "HEAT", "THERMAL", "TEMPERATURE", "FIXED", "RATE", "ROR" },
                ["BEAM"] = new[] { "BEAM", "REFLECTIVE", "PROJECTED" },
                ["MULTISENSOR"] = new[] { "MULTI", "COMBO", "COMBINATION", "DUAL" },
                ["PULL"] = new[] { "PULL", "MANUAL", "STATION", "BREAK", "GLASS", "EMERGENCY" },
                ["DUCT"] = new[] { "DUCT", "HVAC", "AIR" }
            };
            
            // Detection results
            bool hasSmoke = ContainsIDNETKeywords(combined, deviceKeywords["SMOKE"]);
            bool hasHeat = ContainsIDNETKeywords(combined, deviceKeywords["HEAT"]);
            bool hasBeam = ContainsIDNETKeywords(combined, deviceKeywords["BEAM"]);
            bool hasMulti = ContainsIDNETKeywords(combined, deviceKeywords["MULTISENSOR"]);
            bool hasPull = ContainsIDNETKeywords(combined, deviceKeywords["PULL"]);
            bool hasDuct = ContainsIDNETKeywords(combined, deviceKeywords["DUCT"]);
            
            System.Diagnostics.Debug.WriteLine($"IDNET Keyword Detection: Smoke={hasSmoke}, Heat={hasHeat}, Beam={hasBeam}, Multi={hasMulti}, Pull={hasPull}, Duct={hasDuct}");
            
            // HIERARCHICAL CLASSIFICATION for IDNET devices
            
            // 1. Specialized detectors (highest priority)
            if (hasBeam)
            {
                return "BEAM_DETECTOR";
            }
            
            if (hasDuct && hasSmoke)
            {
                return "DUCT_SMOKE_DETECTOR";
            }
            
            // 2. Multi-sensor detectors
            if (hasMulti && hasSmoke && hasHeat)
            {
                return "MULTI_SMOKE_HEAT_DETECTOR";
            }
            
            if (hasMulti && hasSmoke)
            {
                return "MULTI_SMOKE_DETECTOR";
            }
            
            // 3. Single-sensor detectors
            if (hasSmoke && !hasHeat && !hasMulti)
            {
                if (combined.Contains("PHOTOELECTRIC") || combined.Contains("PHOTO"))
                {
                    return "PHOTOELECTRIC_SMOKE_DETECTOR";
                }
                if (combined.Contains("IONIZATION") || combined.Contains("ION"))
                {
                    return "IONIZATION_SMOKE_DETECTOR";
                }
                return "SMOKE_DETECTOR"; // Generic smoke
            }
            
            if (hasHeat && !hasSmoke && !hasMulti)
            {
                if (combined.Contains("FIXED") || combined.Contains("TEMPERATURE"))
                {
                    return "FIXED_HEAT_DETECTOR";
                }
                if (combined.Contains("RATE") || combined.Contains("ROR"))
                {
                    return "RATE_HEAT_DETECTOR";
                }
                return "HEAT_DETECTOR"; // Generic heat
            }
            
            // 4. Manual devices
            if (hasPull)
            {
                if (combined.Contains("BREAK") || combined.Contains("GLASS"))
                {
                    return "BREAKGLASS_PULL_STATION";
                }
                return "MANUAL_PULL_STATION";
            }
            
            // 5. Fallback for generic detector keywords
            if (combined.Contains("DETECTOR") || combined.Contains("SENSOR"))
            {
                return "GENERIC_DETECTOR";
            }
            
            return "UNKNOWN";
        }

        /// <summary>
        /// Check if text contains any IDNET keywords
        /// </summary>
        private bool ContainsIDNETKeywords(string text, string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword));
        }

        /// <summary>
        /// Extract secondary characteristics for IDNET devices
        /// </summary>
        private DeviceCharacteristics ExtractIDNETSecondaryCharacteristics(string combined)
        {
            var characteristics = new DeviceCharacteristics();
            
            // Mounting Type Analysis (IDNET devices are typically ceiling mounted)
            var ceilingKeywords = new[] { "CEILING", "CLNG", "OVERHEAD", "RECESSED" };
            var wallKeywords = new[] { "WALL", "VERTICAL", "SURFACE" };
            var ductKeywords = new[] { "DUCT", "HVAC", "AIR" };
            
            if (ContainsIDNETKeywords(combined, ductKeywords))
            {
                characteristics.Mounting = "duct";
            }
            else if (ContainsIDNETKeywords(combined, ceilingKeywords))
            {
                characteristics.Mounting = "ceiling";
            }
            else if (ContainsIDNETKeywords(combined, wallKeywords))
            {
                characteristics.Mounting = "wall";
            }
            else
            {
                characteristics.Mounting = "ceiling"; // Default for detectors
            }
            
            // Environmental Type Analysis
            var weatherproofKeywords = new[] { "WEATHERPROOF", "WP", "OUTDOOR", "MARINE" };
            var highTempKeywords = new[] { "HIGH TEMP", "HT", "HIGH TEMPERATURE" };
            
            if (ContainsIDNETKeywords(combined, weatherproofKeywords))
            {
                characteristics.Environmental = "weatherproof";
            }
            else if (ContainsIDNETKeywords(combined, highTempKeywords))
            {
                characteristics.Environmental = "hightemp";
            }
            else
            {
                characteristics.Environmental = "standard";
            }
            
            return characteristics;
        }

        /// <summary>
        /// Validate IDNET classification for logical consistency
        /// </summary>
        private bool ValidateIDNETClassification(string deviceIdentity, DeviceCharacteristics characteristics, string combined)
        {
            // Validation Rules for IDNET devices
            
            // Rule 1: Smoke and heat detectors cannot be the same device (unless multi-sensor)
            if (deviceIdentity.Contains("SMOKE") && combined.Contains("HEAT") && !deviceIdentity.Contains("MULTI"))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ IDNET VALIDATION WARNING: Smoke and heat keywords in non-multi device");
                // Allow but warn - might be valid description
            }
            
            // Rule 2: Pull stations cannot be detectors
            if (deviceIdentity.Contains("PULL") && (combined.Contains("DETECTOR") || combined.Contains("SENSOR")))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ IDNET VALIDATION FAILED: Pull station cannot be detector");
                return false;
            }
            
            // Rule 3: Beam detectors must mention beam
            if (deviceIdentity == "BEAM_DETECTOR" && !combined.Contains("BEAM"))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ IDNET VALIDATION FAILED: Beam detector must mention beam");
                return false;
            }
            
            return true; // Validation passed
        }

        /// <summary>
        /// Find IDNET specification based on validated classification
        /// </summary>
        private IDNETDeviceSpec FindIDNETSpecificationByClassification(string deviceIdentity, DeviceCharacteristics characteristics, string sensitivity)
        {
            // For IDNET devices, use unit loads to calculate current (0.8mA per unit load)
            var specs = GetIDNETDeviceSpecifications();
            var specKey = $"{MapIDNETDeviceIdentityToKey(deviceIdentity)}_{characteristics.Mounting}_{characteristics.Environmental}";
            
            System.Diagnostics.Debug.WriteLine($"IDNET Specification lookup key: {specKey}");
            
            if (specs.TryGetValue(specKey, out var spec))
            {
                return CreateIDNETDeviceSpec(spec.description, spec.unitLoads, spec.pointType);
            }
            
            // Try fallbacks for IDNET
            return TryIDNETSpecificationFallbacks(deviceIdentity, characteristics, specs);
        }

        /// <summary>
        /// Map IDNET device identity to specification key
        /// </summary>
        private string MapIDNETDeviceIdentityToKey(string deviceIdentity)
        {
            return deviceIdentity switch
            {
                "PHOTOELECTRIC_SMOKE_DETECTOR" => "smoke_photo",
                "IONIZATION_SMOKE_DETECTOR" => "smoke_ion",
                "SMOKE_DETECTOR" => "smoke_photo", // Default to photoelectric
                "FIXED_HEAT_DETECTOR" => "heat_fixed",
                "RATE_HEAT_DETECTOR" => "heat_rate",
                "HEAT_DETECTOR" => "heat_fixed", // Default to fixed
                "MULTI_SMOKE_HEAT_DETECTOR" => "multi_combo",
                "BEAM_DETECTOR" => "beam",
                "DUCT_SMOKE_DETECTOR" => "duct_smoke",
                "MANUAL_PULL_STATION" => "pull_manual",
                "BREAKGLASS_PULL_STATION" => "pull_break",
                "GENERIC_DETECTOR" => "smoke_photo", // Default fallback
                _ => "smoke_photo"
            };
        }

        /// <summary>
        /// Get IDNET device specifications (unit loads based)
        /// </summary>
        private Dictionary<string, (int unitLoads, string pointType, string description)> GetIDNETDeviceSpecifications()
        {
            return new Dictionary<string, (int unitLoads, string pointType, string description)>
            {
                // Smoke Detectors
                ["smoke_photo_ceiling_standard"] = (1, "PHOTO", "Photoelectric Smoke Detector"),
                ["smoke_ion_ceiling_standard"] = (1, "ION", "Ionization Smoke Detector"),
                
                // Heat Detectors
                ["heat_fixed_ceiling_standard"] = (1, "HEAT", "Fixed Temperature Heat Detector"),
                ["heat_rate_ceiling_standard"] = (1, "ROR", "Rate of Rise Heat Detector"),
                
                // Multi-Sensor
                ["multi_combo_ceiling_standard"] = (1, "COMBO", "Multi-Sensor Smoke/Heat Detector"),
                
                // Beam Detectors
                ["beam_ceiling_standard"] = (1, "BEAM", "Beam Smoke Detector"),
                
                // Duct Detectors
                ["duct_smoke_duct_standard"] = (2, "DUCT", "Duct Smoke Detector"), // 2 UL for duct detectors
                
                // Pull Stations
                ["pull_manual_wall_standard"] = (1, "PULL", "Manual Pull Station"),
                ["pull_break_wall_standard"] = (1, "PULL", "Break Glass Pull Station"),
                
                // Weatherproof variants
                ["smoke_photo_ceiling_weatherproof"] = (1, "PHOTO", "WP Photoelectric Smoke Detector"),
                ["pull_manual_wall_weatherproof"] = (1, "PULL", "WP Manual Pull Station"),
            };
        }

        /// <summary>
        /// Try IDNET specification fallbacks
        /// </summary>
        private IDNETDeviceSpec TryIDNETSpecificationFallbacks(string deviceIdentity, DeviceCharacteristics characteristics, Dictionary<string, (int unitLoads, string pointType, string description)> specs)
        {
            var deviceKey = MapIDNETDeviceIdentityToKey(deviceIdentity);
            
            // Fallback 1: Try standard environmental if weatherproof not found
            if (characteristics.Environmental == "weatherproof")
            {
                var standardKey = $"{deviceKey}_{characteristics.Mounting}_standard";
                if (specs.TryGetValue(standardKey, out var standardSpec))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ IDNET FALLBACK: Using standard environmental specification");
                    return CreateIDNETDeviceSpec($"WP {standardSpec.description}", standardSpec.unitLoads, standardSpec.pointType);
                }
            }
            
            // Fallback 2: Try ceiling mounting if wall not found
            if (characteristics.Mounting == "wall")
            {
                var ceilingKey = $"{deviceKey}_ceiling_{characteristics.Environmental}";
                if (specs.TryGetValue(ceilingKey, out var ceilingSpec))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ IDNET FALLBACK: Using ceiling mounting specification");
                    return CreateIDNETDeviceSpec($"Wall {ceilingSpec.description}", ceilingSpec.unitLoads, ceilingSpec.pointType);
                }
            }
            
            // Fallback 3: Generic photoelectric smoke detector
            System.Diagnostics.Debug.WriteLine($"✓ IDNET FALLBACK: Using generic smoke detector");
            return CreateIDNETDeviceSpec("Generic Smoke Detector", 1, "PHOTO");
        }

        /// <summary>
        /// Create IDNETDeviceSpec from specification
        /// </summary>
        private IDNETDeviceSpec CreateIDNETDeviceSpec(string description, int unitLoads, string pointType)
        {
            var standbyCurrent = unitLoads * 0.0008; // 0.8mA per unit load
            
            return new IDNETDeviceSpec
            {
                FamilyName = "detectors",
                TypeName = description,
                Description = description,
                StandbyCurrent = standbyCurrent,
                AlarmCurrent = standbyCurrent,
                UnitLoads = unitLoads,
                PointType = pointType,
                Addresses = 1,
                DeviceType = pointType,
                OperatingVoltage = "24VDC",
                EnvironmentalRating = "indoor"
            };
        }
        
        private IDNETDeviceSpec FindBestIDNETMatch(string familyType, string deviceType, string sensitivity)
        {
            var candidates = _idnetCatalog.IDNETDevices?.Where(d => 
                (d.FamilyName?.ToLowerInvariant().Contains(familyType) == true ||
                 d.DeviceType?.ToLowerInvariant().Contains(familyType) == true) &&
                (d.TypeName?.ToLowerInvariant().Contains(deviceType) == true ||
                 d.DeviceType?.ToLowerInvariant().Contains(deviceType) == true)).ToList();
            
            if (candidates?.Any() != true) return null;
            
            // If sensitivity specified, try to find exact match
            if (!string.IsNullOrEmpty(sensitivity))
            {
                var exactMatch = candidates.FirstOrDefault(c => 
                    c.Sensitivity?.ToLowerInvariant().Contains(sensitivity.ToLowerInvariant()) == true);
                if (exactMatch != null) return exactMatch;
            }
            
            // Return first match
            return candidates.First();
        }
        
        private string GetBestIDNETFamilyMatch(string familyName, string typeName)
        {
            if (_idnetFamilyMappings == null) return null;
            
            var combined = $"{familyName} {typeName}".ToLowerInvariant();
            
            // Try exact match first
            if (_idnetFamilyMappings.TryGetValue(combined, out var family))
            {
                return family;
            }
            
            // Try partial matches
            foreach (var mapping in _idnetFamilyMappings)
            {
                if (combined.Contains(mapping.Key) || mapping.Key.Contains(combined))
                {
                    return mapping.Value;
                }
            }
            
            return null;
        }
        
        private IDNETDeviceSpec GetDefaultIDNETSpecForFamily(string familyKey, string sensitivity)
        {
            var devices = _idnetCatalog.IDNETDevices?.Where(d => 
                d.FamilyName?.ToLowerInvariant() == familyKey.ToLowerInvariant()).ToList();
            
            if (devices?.Any() != true) return null;
            
            // Try to find with specified sensitivity
            if (!string.IsNullOrEmpty(sensitivity))
            {
                var withSensitivity = devices.FirstOrDefault(d => 
                    d.Sensitivity?.ToLowerInvariant().Contains(sensitivity.ToLowerInvariant()) == true);
                if (withSensitivity != null) return withSensitivity;
            }
            
            // Return first available
            return devices.First();
        }
        
        private IDNETDeviceCatalog CreateFallbackIDNETCatalog()
        {
            return new IDNETDeviceCatalog
            {
                Version = "fallback",
                LastUpdated = DateTime.Now,
                Description = "Minimal fallback IDNET catalog",
                IDNETDevices = new List<IDNETDeviceSpec>
                {
                    new IDNETDeviceSpec
                    {
                        FamilyName = "detectors",
                        TypeName = "smoke",
                        DeviceType = "Photoelectric Smoke Detector",
                        Description = "Generic Photoelectric Smoke Detector",
                        StandbyCurrent = 0.0008, // 1 unit load = 0.8mA
                        AlarmCurrent = 0.0008,
                        UnitLoads = 1,
                        PointType = "PHOTO",
                        Addresses = 1,
                        OperatingVoltage = "24VDC",
                        EnvironmentalRating = "indoor"
                    },
                    new IDNETDeviceSpec
                    {
                        FamilyName = "pullStations",
                        TypeName = "manual",
                        DeviceType = "Manual Pull Station",
                        Description = "Generic Manual Pull Station",
                        StandbyCurrent = 0.0008, // 1 unit load = 0.8mA
                        AlarmCurrent = 0.0008,
                        UnitLoads = 1,
                        PointType = "PULL",
                        Addresses = 1,
                        OperatingVoltage = "24VDC",
                        EnvironmentalRating = "indoor"
                    }
                }
            };
        }
    }
    
    /// <summary>
    /// IDNET device catalog data structure
    /// </summary>
    public class IDNETDeviceCatalog
    {
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Description { get; set; }
        public List<IDNETDeviceSpec> IDNETDevices { get; set; } = new List<IDNETDeviceSpec>();
    }
    
    /// <summary>
    /// Individual IDNET device specification
    /// </summary>
    public class IDNETDeviceSpec
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string DeviceType { get; set; }
        public string SKU { get; set; }
        public string PartCode { get; set; }
        public string Description { get; set; }
        public double StandbyCurrent { get; set; }
        public double AlarmCurrent { get; set; }
        public int UnitLoads { get; set; }
        public string Sensitivity { get; set; }
        public string OperatingVoltage { get; set; }
        public string EnvironmentalRating { get; set; }
        public string PointType { get; set; }
        public int Addresses { get; set; }
        public string Path { get; set; }
    }
    
    /// <summary>
    /// Result of IDNET device specification lookup
    /// </summary>
    public class IDNETDeviceSpecResult : IDeviceSpecResult
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public bool FoundMatch { get; set; }
        public double Current { get; set; }
        public int UnitLoads { get; set; }
        public string Source { get; set; }
        public IDNETDeviceSpec IDNETDeviceSpec { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// IDNET catalog statistics
    /// </summary>
    public class IDNETCatalogStats : ICatalogStats
    {
        public int TotalIDNETDevices { get; set; }
        public bool IDNETCatalogLoaded { get; set; }
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }
        
        // Interface implementation
        public int TotalDevices 
        { 
            get => TotalIDNETDevices; 
            set => TotalIDNETDevices = value; 
        }
        public bool CatalogLoaded 
        { 
            get => IDNETCatalogLoaded; 
            set => IDNETCatalogLoaded = value; 
        }
    }
}