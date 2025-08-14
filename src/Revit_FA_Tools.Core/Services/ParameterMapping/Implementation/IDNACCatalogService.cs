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
    /// Service for loading and querying the AutoCall 4100ES IDNAC device catalog
    /// Maps IDNAC notification device family names and types to specific device specifications with current draws
    /// </summary>
    public class IDNACCatalogService : IFireAlarmCatalogService
    {
        private static IDNACDeviceCatalog _idnacCatalog;
        private static Dictionary<string, IDNACDeviceSpec> _idnacDeviceLookup;
        private static Dictionary<string, string> _idnacFamilyMappings;
        private static readonly object _lockObject = new object();
        
        public IDNACCatalogService()
        {
            EnsureIDNACCatalogLoaded();
        }
        
        #region IFireAlarmCatalogService Implementation
        
        /// <summary>
        /// Interface implementation for generic device spec finding
        /// </summary>
        public IDeviceSpecResult FindDeviceSpec(string familyName, string typeName, string candela = null, double wattage = 0)
        {
            return FindIDNACDeviceSpec(familyName, typeName, candela, wattage);
        }
        
        /// <summary>
        /// Interface implementation for catalog stats
        /// </summary>
        public ICatalogStats GetCatalogStats()
        {
            return GetIDNACCatalogStats();
        }
        
        /// <summary>
        /// Interface implementation for catalog testing
        /// </summary>
        public string TestCatalogLoading()
        {
            return TestIDNACCatalogLoading();
        }
        
        #endregion
        
        /// <summary>
        /// Find IDNAC device specification by family name and type name
        /// </summary>
        public IDNACDeviceSpecResult FindIDNACDeviceSpec(string familyName, string typeName, string candela = null, double wattage = 0)
        {
            try
            {
                EnsureIDNACCatalogLoaded();
                
                var result = new IDNACDeviceSpecResult
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
                var spec = FindIDNACByDescription(combinedName, candela);
                if (spec != null)
                {
                    result.FoundMatch = true;
                    result.Current = spec.CombinedCurrent;
                    result.UnitLoads = spec.UnitLoads;
                    result.Source = "IDNAC Direct Match";
                    result.IDNACDeviceSpec = spec;
                    return result;
                }
                
                // Strategy 2: Pattern-based matching
                spec = FindIDNACByPatterns(familyName, typeName, candela);
                if (spec != null)
                {
                    result.FoundMatch = true;
                    result.Current = spec.CombinedCurrent;
                    result.UnitLoads = spec.UnitLoads;
                    result.Source = "IDNAC Pattern Match";
                    result.IDNACDeviceSpec = spec;
                    return result;
                }
                
                // Strategy 3: Family mapping lookup
                if (_idnacFamilyMappings != null)
                {
                    var familyKey = GetBestIDNACFamilyMatch(familyName, typeName);
                    if (!string.IsNullOrEmpty(familyKey))
                    {
                        spec = GetDefaultIDNACSpecForFamily(familyKey, candela);
                        if (spec != null)
                        {
                            result.FoundMatch = true;
                            result.Current = spec.CombinedCurrent;
                            result.UnitLoads = spec.UnitLoads;
                            result.Source = "IDNAC Family Mapping";
                            result.IDNACDeviceSpec = spec;
                            return result;
                        }
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding IDNAC device spec: {ex.Message}");
                return new IDNACDeviceSpecResult
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
        /// Get IDNAC catalog statistics
        /// </summary>
        public IDNACCatalogStats GetIDNACCatalogStats()
        {
            EnsureIDNACCatalogLoaded();
            
            return new IDNACCatalogStats
            {
                TotalIDNACDevices = _idnacDeviceLookup?.Count ?? 0,
                IDNACCatalogLoaded = _idnacCatalog != null,
                Version = _idnacCatalog?.Version ?? "Unknown",
                LastUpdated = _idnacCatalog?.LastUpdated ?? DateTime.MinValue
            };
        }
        
        /// <summary>
        /// Test the IDNAC catalog loading and provide diagnostic information
        /// </summary>
        public string TestIDNACCatalogLoading()
        {
            try
            {
                EnsureIDNACCatalogLoaded();
                
                var stats = GetIDNACCatalogStats();
                var result = new List<string>
                {
                    "=== IDNAC Device Catalog Test Results ===",
                    $"IDNAC Catalog Loaded: {stats.IDNACCatalogLoaded}",
                    $"Version: {stats.Version}",
                    $"Last Updated: {stats.LastUpdated:yyyy-MM-dd}",
                    $"Total IDNAC Device Specs: {stats.TotalIDNACDevices}",
                    $"IDNAC Family Mappings: {_idnacFamilyMappings?.Count ?? 0}"
                };
                
                // Test a few sample IDNAC lookups
                if (stats.IDNACCatalogLoaded)
                {
                    result.Add("--- Sample IDNAC Lookups ---");
                    
                    var testCases = new[]
                    {
                        new { Family = "Wall Horn Strobe", Type = "75cd", Expected = "0.221A" },
                        new { Family = "Horn Strobe", Type = "Wall", Expected = "> 0A" },
                        new { Family = "Strobe", Type = "Wall", Expected = "> 0A" }
                    };
                    
                    foreach (var test in testCases)
                    {
                        var testResult = FindIDNACDeviceSpec(test.Family, test.Type);
                        result.Add($"{test.Family} {test.Type}: Match={testResult.FoundMatch}, Current={testResult.Current:F3}A, Source={testResult.Source}");
                    }
                }
                
                result.Add("==========================================");
                return string.Join("\n", result);
            }
            catch (Exception ex)
            {
                return $"IDNAC Device Catalog Test FAILED: {ex.Message}";
            }
        }
        
        private void EnsureIDNACCatalogLoaded()
        {
            if (_idnacCatalog != null) return;
            
            lock (_lockObject)
            {
                if (_idnacCatalog != null) return;
                
                LoadIDNACDeviceCatalog();
                BuildIDNACLookupTables();
            }
        }
        
        private void LoadIDNACDeviceCatalog()
        {
            try
            {
                string catalogJson = null;
                
                // Try loading from file system first
                var catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DeviceCatalogs", "IDNAC_Catalog.json");
                if (File.Exists(catalogPath))
                {
                    catalogJson = File.ReadAllText(catalogPath);
                    System.Diagnostics.Debug.WriteLine($"Loaded IDNAC device catalog from: {catalogPath}");
                }
                
                // Fallback: try embedded resource
                if (string.IsNullOrEmpty(catalogJson))
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(r => r.EndsWith("IDNAC_Catalog.json"));
                    
                    if (resourceName != null)
                    {
                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        using (var reader = new StreamReader(stream))
                        {
                            catalogJson = reader.ReadToEnd();
                            System.Diagnostics.Debug.WriteLine($"Loaded IDNAC device catalog from embedded resource: {resourceName}");
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(catalogJson))
                {
                    var jObject = JObject.Parse(catalogJson);
                    _idnacCatalog = ParseIDNACDeviceCatalog(jObject);
                    System.Diagnostics.Debug.WriteLine($"IDNAC device catalog loaded successfully. Version: {_idnacCatalog.Version}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: IDNAC device catalog not found. Creating minimal fallback catalog.");
                    _idnacCatalog = CreateFallbackIDNACCatalog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to load IDNAC device catalog: {ex.Message}");
                _idnacCatalog = CreateFallbackIDNACCatalog();
            }
        }
        
        private IDNACDeviceCatalog ParseIDNACDeviceCatalog(JObject jObject)
        {
            var catalog = new IDNACDeviceCatalog
            {
                Version = jObject["version"]?.ToString() ?? "unknown",
                LastUpdated = DateTime.TryParse(jObject["lastUpdated"]?.ToString(), out var date) ? date : DateTime.MinValue,
                Description = jObject["description"]?.ToString() ?? "",
                IDNACDevices = new List<IDNACDeviceSpec>()
            };
            
            // Parse device families for IDNAC devices
            var deviceFamilies = jObject["deviceFamilies"] as JObject;
            if (deviceFamilies != null)
            {
                foreach (var familyKvp in deviceFamilies)
                {
                    var familyName = familyKvp.Key;
                    var familyObj = familyKvp.Value as JObject;
                    ParseIDNACDeviceFamily(catalog, familyName, familyObj);
                }
            }
            
            // Parse family mappings for IDNAC devices
            var fqqMapping = jObject["fqqMapping"]?["mappings"] as JObject;
            if (fqqMapping != null)
            {
                _idnacFamilyMappings = new Dictionary<string, string>();
                foreach (var mapping in fqqMapping)
                {
                    var deviceDescription = mapping.Key;
                    var familyInfo = mapping.Value["family"]?.ToString();
                    if (!string.IsNullOrEmpty(familyInfo))
                    {
                        _idnacFamilyMappings[deviceDescription.ToLowerInvariant()] = familyInfo;
                    }
                }
            }
            
            return catalog;
        }
        
        private void ParseIDNACDeviceFamily(IDNACDeviceCatalog catalog, string familyName, JObject familyObj)
        {
            try
            {
                var deviceTypes = familyObj["deviceTypes"] as JObject;
                if (deviceTypes == null) return;
                
                foreach (var typeKvp in deviceTypes)
                {
                    var typeName = typeKvp.Key; // wall, ceiling, etc.
                    var typeObj = typeKvp.Value as JObject;
                    ParseIDNACDeviceType(catalog, familyName, typeName, typeObj);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNAC device family {familyName}: {ex.Message}");
            }
        }
        
        private void ParseIDNACDeviceType(IDNACDeviceCatalog catalog, string familyName, string typeName, JObject typeObj)
        {
            try
            {
                // Navigate through the nested structure (indoor/outdoor -> standard/high, etc.)
                ParseNestedIDNACDeviceSpecs(catalog, familyName, typeName, typeObj, "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNAC device type {familyName}/{typeName}: {ex.Message}");
            }
        }
        
        private void ParseNestedIDNACDeviceSpecs(IDNACDeviceCatalog catalog, string familyName, string typeName, JObject obj, string path)
        {
            foreach (var kvp in obj)
            {
                var key = kvp.Key;
                var value = kvp.Value;
                
                if (key == "candelaRatings" && value is JObject candelaObj)
                {
                    // Found candela ratings - parse individual IDNAC devices
                    foreach (var candelaKvp in candelaObj)
                    {
                        var candela = candelaKvp.Key;
                        var deviceObj = candelaKvp.Value as JObject;
                        
                        if (deviceObj != null)
                        {
                            var device = ParseIDNACDeviceSpec(familyName, typeName, candela, deviceObj, path);
                            if (device != null)
                            {
                                catalog.IDNACDevices.Add(device);
                            }
                        }
                    }
                }
                else if (value is JObject nestedObj)
                {
                    // Continue drilling down
                    var newPath = string.IsNullOrEmpty(path) ? key : $"{path}/{key}";
                    ParseNestedIDNACDeviceSpecs(catalog, familyName, typeName, nestedObj, newPath);
                }
            }
        }
        
        private IDNACDeviceSpec ParseIDNACDeviceSpec(string familyName, string typeName, string candela, JObject deviceObj, string path)
        {
            try
            {
                return new IDNACDeviceSpec
                {
                    FamilyName = familyName,
                    TypeName = typeName,
                    Candela = candela,
                    SKU = deviceObj["sku"]?.ToString() ?? "",
                    PartCode = deviceObj["partCode"]?.ToString() ?? "",
                    Description = deviceObj["description"]?.ToString() ?? "",
                    Setting = deviceObj["setting"]?.ToString() ?? "",
                    HornCurrent = (double)(deviceObj["hornCurrent"] ?? 0),
                    StrobeCurrent = (double)(deviceObj["strobeCurrent"] ?? 0),
                    CombinedCurrent = (double)(deviceObj["combinedCurrent"] ?? 0),
                    UnitLoads = (int)(deviceObj["unitLoads"] ?? 1),
                    TTapCompatible = (bool)(deviceObj["ttapCompatible"] ?? false),
                    Mounting = deviceObj["mounting"]?.ToString() ?? "",
                    EnvironmentalRating = deviceObj["environmentalRating"]?.ToString() ?? "",
                    Path = path
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing IDNAC device spec: {ex.Message}");
                return null;
            }
        }
        
        private void BuildIDNACLookupTables()
        {
            _idnacDeviceLookup = new Dictionary<string, IDNACDeviceSpec>();
            
            if (_idnacCatalog?.IDNACDevices == null) return;
            
            foreach (var device in _idnacCatalog.IDNACDevices)
            {
                // Build lookup keys for IDNAC devices
                var keys = new List<string>
                {
                    device.Description?.ToLowerInvariant(),
                    $"{device.FamilyName} {device.TypeName}".ToLowerInvariant(),
                    $"{device.FamilyName} {device.TypeName} {device.Candela}".ToLowerInvariant(),
                    device.SKU?.ToLowerInvariant(),
                    device.PartCode?.ToLowerInvariant()
                };
                
                foreach (var key in keys.Where(k => !string.IsNullOrEmpty(k)))
                {
                    if (!_idnacDeviceLookup.ContainsKey(key))
                    {
                        _idnacDeviceLookup[key] = device;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Built IDNAC lookup table with {_idnacDeviceLookup.Count} entries from {_idnacCatalog.IDNACDevices.Count} IDNAC devices");
        }
        
        private IDNACDeviceSpec FindIDNACByDescription(string description, string candela)
        {
            var key = description.ToLowerInvariant();
            
            // Try exact match first
            if (_idnacDeviceLookup.TryGetValue(key, out var device))
            {
                return device;
            }
            
            // Try with candela
            if (!string.IsNullOrEmpty(candela))
            {
                var keyWithCandela = $"{key} {candela}";
                if (_idnacDeviceLookup.TryGetValue(keyWithCandela, out device))
                {
                    return device;
                }
            }
            
            return null;
        }
        
        private IDNACDeviceSpec FindIDNACByPatterns(string familyName, string typeName, string candela)
        {
            var combined = $"{familyName} {typeName}".ToLowerInvariant();
            
            // Pattern matching for common IDNAC device types
            var patterns = new Dictionary<string, Func<IDNACDeviceSpec>>
            {
                { "horn strobe", () => FindBestIDNACMatch("hornstrobe", "wall", candela) },
                { "horn", () => FindBestIDNACMatch("horn", "wall", candela) },
                { "strobe", () => FindBestIDNACMatch("strobe", "wall", candela) },
                { "speaker", () => FindBestIDNACMatch("speaker", "wall", candela) },
                { "speaker strobe", () => FindBestIDNACMatch("speakerstrobe", "wall", candela) }
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
        
        private IDNACDeviceSpec FindBestIDNACMatch(string familyType, string mounting, string candela)
        {
            var candidates = _idnacCatalog.IDNACDevices?.Where(d => 
                d.FamilyName?.ToLowerInvariant().Contains(familyType) == true &&
                d.Mounting?.ToLowerInvariant().Contains(mounting) == true).ToList();
            
            if (candidates?.Any() != true) return null;
            
            // If candela specified, try to find exact match
            if (!string.IsNullOrEmpty(candela))
            {
                var exactMatch = candidates.FirstOrDefault(c => c.Candela == candela);
                if (exactMatch != null) return exactMatch;
            }
            
            // Return first match or default candela rating
            return candidates.FirstOrDefault(c => c.Candela == "75") ?? candidates.First();
        }
        
        private string GetBestIDNACFamilyMatch(string familyName, string typeName)
        {
            if (_idnacFamilyMappings == null) return null;
            
            var combined = $"{familyName} {typeName}".ToLowerInvariant();
            
            // Try exact match first
            if (_idnacFamilyMappings.TryGetValue(combined, out var family))
            {
                return family;
            }
            
            // Try partial matches
            foreach (var mapping in _idnacFamilyMappings)
            {
                if (combined.Contains(mapping.Key) || mapping.Key.Contains(combined))
                {
                    return mapping.Value;
                }
            }
            
            return null;
        }
        
        private IDNACDeviceSpec GetDefaultIDNACSpecForFamily(string familyKey, string candela)
        {
            var devices = _idnacCatalog.IDNACDevices?.Where(d => 
                d.FamilyName?.ToLowerInvariant() == familyKey.ToLowerInvariant()).ToList();
            
            if (devices?.Any() != true) return null;
            
            // Try to find with specified candela
            if (!string.IsNullOrEmpty(candela))
            {
                var withCandela = devices.FirstOrDefault(d => d.Candela == candela);
                if (withCandela != null) return withCandela;
            }
            
            // Return default (75cd or first available)
            return devices.FirstOrDefault(d => d.Candela == "75") ?? devices.First();
        }
        
        private IDNACDeviceCatalog CreateFallbackIDNACCatalog()
        {
            return new IDNACDeviceCatalog
            {
                Version = "fallback",
                LastUpdated = DateTime.Now,
                Description = "Minimal fallback IDNAC catalog",
                IDNACDevices = new List<IDNACDeviceSpec>
                {
                    new IDNACDeviceSpec
                    {
                        FamilyName = "hornstrobe",
                        TypeName = "wall",
                        Candela = "75",
                        Description = "Generic Wall Horn Strobe 75cd",
                        CombinedCurrent = 0.221,
                        UnitLoads = 1,
                        Mounting = "wall",
                        EnvironmentalRating = "indoor"
                    }
                }
            };
        }
    }
    
    /// <summary>
    /// IDNAC device catalog data structure
    /// </summary>
    public class IDNACDeviceCatalog
    {
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Description { get; set; }
        public List<IDNACDeviceSpec> IDNACDevices { get; set; } = new List<IDNACDeviceSpec>();
    }
    
    /// <summary>
    /// Individual IDNAC device specification
    /// </summary>
    public class IDNACDeviceSpec
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string Candela { get; set; }
        public string SKU { get; set; }
        public string PartCode { get; set; }
        public string Description { get; set; }
        public string Setting { get; set; }
        public double HornCurrent { get; set; }
        public double StrobeCurrent { get; set; }
        public double CombinedCurrent { get; set; }
        public int UnitLoads { get; set; }
        public bool TTapCompatible { get; set; }
        public string Mounting { get; set; }
        public string EnvironmentalRating { get; set; }
        public string Path { get; set; }
    }
    
    /// <summary>
    /// Result of IDNAC device specification lookup
    /// </summary>
    public class IDNACDeviceSpecResult : IDeviceSpecResult
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public bool FoundMatch { get; set; }
        public double Current { get; set; }
        public int UnitLoads { get; set; }
        public string Source { get; set; }
        public IDNACDeviceSpec IDNACDeviceSpec { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// IDNAC catalog statistics
    /// </summary>
    public class IDNACCatalogStats : ICatalogStats
    {
        public int TotalIDNACDevices { get; set; }
        public bool IDNACCatalogLoaded { get; set; }
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }
        
        // Interface implementation
        public int TotalDevices 
        { 
            get => TotalIDNACDevices; 
            set => TotalIDNACDevices = value; 
        }
        public bool CatalogLoaded 
        { 
            get => IDNACCatalogLoaded; 
            set => IDNACCatalogLoaded = value; 
        }
    }
}