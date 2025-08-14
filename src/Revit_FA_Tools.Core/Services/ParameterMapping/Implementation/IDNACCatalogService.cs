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
        
        /// <summary>
        /// ENHANCED: Hierarchical keyword matching with contradiction prevention
        /// </summary>
        private IDNACDeviceSpec FindIDNACByPatterns(string familyName, string typeName, string candela)
        {
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            System.Diagnostics.Debug.WriteLine($"\n=== HIERARCHICAL KEYWORD MATCHING ===");
            System.Diagnostics.Debug.WriteLine($"Input: '{combined}' with candela '{candela}'");
            
            // STEP 1: PRIMARY DEVICE CLASSIFICATION (establishes core device identity)
            var deviceIdentity = DeterminePrimaryDeviceIdentity(combined);
            System.Diagnostics.Debug.WriteLine($"Primary Identity: {deviceIdentity}");
            
            if (deviceIdentity == "UNKNOWN")
            {
                System.Diagnostics.Debug.WriteLine($"✗ Cannot classify device type from keywords");
                return null;
            }
            
            // STEP 2: SECONDARY CHARACTERISTICS (refines the primary classification)
            var characteristics = ExtractSecondaryCharacteristics(combined);
            System.Diagnostics.Debug.WriteLine($"Characteristics: Mounting={characteristics.Mounting}, Environmental={characteristics.Environmental}");
            
            // STEP 3: VALIDATE CLASSIFICATION (ensure logical consistency)
            if (!ValidateClassification(deviceIdentity, characteristics, combined))
            {
                System.Diagnostics.Debug.WriteLine($"✗ Classification failed validation");
                return null;
            }
            
            // STEP 4: FIND PRECISE SPECIFICATION
            var spec = FindSpecificationByClassification(deviceIdentity, characteristics, candela);
            if (spec != null)
            {
                System.Diagnostics.Debug.WriteLine($"✓ MATCHED: {spec.Description} → {spec.CombinedCurrent}A");
            }
            
            return spec;
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
        
        /// <summary>
        /// STEP 1: Determine primary device identity using keyword hierarchy
        /// </summary>
        private string DeterminePrimaryDeviceIdentity(string combined)
        {
            // Keyword sets for each device type (order matters - most specific first)
            var deviceKeywords = new Dictionary<string, string[]>
            {
                // Primary Keywords (establish core device function)
                ["HORN"] = new[] { "HORN", "AUDIBLE" },
                ["SPEAKER"] = new[] { "SPEAKER", "VOICE", "AUDIO", "MASS NOTIFICATION" },
                ["STROBE"] = new[] { "STROBE", "VISUAL", "FLASH", "LIGHT", "BEACON" },
                
                // Combination Keywords (checked after primary to detect combinations)
                ["MULTITONE"] = new[] { "MULTITONE", "MULTI TONE", "MT", "520HZ", "520 HZ" },
                ["CHIME"] = new[] { "CHIME", "TONE", "BELL" }
            };
            
            // Detection results
            bool hasHorn = ContainsKeywords(combined, deviceKeywords["HORN"]);
            bool hasSpeaker = ContainsKeywords(combined, deviceKeywords["SPEAKER"]);
            bool hasStrobe = ContainsKeywords(combined, deviceKeywords["STROBE"]);
            bool hasMultitone = ContainsKeywords(combined, deviceKeywords["MULTITONE"]);
            bool hasChime = ContainsKeywords(combined, deviceKeywords["CHIME"]);
            
            System.Diagnostics.Debug.WriteLine($"Keyword Detection: Horn={hasHorn}, Speaker={hasSpeaker}, Strobe={hasStrobe}, MT={hasMultitone}, Chime={hasChime}");
            
            // HIERARCHICAL CLASSIFICATION with contradiction prevention
            
            // 1. Combination devices (most specific)
            if (hasSpeaker && hasStrobe)
            {
                // RULE: Speaker + Strobe = Speaker Strobe (Horn keywords ignored in speaker context)
                return "SPEAKER_STROBE";
            }
            
            if (hasHorn && hasStrobe)
            {
                // RULE: Horn + Strobe = Horn Strobe (Speaker keywords ignored in horn context)
                if (hasMultitone)
                {
                    return "MULTITONE_HORN_STROBE";
                }
                return "HORN_STROBE";
            }
            
            // 2. Single function devices
            if (hasSpeaker && !hasStrobe && !hasHorn)
            {
                // RULE: Pure speaker device (no horn or strobe)
                return "SPEAKER";
            }
            
            if (hasHorn && !hasStrobe && !hasSpeaker)
            {
                // RULE: Pure horn device (no speaker or strobe)
                if (hasMultitone)
                {
                    return "MULTITONE_HORN";
                }
                return "HORN";
            }
            
            if (hasStrobe && !hasHorn && !hasSpeaker)
            {
                // RULE: Pure strobe device (no horn or speaker)
                return "STROBE";
            }
            
            if (hasChime)
            {
                // RULE: Chime device (special notification type)
                return "CHIME";
            }
            
            // 3. Fallback analysis for unclear cases
            return AnalyzeFallbackKeywords(combined);
        }

        /// <summary>
        /// Check if text contains any of the specified keywords
        /// </summary>
        private bool ContainsKeywords(string text, string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword));
        }

        /// <summary>
        /// Fallback analysis for devices that don't match primary keywords
        /// </summary>
        private string AnalyzeFallbackKeywords(string combined)
        {
            // Generic notification device keywords
            var notificationKeywords = new[] { "NOTIFICATION", "APPLIANCE", "NAC", "DEVICE", "UNIT" };
            
            if (ContainsKeywords(combined, notificationKeywords))
            {
                // Guess based on common patterns
                if (combined.Contains("CEILING") || combined.Contains("CLNG"))
                {
                    return "STROBE"; // Ceiling devices are often strobes
                }
                return "HORN_STROBE"; // Default assumption for notification devices
            }
            
            return "UNKNOWN";
        }

        /// <summary>
        /// STEP 2: Extract secondary characteristics
        /// </summary>
        private DeviceCharacteristics ExtractSecondaryCharacteristics(string combined)
        {
            var characteristics = new DeviceCharacteristics();
            
            // Mounting Type Analysis
            var ceilingKeywords = new[] { "CEILING", "CLNG", "OVERHEAD", "RECESSED", "PENDANT", "FLUSH" };
            var wallKeywords = new[] { "WALL", "VERTICAL", "SURFACE", "MOUNT" };
            
            if (ContainsKeywords(combined, ceilingKeywords))
            {
                characteristics.Mounting = "ceiling";
            }
            else if (ContainsKeywords(combined, wallKeywords))
            {
                characteristics.Mounting = "wall";
            }
            else
            {
                characteristics.Mounting = "wall"; // Default assumption
            }
            
            // Environmental Type Analysis (CRITICAL for current accuracy)
            var weatherproofKeywords = new[] { "WEATHERPROOF", "WP", "WPHC", "OUTDOOR", "NEMA", "IP65", "IP66", "IP67", "MARINE", "SEALED" };
            var highCancelKeywords = new[] { "HIGH CANCEL", "HC", "HIGH-CANCEL" };
            
            if (ContainsKeywords(combined, weatherproofKeywords))
            {
                characteristics.Environmental = "weatherproof";
            }
            else if (ContainsKeywords(combined, highCancelKeywords))
            {
                characteristics.Environmental = "highcancel";
            }
            else
            {
                characteristics.Environmental = "standard";
            }
            
            return characteristics;
        }

        /// <summary>
        /// STEP 3: Validate classification for logical consistency
        /// </summary>
        private bool ValidateClassification(string deviceIdentity, DeviceCharacteristics characteristics, string combined)
        {
            // Validation Rules to prevent contradictions
            
            // Rule 1: Horn devices cannot be speakers
            if (deviceIdentity.Contains("HORN") && combined.Contains("SPEAKER") && 
                !deviceIdentity.Contains("HORN_STROBE")) // Horn strobe with speaker mention is invalid
            {
                System.Diagnostics.Debug.WriteLine($"⚠ VALIDATION FAILED: Horn device cannot contain speaker keywords");
                return false;
            }
            
            // Rule 2: Speaker devices cannot be horns
            if (deviceIdentity.Contains("SPEAKER") && combined.Contains("HORN") && 
                !deviceIdentity.Contains("SPEAKER_STROBE")) // Speaker strobe with horn mention is invalid
            {
                System.Diagnostics.Debug.WriteLine($"⚠ VALIDATION FAILED: Speaker device cannot contain horn keywords");
                return false;
            }
            
            // Rule 3: Validate environmental consistency
            if (characteristics.Environmental == "weatherproof" && 
                !ContainsKeywords(combined, new[] { "WP", "WEATHERPROOF", "OUTDOOR", "NEMA" }))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ VALIDATION WARNING: Weatherproof classification without clear indicators");
                // Allow but warn
            }
            
            // Rule 4: Combination devices must have both components mentioned
            if (deviceIdentity == "HORN_STROBE" && 
                (!combined.Contains("HORN") && !combined.Contains("STROBE")))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ VALIDATION FAILED: Horn strobe must mention both horn and strobe");
                return false;
            }
            
            return true; // Validation passed
        }

        /// <summary>
        /// STEP 4: Find specification based on validated classification
        /// </summary>
        private IDNACDeviceSpec FindSpecificationByClassification(string deviceIdentity, DeviceCharacteristics characteristics, string candela)
        {
            var targetCandela = !string.IsNullOrEmpty(candela) ? candela : "75";
            
            // Map device identity to specification key
            var specKey = $"{MapDeviceIdentityToKey(deviceIdentity)}_{characteristics.Mounting}_{characteristics.Environmental}_{targetCandela}";
            
            System.Diagnostics.Debug.WriteLine($"Specification lookup key: {specKey}");
            
            // Get specifications from comprehensive database
            var specs = GetPrecisionDeviceSpecifications();
            
            if (specs.TryGetValue(specKey, out var spec))
            {
                return CreateIDNACDeviceSpec(spec.description, spec.current, spec.wattage, spec.unitLoads);
            }
            
            // Try fallbacks
            return TrySpecificationFallbacks(deviceIdentity, characteristics, targetCandela, specs);
        }

        /// <summary>
        /// Map device identity to specification key format
        /// </summary>
        private string MapDeviceIdentityToKey(string deviceIdentity)
        {
            return deviceIdentity switch
            {
                "SPEAKER_STROBE" => "speakerstrobe",
                "HORN_STROBE" => "hornstrobe",
                "MULTITONE_HORN_STROBE" => "hornstrobe", // Use horn strobe specs with adjustment
                "SPEAKER" => "speaker",
                "HORN" => "horn",
                "MULTITONE_HORN" => "horn", // Use horn specs with adjustment
                "STROBE" => "strobe",
                "CHIME" => "chime",
                _ => "hornstrobe" // Default fallback
            };
        }

        /// <summary>
        /// Try specification fallbacks when exact match not found
        /// </summary>
        private IDNACDeviceSpec TrySpecificationFallbacks(string deviceIdentity, DeviceCharacteristics characteristics, string targetCandela, Dictionary<string, (double current, double wattage, int unitLoads, string description)> specs)
        {
            var deviceKey = MapDeviceIdentityToKey(deviceIdentity);
            
            // Fallback 1: Try standard environmental if weatherproof not found
            if (characteristics.Environmental == "weatherproof")
            {
                var standardKey = $"{deviceKey}_{characteristics.Mounting}_standard_{targetCandela}";
                if (specs.TryGetValue(standardKey, out var standardSpec))
                {
                    // Apply weatherproof multiplier
                    var wpCurrent = standardSpec.current * 1.15; // 15% higher for weatherproof
                    System.Diagnostics.Debug.WriteLine($"✓ FALLBACK: Weatherproof adjustment applied → {wpCurrent:F3}A");
                    return CreateIDNACDeviceSpec($"WP {standardSpec.description}", wpCurrent, standardSpec.wattage, standardSpec.unitLoads);
                }
            }
            
            // Fallback 2: Try 75cd if specific candela not found
            if (targetCandela != "75")
            {
                var candela75Key = $"{deviceKey}_{characteristics.Mounting}_{characteristics.Environmental}_75";
                if (specs.TryGetValue(candela75Key, out var candela75Spec))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ FALLBACK: Using 75cd specification → {candela75Spec.current}A");
                    return CreateIDNACDeviceSpec(candela75Spec.description, candela75Spec.current, candela75Spec.wattage, candela75Spec.unitLoads);
                }
            }
            
            // Fallback 3: Try wall mounting if ceiling not found
            if (characteristics.Mounting == "ceiling")
            {
                var wallKey = $"{deviceKey}_wall_{characteristics.Environmental}_{targetCandela}";
                if (specs.TryGetValue(wallKey, out var wallSpec))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ FALLBACK: Using wall mounting specification → {wallSpec.current}A");
                    return CreateIDNACDeviceSpec($"Ceiling {wallSpec.description}", wallSpec.current, wallSpec.wattage, wallSpec.unitLoads);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"✗ No fallback specification found");
            return null;
        }

        /// <summary>
        /// Comprehensive device specifications
        /// </summary>
        private Dictionary<string, (double current, double wattage, int unitLoads, string description)> GetPrecisionDeviceSpecifications()
        {
            return new Dictionary<string, (double current, double wattage, int unitLoads, string description)>
            {
                // Horn Strobe - Wall - Standard
                ["hornstrobe_wall_standard_15"] = (0.075, 1.8, 1, "Wall Horn Strobe 15cd"),
                ["hornstrobe_wall_standard_30"] = (0.116, 2.8, 1, "Wall Horn Strobe 30cd"),
                ["hornstrobe_wall_standard_75"] = (0.221, 5.3, 1, "Wall Horn Strobe 75cd"),
                ["hornstrobe_wall_standard_110"] = (0.285, 6.8, 1, "Wall Horn Strobe 110cd"),
                ["hornstrobe_wall_standard_135"] = (0.333, 8.0, 1, "Wall Horn Strobe 135cd"),
                ["hornstrobe_wall_standard_177"] = (0.418, 10.0, 1, "Wall Horn Strobe 177cd"),
                ["hornstrobe_wall_standard_185"] = (0.433, 10.4, 1, "Wall Horn Strobe 185cd"),
                
                // Horn Strobe - Wall - Weatherproof
                ["hornstrobe_wall_weatherproof_15"] = (0.135, 3.2, 1, "WP Wall Horn Strobe 15cd"),
                ["hornstrobe_wall_weatherproof_30"] = (0.155, 3.7, 1, "WP Wall Horn Strobe 30cd"),
                ["hornstrobe_wall_weatherproof_75"] = (0.205, 4.9, 1, "WP Wall Horn Strobe 75cd"),
                ["hornstrobe_wall_weatherproof_185"] = (0.255, 6.1, 1, "WP Wall Horn Strobe 185cd"),
                
                // Speaker Strobe - Wall - Standard  
                ["speakerstrobe_wall_standard_15"] = (0.080, 3.0, 1, "Wall Speaker Strobe 15cd"),
                ["speakerstrobe_wall_standard_30"] = (0.105, 3.5, 1, "Wall Speaker Strobe 30cd"),
                ["speakerstrobe_wall_standard_75"] = (0.206, 6.0, 1, "Wall Speaker Strobe 75cd"),
                ["speakerstrobe_wall_standard_110"] = (0.272, 7.5, 1, "Wall Speaker Strobe 110cd"),
                ["speakerstrobe_wall_standard_135"] = (0.334, 9.0, 1, "Wall Speaker Strobe 135cd"),
                ["speakerstrobe_wall_standard_177"] = (0.410, 11.5, 1, "Wall Speaker Strobe 177cd"),
                ["speakerstrobe_wall_standard_185"] = (0.429, 12.3, 1, "Wall Speaker Strobe 185cd"),
                
                // Strobe Only - Wall - Standard
                ["strobe_wall_standard_15"] = (0.060, 1.4, 1, "Wall Strobe 15cd"),
                ["strobe_wall_standard_30"] = (0.094, 2.3, 1, "Wall Strobe 30cd"),
                ["strobe_wall_standard_75"] = (0.186, 4.5, 1, "Wall Strobe 75cd"),
                ["strobe_wall_standard_110"] = (0.252, 6.0, 1, "Wall Strobe 110cd"),
                ["strobe_wall_standard_135"] = (0.314, 7.5, 1, "Wall Strobe 135cd"),
                ["strobe_wall_standard_177"] = (0.390, 9.4, 1, "Wall Strobe 177cd"),
                ["strobe_wall_standard_185"] = (0.409, 9.8, 1, "Wall Strobe 185cd"),
                
                // Horn Only - Wall
                ["horn_wall_standard_75"] = (0.020, 0.48, 1, "Wall Horn"),
                ["horn_wall_weatherproof_75"] = (0.052, 1.2, 1, "WP Wall Horn"),
                
                // Speaker Only - Wall
                ["speaker_wall_standard_75"] = (0.020, 2.0, 1, "Wall Speaker"),
                
                // Ceiling variants
                ["hornstrobe_ceiling_standard_75"] = (0.250, 6.0, 1, "Ceiling Horn Strobe 75cd"),
                ["hornstrobe_ceiling_standard_185"] = (0.463, 11.1, 1, "Ceiling Horn Strobe 185cd"),
                ["strobe_ceiling_standard_75"] = (0.233, 5.6, 1, "Ceiling Strobe 75cd"),
                ["strobe_ceiling_standard_185"] = (0.443, 10.6, 1, "Ceiling Strobe 185cd"),
            };
        }

        /// <summary>
        /// Create IDNACDeviceSpec from specification
        /// </summary>
        private IDNACDeviceSpec CreateIDNACDeviceSpec(string description, double current, double wattage, int unitLoads)
        {
            return new IDNACDeviceSpec
            {
                FamilyName = description,
                TypeName = description,
                Description = description,
                CombinedCurrent = current,
                UnitLoads = unitLoads,
                TTapCompatible = true,
                Mounting = "wall",
                EnvironmentalRating = "standard"
            };
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
    /// Supporting class for device characteristics
    /// </summary>
    public class DeviceCharacteristics
    {
        public string Mounting { get; set; } = "wall";
        public string Environmental { get; set; } = "standard";
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