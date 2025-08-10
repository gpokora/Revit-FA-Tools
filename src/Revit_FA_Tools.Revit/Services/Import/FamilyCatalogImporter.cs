using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Service for importing pyRevit family catalog JSON files and merging with device mapping
    /// </summary>
    public class FamilyCatalogImporter
    {
        public class FamilyCatalogEntry
        {
            [JsonProperty("family_name")]
            public string? FamilyName { get; set; }

            [JsonProperty("type_name")]
            public string TypeName { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; }

            [JsonProperty("parameters")]
            public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

            [JsonProperty("subcategory")]
            public string Subcategory { get; set; }

            [JsonProperty("workset")]
            public string Workset { get; set; }

            [JsonProperty("level")]
            public string Level { get; set; }

            [JsonProperty("is_speaker")]
            public bool IsSpeaker { get; set; }

            [JsonProperty("has_strobe")]
            public bool HasStrobe { get; set; }

            [JsonProperty("is_notification_device")]
            public bool IsNotificationDevice { get; set; }

            [JsonProperty("watts")]
            public double? Watts { get; set; }

            [JsonProperty("current_draw")]
            public double? CurrentDraw { get; set; }

            [JsonProperty("unit_loads")]
            public int? UnitLoads { get; set; }
        }

        public class FamilyCatalogDocument
        {
            [JsonProperty("catalog_info")]
            public CatalogInfo CatalogInfo { get; set; }

            [JsonProperty("families")]
            public List<FamilyCatalogEntry> Families { get; set; } = new List<FamilyCatalogEntry>();
        }

        public class CatalogInfo
        {
            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("created_date")]
            public string CreatedDate { get; set; }

            [JsonProperty("project_name")]
            public string ProjectName { get; set; }

            [JsonProperty("total_families")]
            public int TotalFamilies { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }
        }

        public class ImportResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int FamiliesProcessed { get; set; }
            public int NewMappingsAdded { get; set; }
            public int ExistingMappingsUpdated { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> Errors { get; set; } = new List<string>();
        }

        /// <summary>
        /// Import pyRevit family catalog JSON file and merge with existing device mapping
        /// </summary>
        public ImportResult ImportFamilyCatalog(string catalogFilePath)
        {
            var result = new ImportResult();

            try
            {
                // Validate file path
                if (string.IsNullOrEmpty(catalogFilePath) || !File.Exists(catalogFilePath))
                {
                    result.Success = false;
                    result.Message = $"Catalog file not found: {catalogFilePath}";
                    return result;
                }

                // Load and parse JSON
                var jsonContent = File.ReadAllText(catalogFilePath);
                var catalog = JsonConvert.DeserializeObject<FamilyCatalogDocument>(jsonContent);

                if (catalog?.Families == null)
                {
                    result.Success = false;
                    result.Message = "Invalid catalog format - no families found";
                    return result;
                }

                // Load existing candela configuration
                var candelaConfig = CandelaConfigurationService.LoadConfiguration();
                if (candelaConfig == null)
                {
                    result.Success = false;
                    result.Message = "Failed to load existing candela configuration";
                    return result;
                }

                // Process catalog entries
                foreach (var catalogEntry in catalog.Families)
                {
                    try
                    {
                        ProcessCatalogEntry(catalogEntry, candelaConfig, result);
                        result.FamiliesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to process family {catalogEntry.FamilyName}: {ex.Message}");
                    }
                }

                // Save updated configuration
                if (result.NewMappingsAdded > 0 || result.ExistingMappingsUpdated > 0)
                {
                    CandelaConfigurationService.SaveConfiguration(candelaConfig);
                    CandelaConfigurationService.ReloadConfiguration(); // Clear cache
                }

                result.Success = true;
                result.Message = $"Import completed: {result.FamiliesProcessed} families processed, " +
                               $"{result.NewMappingsAdded} new mappings added, " +
                               $"{result.ExistingMappingsUpdated} existing mappings updated";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Import failed: {ex.Message}";
                result.Errors.Add(ex.ToString());
                return result;
            }
        }

        /// <summary>
        /// Process a single catalog entry and merge with device configuration
        /// </summary>
        private void ProcessCatalogEntry(FamilyCatalogEntry catalogEntry, CandelaConfiguration config, ImportResult result)
        {
            if (string.IsNullOrEmpty(catalogEntry.FamilyName))
                return;

            var deviceKey = !string.IsNullOrEmpty(catalogEntry.TypeName) 
                ? $"{catalogEntry.FamilyName}|{catalogEntry.TypeName}"
                : catalogEntry.FamilyName;

            // Check if device type already exists
            var existingDevice = config.DeviceTypes.ContainsKey(deviceKey);
            var deviceConfig = existingDevice 
                ? config.DeviceTypes[deviceKey] 
                : new DeviceTypeConfig();

            // Merge catalog data into device configuration
            MergeCatalogData(catalogEntry, deviceConfig);

            // Update configuration
            config.DeviceTypes[deviceKey] = deviceConfig;

            if (existingDevice)
            {
                result.ExistingMappingsUpdated++;
            }
            else
            {
                result.NewMappingsAdded++;
            }
        }

        /// <summary>
        /// Merge catalog entry data into device type configuration
        /// </summary>
        private void MergeCatalogData(FamilyCatalogEntry catalogEntry, DeviceTypeConfig deviceConfig)
        {
            // Update basic device properties
            deviceConfig.Description = deviceConfig.Description ?? 
                $"Imported from catalog: {catalogEntry.FamilyName}" +
                (!string.IsNullOrEmpty(catalogEntry.TypeName) ? $" - {catalogEntry.TypeName}" : "");

            deviceConfig.DeviceFunction = deviceConfig.DeviceFunction ?? catalogEntry.Category;

            // Set device characteristics based on catalog flags
            deviceConfig.IsSpeaker = catalogEntry.IsSpeaker;
            deviceConfig.HasStrobe = catalogEntry.HasStrobe;
            deviceConfig.IsAudioDevice = catalogEntry.IsSpeaker || 
                (catalogEntry.FamilyName?.ToLower().Contains("horn") == true);

            // Auto-populate current mapping if available from catalog
            if (catalogEntry.CurrentDraw.HasValue && catalogEntry.CurrentDraw.Value > 0)
            {
                var currentKey = "Default";
                if (deviceConfig.CandelaCurrentMap == null)
                    deviceConfig.CandelaCurrentMap = new Dictionary<string, double>();
                    
                deviceConfig.CandelaCurrentMap[currentKey] = catalogEntry.CurrentDraw.Value;
            }

            // Auto-populate unit load mapping
            if (catalogEntry.UnitLoads.HasValue && catalogEntry.UnitLoads.Value > 0)
            {
                var ulKey = "Default";
                if (deviceConfig.UnitLoadMap == null)
                    deviceConfig.UnitLoadMap = new Dictionary<string, int>();
                    
                deviceConfig.UnitLoadMap[ulKey] = catalogEntry.UnitLoads.Value;
            }
            else
            {
                // Apply default unit load rules
                var defaultUL = GetDefaultUnitLoads(catalogEntry.FamilyName, catalogEntry.TypeName);
                if (defaultUL > 0)
                {
                    var ulKey = "Default";
                    if (deviceConfig.UnitLoadMap == null)
                        deviceConfig.UnitLoadMap = new Dictionary<string, int>();
                        
                    deviceConfig.UnitLoadMap[ulKey] = defaultUL;
                }
            }

            // Merge parameters if available
            if (catalogEntry.Parameters?.Any() == true)
            {
                foreach (var param in catalogEntry.Parameters)
                {
                    // Extract electrical parameters
                    if (param.Key.ToLower().Contains("wattage") && double.TryParse(param.Value?.ToString(), out var watts))
                    {
                        // Could be used for audio calculations
                    }
                    else if (param.Key.ToLower().Contains("current") && double.TryParse(param.Value?.ToString(), out var current))
                    {
                        var currentKey = "Default";
                        if (deviceConfig.CandelaCurrentMap == null)
                            deviceConfig.CandelaCurrentMap = new Dictionary<string, double>();
                            
                        deviceConfig.CandelaCurrentMap[currentKey] = current;
                    }
                }
            }
        }

        /// <summary>
        /// Get default unit loads based on device type patterns
        /// </summary>
        private int GetDefaultUnitLoads(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLower();
            
            if (combined.Contains("isolator")) return 4;
            if (combined.Contains("repeater")) return 4;
            if (combined.Contains("mt") && combined.Contains("520")) return 2;
            
            return 1; // Default UL
        }

        /// <summary>
        /// Auto-detect pyRevit catalog files in common locations
        /// </summary>
        public List<string> FindPyRevitCatalogFiles()
        {
            var catalogFiles = new List<string>();
            var searchPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "pyRevit"),
                Environment.CurrentDirectory
            };

            foreach (var searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    var files = Directory.GetFiles(searchPath, "fa_family_catalog_*.json", SearchOption.AllDirectories);
                    catalogFiles.AddRange(files);
                }
            }

            return catalogFiles.Distinct().ToList();
        }

        /// <summary>
        /// Validate catalog file format before import
        /// </summary>
        public ValidationResult ValidateCatalogFile(string filePath)
        {
            var result = new ValidationResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.Errors.Add("File does not exist");
                    return result;
                }

                var jsonContent = File.ReadAllText(filePath);
                var catalog = JsonConvert.DeserializeObject<FamilyCatalogDocument>(jsonContent);

                if (catalog == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid JSON format");
                    return result;
                }

                if (catalog.Families == null || !catalog.Families.Any())
                {
                    result.IsValid = false;
                    result.Errors.Add("No families found in catalog");
                    return result;
                }

                result.IsValid = true;
                result.FamilyCount = catalog.Families.Count;
                result.CatalogInfo = catalog.CatalogInfo;

                // Check for potential issues
                var entriesWithoutFamily = catalog.Families.Count(f => string.IsNullOrEmpty(f.FamilyName));
                if (entriesWithoutFamily > 0)
                {
                    result.Warnings.Add($"{entriesWithoutFamily} entries have no family name");
                }

                var notificationDevices = catalog.Families.Count(f => f.IsNotificationDevice);
                result.Warnings.Add($"Found {notificationDevices} notification devices out of {catalog.Families.Count} total");

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public int FamilyCount { get; set; }
            public CatalogInfo CatalogInfo { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
        }
    }
}