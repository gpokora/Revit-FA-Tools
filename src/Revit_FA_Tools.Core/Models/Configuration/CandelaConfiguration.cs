using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Configuration model for Candela to Current mapping loaded from JSON
    /// </summary>
    public class CandelaConfiguration
    {
        [JsonProperty("ConfigurationInfo")]
        public ConfigurationInfo Info { get; set; }

        [JsonProperty("DeviceTypes")]
        public Dictionary<string, DeviceTypeConfig> DeviceTypes { get; set; }

        [JsonProperty("FallbackHierarchy")]
        public Dictionary<string, List<string>> FallbackHierarchy { get; set; }

        [JsonProperty("DeviceRecognitionPatterns")]
        public DeviceRecognitionPatterns RecognitionPatterns { get; set; }

        public CandelaConfiguration()
        {
            Info = new ConfigurationInfo();
            DeviceTypes = new Dictionary<string, DeviceTypeConfig>();
            FallbackHierarchy = new Dictionary<string, List<string>>();
            RecognitionPatterns = new DeviceRecognitionPatterns();
        }
    }

    public class ConfigurationInfo
    {
        [JsonProperty("Version")]
        public string Version { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("LastUpdated")]
        public string LastUpdated { get; set; }

        [JsonProperty("Voltage")]
        public string Voltage { get; set; }

        [JsonProperty("Notes")]
        public string Notes { get; set; }
    }

    public class DeviceTypeConfig
    {
        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("MountingType")]
        public string MountingType { get; set; }

        [JsonProperty("EnvironmentalRating")]
        public string EnvironmentalRating { get; set; }

        [JsonProperty("DeviceFunction")]
        public string DeviceFunction { get; set; }

        [JsonProperty("CandelaCurrentMap")]
        public Dictionary<string, double> CandelaCurrentMap { get; set; }

        [JsonProperty("UnitLoadMap")]
        public Dictionary<string, int> UnitLoadMap { get; set; }

        [JsonProperty("IsAudioDevice")]
        public bool IsAudioDevice { get; set; } = false;

        [JsonProperty("IsSpeaker")]
        public bool IsSpeaker { get; set; } = false;

        [JsonProperty("HasStrobe")]
        public bool HasStrobe { get; set; } = false;

        public DeviceTypeConfig()
        {
            CandelaCurrentMap = new Dictionary<string, double>();
            UnitLoadMap = new Dictionary<string, int>();
        }
    }

    public class DeviceRecognitionPatterns
    {
        [JsonProperty("MountingTypes")]
        public Dictionary<string, List<string>> MountingTypes { get; set; }

        [JsonProperty("DeviceFunctions")]
        public Dictionary<string, List<string>> DeviceFunctions { get; set; }

        public DeviceRecognitionPatterns()
        {
            MountingTypes = new Dictionary<string, List<string>>();
            DeviceFunctions = new Dictionary<string, List<string>>();
        }
    }

    /// <summary>
    /// Service class to load and manage Candela configuration
    /// </summary>
    public class CandelaConfigurationService
    {
        private static CandelaConfiguration _cachedConfiguration;
        private static readonly object _lock = new object();
        private const string DEFAULT_CONFIG_FILENAME = "CandelaCurrentMapping.json";

        /// <summary>
        /// Load candela configuration from JSON file
        /// </summary>
        public static CandelaConfiguration LoadConfiguration(string? configFilePath = null)
        {
            lock (_lock)
            {
                if (_cachedConfiguration != null)
                    return _cachedConfiguration;

                try
                {
                    // Determine config file path
                    string filePath = configFilePath ?? GetDefaultConfigPath();

                    if (!File.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Candela configuration file not found: {filePath}");
                        return CreateDefaultConfiguration();
                    }

                    // Load and parse JSON
                    string jsonContent = File.ReadAllText(filePath);
                    _cachedConfiguration = JsonConvert.DeserializeObject<CandelaConfiguration>(jsonContent);

                    if (_cachedConfiguration == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to parse candela configuration JSON");
                        return CreateDefaultConfiguration();
                    }

                    System.Diagnostics.Debug.WriteLine($"Loaded candela configuration from: {filePath}");
                    System.Diagnostics.Debug.WriteLine($"Configuration version: {_cachedConfiguration.Info?.Version}");
                    System.Diagnostics.Debug.WriteLine($"Device types loaded: {_cachedConfiguration.DeviceTypes?.Count ?? 0}");

                    return _cachedConfiguration;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading candela configuration: {ex.Message}");
                    return CreateDefaultConfiguration();
                }
            }
        }

        /// <summary>
        /// Get the default configuration file path
        /// </summary>
        private static string GetDefaultConfigPath()
        {
            // Try multiple locations in order of preference
            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEFAULT_CONFIG_FILENAME),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Revit_FA_Tools", DEFAULT_CONFIG_FILENAME),
                Path.Combine(Path.GetDirectoryName(typeof(CandelaConfigurationService).Assembly.Location), DEFAULT_CONFIG_FILENAME)
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return searchPaths[0]; // Return first path as default
        }

        /// <summary>
        /// Create default configuration when JSON file is not available
        /// </summary>
        private static CandelaConfiguration CreateDefaultConfiguration()
        {
            return new CandelaConfiguration
            {
                Info = new ConfigurationInfo
                {
                    Version = "1.0.0",
                    Description = "Default Fire Alarm Notification Device Candela to Current Mapping",
                    Voltage = "24V DC",
                    Notes = "Fallback configuration when JSON file is not available"
                },
                DeviceTypes = new Dictionary<string, DeviceTypeConfig>
                {
                    ["WALL_STROBE"] = new DeviceTypeConfig
                    {
                        Description = "Wall-Mount Strobe Devices - Default Fallback",
                        CandelaCurrentMap = new Dictionary<string, double>
                        {
                            ["15"] = 0.095,
                            ["30"] = 0.115,
                            ["75"] = 0.140,
                            ["95"] = 0.160,
                            ["110"] = 0.170,
                            ["135"] = 0.185,
                            ["177"] = 0.205,
                            ["185"] = 0.210
                        }
                    }
                },
                FallbackHierarchy = new Dictionary<string, List<string>>(),
                RecognitionPatterns = new DeviceRecognitionPatterns()
            };
        }

        /// <summary>
        /// Force reload of configuration (useful for testing or config updates)
        /// </summary>
        public static void ReloadConfiguration()
        {
            lock (_lock)
            {
                _cachedConfiguration = null;
            }
        }

        /// <summary>
        /// Save configuration to JSON file
        /// </summary>
        public static bool SaveConfiguration(CandelaConfiguration config, string? filePath = null)
        {
            try
            {
                string path = filePath ?? GetDefaultConfigPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(path, jsonContent);
                
                System.Diagnostics.Debug.WriteLine($"Saved candela configuration to: {path}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving candela configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Classify device and get current/UL values based on speaker vs other notification logic
        /// </summary>
        public static DeviceClassification ClassifyDevice(string familyName, string typeName)
        {
            // Handle null inputs safely
            familyName = familyName ?? "Unknown";
            typeName = typeName ?? "Unknown";
            
            var config = LoadConfiguration();
            var classification = new DeviceClassification
            {
                FamilyName = familyName,
                TypeName = typeName,
                Current = 0.0,
                UnitLoads = 1,
                IsAudioDevice = false,
                IsSpeaker = false,
                HasStrobe = false
            };

            try
            {
                // First check for exact match - safely handle null config or DeviceTypes
                if (config?.DeviceTypes != null)
                {
                    var deviceKey = GetDeviceKey(familyName, typeName);
                    if (!string.IsNullOrEmpty(deviceKey) && config.DeviceTypes.ContainsKey(deviceKey))
                    {
                        var deviceConfig = config.DeviceTypes[deviceKey];
                        return ApplyDeviceConfig(deviceConfig, classification);
                    }
                }

                // Pattern-based classification for speakers
                if (IsSpeakerDevice(familyName, typeName))
                {
                    classification.IsSpeaker = true;
                    classification.IsAudioDevice = true;
                    // Speakers: Amps = 0 (per instructions), UL from mapping or default
                    classification.Current = 0.0;
                    classification.UnitLoads = GetUnitLoadsFromMapping(familyName, typeName) ?? 1;
                    
                    // Check if it's a speaker-strobe combo
                    if (IsStrobeDevice(familyName, typeName))
                    {
                        classification.HasStrobe = true;
                    }
                }
                else
                {
                    // All other notification devices: Amps and UL from mapping
                    classification.Current = GetCurrentFromMapping(familyName, typeName) ?? 0.020; // Default 20mA
                    classification.UnitLoads = GetUnitLoadsFromMapping(familyName, typeName) ?? 1;
                    
                    if (IsStrobeDevice(familyName, typeName))
                    {
                        classification.HasStrobe = true;
                    }
                }

                return classification;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error classifying device {familyName}/{typeName}: {ex.Message}");
                return classification;
            }
        }

        private static DeviceClassification ApplyDeviceConfig(DeviceTypeConfig config, DeviceClassification classification)
        {
            classification.IsAudioDevice = config.IsAudioDevice;
            classification.IsSpeaker = config.IsSpeaker;
            classification.HasStrobe = config.HasStrobe;

            if (config.IsSpeaker)
            {
                // Speakers: Amps = 0, UL from mapping
                classification.Current = 0.0;
                classification.UnitLoads = GetFirstUnitLoadValue(config.UnitLoadMap) ?? 1;
            }
            else
            {
                // Other notification: Amps and UL from mapping
                classification.Current = GetFirstCurrentValue(config.CandelaCurrentMap) ?? 0.020;
                classification.UnitLoads = GetFirstUnitLoadValue(config.UnitLoadMap) ?? 1;
            }

            return classification;
        }

        private static bool IsSpeakerDevice(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            return combined.Contains("SPEAKER") || 
                   combined.Contains("AUDIO") ||
                   combined.Contains("VOICE") ||
                   combined.Contains("HORN");
        }

        private static bool IsStrobeDevice(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            return combined.Contains("STROBE");
        }

        private static double? GetCurrentFromMapping(string familyName, string typeName)
        {
            var config = LoadConfiguration();
            if (config?.DeviceTypes == null) return null;
            
            var deviceKey = GetDeviceKey(familyName, typeName);
            if (string.IsNullOrEmpty(deviceKey)) return null;
            
            if (config.DeviceTypes.ContainsKey(deviceKey))
            {
                var deviceConfig = config.DeviceTypes[deviceKey];
                return GetFirstCurrentValue(deviceConfig?.CandelaCurrentMap);
            }

            // Try fallback patterns
            return TryFallbackCurrentLookup(familyName, typeName);
        }

        private static int? GetUnitLoadsFromMapping(string familyName, string typeName)
        {
            var config = LoadConfiguration();
            if (config?.DeviceTypes == null) return null;
            
            var deviceKey = GetDeviceKey(familyName, typeName);
            if (string.IsNullOrEmpty(deviceKey)) return null;
            
            if (config.DeviceTypes.ContainsKey(deviceKey))
            {
                var deviceConfig = config.DeviceTypes[deviceKey];
                return GetFirstUnitLoadValue(deviceConfig?.UnitLoadMap);
            }

            // Try fallback patterns
            return TryFallbackULLookup(familyName, typeName);
        }

        private static double? GetFirstCurrentValue(Dictionary<string, double> currentMap)
        {
            if (currentMap?.Any() == true)
            {
                return currentMap.Values.First();
            }
            return null;
        }

        private static int? GetFirstUnitLoadValue(Dictionary<string, int> ulMap)
        {
            if (ulMap?.Any() == true)
            {
                return ulMap.Values.First();
            }
            return null;
        }

        private static double? TryFallbackCurrentLookup(string familyName, string typeName)
        {
            // Basic fallback based on device type patterns
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            
            if (combined.Contains("SMOKE")) return 0.050; // 50mA typical
            if (combined.Contains("HEAT")) return 0.040;  // 40mA typical  
            if (combined.Contains("PULL")) return 0.020;  // 20mA typical
            if (combined.Contains("STROBE")) return 0.140; // 140mA typical
            if (combined.Contains("HORN")) return 0.020;   // 20mA for horn circuit
            
            return 0.020; // Default 20mA
        }

        private static int? TryFallbackULLookup(string familyName, string typeName)
        {
            // Basic fallback UL based on device type patterns
            var combined = $"{familyName} {typeName}".ToUpperInvariant();
            
            if (combined.Contains("ISOLATOR")) return 4;   // 4 UL typical
            if (combined.Contains("REPEATER")) return 4;   // 4 UL typical
            if (combined.Contains("MT") && combined.Contains("520")) return 2; // MT 520 Hz = 2 UL
            if (combined.Contains("STROBE")) return 1;     // 1 UL for visual
            
            return 1; // Default 1 UL
        }

        private static string GetDeviceKey(string familyName, string typeName)
        {
            // Handle null inputs safely
            familyName = familyName ?? "Unknown";
            typeName = typeName ?? "Unknown";
            return $"{familyName}|{typeName}";
        }
    }

    /// <summary>
    /// Result of device classification with current and UL values
    /// </summary>
    public class DeviceClassification
    {
        public string? FamilyName { get; set; }
        public string TypeName { get; set; }
        public double Current { get; set; }
        public int UnitLoads { get; set; }
        public bool IsAudioDevice { get; set; }
        public bool IsSpeaker { get; set; }
        public bool HasStrobe { get; set; }
        public string Classification => IsSpeaker ? "Audio Device" : "Notification Device";
    }
}