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
        
        private IDNETDeviceSpec FindIDNETByPatterns(string familyName, string typeName, string candela)
        {
            var combined = $"{familyName} {typeName}".ToLowerInvariant();
            
            // Pattern matching for common IDNET device types
            var patterns = new Dictionary<string, Func<IDNETDeviceSpec>>
            {
                { "smoke detector", () => FindBestIDNETMatch("smoke", "detector", candela) },
                { "smoke", () => FindBestIDNETMatch("smoke", "detector", candela) },
                { "heat detector", () => FindBestIDNETMatch("heat", "detector", candela) },
                { "heat", () => FindBestIDNETMatch("heat", "detector", candela) },
                { "pull station", () => FindBestIDNETMatch("manual", "pull", candela) },
                { "manual", () => FindBestIDNETMatch("manual", "pull", candela) },
                { "beam detector", () => FindBestIDNETMatch("beam", "detector", candela) },
                { "beam", () => FindBestIDNETMatch("beam", "detector", candela) }
            };
            
            foreach (var pattern in patterns)
            {
                if (combined.Contains(pattern.Key))
                {
                    var result = pattern.Value();
                    if (result != null) return result;
                }
            }
            
            return null;
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