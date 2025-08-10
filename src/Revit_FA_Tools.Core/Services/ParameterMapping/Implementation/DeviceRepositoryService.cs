using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// AutoCall 4100ES device repository service with FQQ Excel accuracy
    /// Provides fast lookup of device specifications by family name and parameters
    /// </summary>
    public class DeviceRepositoryService
    {
        private static List<AutoCallDevice> _deviceCatalog;
        private static Dictionary<string, List<AutoCallDevice>> _familyIndex;
        private static Dictionary<string, AutoCallDevice> _skuIndex;
        private static readonly object _lockObject = new object();
        
        public DeviceRepositoryService()
        {
            EnsureCatalogLoaded();
        }
        
        /// <summary>
        /// Find device specification with high-performance lookup
        /// </summary>
        public DeviceSpecification FindSpecification(DeviceSnapshot device, Dictionary<string, object> parameters = null)
        {
            try
            {
                EnsureCatalogLoaded();
                
                // Strategy 1: Direct family name match
                var candidates = FindByFamilyName(device.FamilyName);
                
                if (candidates.Any())
                {
                    // Refine by parameters if available
                    var best = RefineByParameters(candidates, parameters, device);
                    if (best != null)
                    {
                        return ConvertToSpecification(best);
                    }
                }
                
                // Strategy 2: Fuzzy matching by device characteristics
                candidates = FindByCharacteristics(device);
                if (candidates.Any())
                {
                    var best = RefineByParameters(candidates, parameters, device);
                    if (best != null)
                    {
                        return ConvertToSpecification(best);
                    }
                }
                
                // Strategy 3: Fallback to device type matching
                candidates = FindByDeviceType(device);
                if (candidates.Any())
                {
                    var best = candidates.First(); // Take first match as fallback
                    return ConvertToSpecification(best);
                }
                
                return CreateFallbackSpecification(device);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Repository lookup failed: {ex.Message}");
                return CreateFallbackSpecification(device);
            }
        }
        
        /// <summary>
        /// Find devices by SKU for direct lookup
        /// </summary>
        public DeviceSpecification FindBySKU(string sku)
        {
            EnsureCatalogLoaded();
            
            if (_skuIndex.TryGetValue(sku.ToUpper(), out var device))
            {
                return ConvertToSpecification(device);
            }
            
            return null;
        }
        
        /// <summary>
        /// Get all devices in a category
        /// </summary>
        public List<DeviceSpecification> GetDevicesByCategory(string category)
        {
            EnsureCatalogLoaded();
            
            return _deviceCatalog
                .Where(d => string.Equals(d.DeviceCategory, category, StringComparison.OrdinalIgnoreCase))
                .Select(ConvertToSpecification)
                .ToList();
        }
        
        /// <summary>
        /// Get device catalog statistics
        /// </summary>
        public CatalogStatistics GetCatalogStatistics()
        {
            EnsureCatalogLoaded();
            
            return new CatalogStatistics
            {
                TotalDevices = _deviceCatalog.Count,
                Categories = _deviceCatalog.Select(d => d.DeviceCategory).Distinct().Count(),
                Manufacturers = _deviceCatalog.Select(d => d.Manufacturer).Distinct().Count(),
                AverageCurrentDraw = _deviceCatalog.Average(d => d.StandbyCurrentmA),
                HasTTapDevices = _deviceCatalog.Any(d => d.IsTTapCompatible)
            };
        }
        
        private void EnsureCatalogLoaded()
        {
            if (_deviceCatalog != null) return;
            
            lock (_lockObject)
            {
                if (_deviceCatalog != null) return;
                
                LoadDeviceCatalog();
                BuildIndices();
            }
        }
        
        private void LoadDeviceCatalog()
        {
            try
            {
                // Load from embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Revit_FA_Tools.Data.AutoCallDeviceCatalog.json";
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            _deviceCatalog = JsonConvert.DeserializeObject<List<AutoCallDevice>>(json);
                        }
                    }
                }
                
                // Fallback: try loading from file system
                if (_deviceCatalog == null)
                {
                    var catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "AutoCallDeviceCatalog.json");
                    if (File.Exists(catalogPath))
                    {
                        var json = File.ReadAllText(catalogPath);
                        _deviceCatalog = JsonConvert.DeserializeObject<List<AutoCallDevice>>(json);
                    }
                }
                
                // Ultimate fallback: create minimal catalog
                if (_deviceCatalog == null)
                {
                    _deviceCatalog = CreateMinimalCatalog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load device catalog: {ex.Message}");
                _deviceCatalog = CreateMinimalCatalog();
            }
        }
        
        private void BuildIndices()
        {
            _familyIndex = new Dictionary<string, List<AutoCallDevice>>();
            _skuIndex = new Dictionary<string, AutoCallDevice>();
            
            foreach (var device in _deviceCatalog)
            {
                // Family name index
                var familyKey = NormalizeName(device.FamilyName);
                if (!_familyIndex.ContainsKey(familyKey))
                {
                    _familyIndex[familyKey] = new List<AutoCallDevice>();
                }
                _familyIndex[familyKey].Add(device);
                
                // SKU index
                if (!string.IsNullOrEmpty(device.SKU))
                {
                    _skuIndex[device.SKU.ToUpper()] = device;
                }
                
                // Alternative SKU index
                if (!string.IsNullOrEmpty(device.AlternateSKU))
                {
                    _skuIndex[device.AlternateSKU.ToUpper()] = device;
                }
            }
        }
        
        private List<AutoCallDevice> FindByFamilyName(string familyName)
        {
            if (string.IsNullOrEmpty(familyName)) return new List<AutoCallDevice>();
            
            var normalizedName = NormalizeName(familyName);
            
            if (_familyIndex.TryGetValue(normalizedName, out var exact))
            {
                return exact;
            }
            
            // Fuzzy matching for family names
            var candidates = new List<AutoCallDevice>();
            foreach (var kvp in _familyIndex)
            {
                if (kvp.Key.Contains(normalizedName) || normalizedName.Contains(kvp.Key))
                {
                    candidates.AddRange(kvp.Value);
                }
            }
            
            return candidates;
        }
        
        private List<AutoCallDevice> FindByCharacteristics(DeviceSnapshot device)
        {
            var candidates = new List<AutoCallDevice>();
            
            // Match by device characteristics
            foreach (var catalogDevice in _deviceCatalog)
            {
                var score = CalculateCharacteristicMatch(device, catalogDevice);
                if (score > 0.5) // 50% match threshold
                {
                    candidates.Add(catalogDevice);
                }
            }
            
            return candidates.OrderByDescending(d => CalculateCharacteristicMatch(device, d)).ToList();
        }
        
        private List<AutoCallDevice> FindByDeviceType(DeviceSnapshot device)
        {
            var deviceType = DetermineDeviceType(device);
            
            return _deviceCatalog
                .Where(d => string.Equals(d.DeviceCategory, deviceType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        private AutoCallDevice RefineByParameters(List<AutoCallDevice> candidates, Dictionary<string, object> parameters, DeviceSnapshot device)
        {
            if (parameters == null || !parameters.Any()) 
            {
                return candidates.FirstOrDefault();
            }
            
            var scoredCandidates = candidates
                .Select(c => new { Device = c, Score = CalculateParameterMatchScore(c, parameters, device) })
                .OrderByDescending(x => x.Score)
                .ToList();
            
            return scoredCandidates.FirstOrDefault()?.Device;
        }
        
        private double CalculateCharacteristicMatch(DeviceSnapshot device, AutoCallDevice catalogDevice)
        {
            double score = 0;
            int factors = 0;
            
            // Device type matching
            if (device.HasStrobe && catalogDevice.HasStrobe) { score += 1; factors++; }
            if (device.HasSpeaker && catalogDevice.HasSpeaker) { score += 1; factors++; }
            if (device.IsIsolator && catalogDevice.DeviceCategory == "ISOLATOR") { score += 1; factors++; }
            if (device.IsRepeater && catalogDevice.DeviceCategory == "REPEATER") { score += 1; factors++; }
            
            // Power consumption matching (within 20% tolerance)
            if (device.Watts > 0 && catalogDevice.PowerConsumptionW > 0)
            {
                var powerDiff = Math.Abs(device.Watts - catalogDevice.PowerConsumptionW) / Math.Max(device.Watts, catalogDevice.PowerConsumptionW);
                if (powerDiff < 0.2) { score += 1; factors++; }
            }
            
            return factors > 0 ? score / factors : 0;
        }
        
        private double CalculateParameterMatchScore(AutoCallDevice catalogDevice, Dictionary<string, object> parameters, DeviceSnapshot device)
        {
            double score = 0;
            int factors = 0;
            
            // CANDELA parameter matching
            if (parameters.TryGetValue("CANDELA", out var candela) && int.TryParse(candela.ToString(), out var candelaValue))
            {
                if (catalogDevice.CandelaRating == candelaValue) { score += 2; factors++; }
                else if (Math.Abs(catalogDevice.CandelaRating - candelaValue) <= 15) { score += 1; factors++; }
            }
            
            // WATTAGE parameter matching
            if (parameters.TryGetValue("WATTAGE", out var wattage) && double.TryParse(wattage.ToString(), out var wattageValue))
            {
                if (Math.Abs(catalogDevice.PowerConsumptionW - wattageValue) < 0.5) { score += 2; factors++; }
                else if (Math.Abs(catalogDevice.PowerConsumptionW - wattageValue) < 1.0) { score += 1; factors++; }
            }
            
            // Device characteristics
            if (device.HasStrobe && catalogDevice.HasStrobe) { score += 1; factors++; }
            if (device.HasSpeaker && catalogDevice.HasSpeaker) { score += 1; factors++; }
            
            return factors > 0 ? score / factors : 0;
        }
        
        private DeviceSpecification ConvertToSpecification(AutoCallDevice device)
        {
            return new DeviceSpecification
            {
                SKU = device.SKU,
                Manufacturer = device.Manufacturer,
                ProductName = device.ProductName,
                CurrentDraw = device.StandbyCurrentmA / 1000.0, // Convert mA to A
                PowerConsumption = device.PowerConsumptionW,
                UnitLoads = device.UnitLoads,
                IsTTapCompatible = device.IsTTapCompatible,
                MountingType = device.MountingType,
                EnvironmentalRating = device.EnvironmentalRating,
                IsULListed = device.IsULListed,
                TechnicalSpecs = new Dictionary<string, object>
                {
                    ["CANDELA_RATING"] = device.CandelaRating,
                    ["HAS_STROBE"] = device.HasStrobe,
                    ["HAS_SPEAKER"] = device.HasSpeaker,
                    ["ALARM_CURRENT_MA"] = device.AlarmCurrentmA,
                    ["DEVICE_CATEGORY"] = device.DeviceCategory,
                    ["MODEL_NUMBER"] = device.ModelNumber
                }
            };
        }
        
        private DeviceSpecification CreateFallbackSpecification(DeviceSnapshot device)
        {
            return new DeviceSpecification
            {
                SKU = "UNKNOWN",
                Manufacturer = "GENERIC",
                ProductName = device.FamilyName,
                CurrentDraw = device.Amps > 0 ? device.Amps : (device.Watts / 24.0),
                PowerConsumption = device.Watts,
                UnitLoads = device.UnitLoads,
                IsTTapCompatible = false,
                MountingType = "WALL",
                EnvironmentalRating = "INDOOR",
                IsULListed = true,
                TechnicalSpecs = new Dictionary<string, object>
                {
                    ["FALLBACK"] = true,
                    ["HAS_STROBE"] = device.HasStrobe,
                    ["HAS_SPEAKER"] = device.HasSpeaker
                }
            };
        }
        
        private List<AutoCallDevice> CreateMinimalCatalog()
        {
            return new List<AutoCallDevice>
            {
                new AutoCallDevice
                {
                    SKU = "MT-12127WF-3",
                    FamilyName = "SpectrAlert Advance",
                    ProductName = "Horn Strobe 75cd White",
                    Manufacturer = "System Sensor",
                    DeviceCategory = "HORN_STROBE",
                    StandbyCurrentmA = 0.40,
                    AlarmCurrentmA = 177.0,
                    PowerConsumptionW = 0.4,
                    CandelaRating = 75,
                    HasStrobe = true,
                    HasSpeaker = true,
                    UnitLoads = 1,
                    IsTTapCompatible = true,
                    MountingType = "WALL",
                    EnvironmentalRating = "INDOOR",
                    IsULListed = true
                }
            };
        }
        
        private string DetermineDeviceType(DeviceSnapshot device)
        {
            if (device.HasStrobe && device.HasSpeaker) return "HORN_STROBE";
            if (device.HasStrobe) return "STROBE";
            if (device.HasSpeaker) return "SPEAKER";
            if (device.IsIsolator) return "ISOLATOR";
            if (device.IsRepeater) return "REPEATER";
            return "UNKNOWN";
        }
        
        private string NormalizeName(string name)
        {
            return name?.ToUpper().Replace(" ", "").Replace("-", "").Replace("_", "") ?? "";
        }
    }
    
    /// <summary>
    /// AutoCall 4100ES device catalog entry
    /// </summary>
    public class AutoCallDevice
    {
        public string SKU { get; set; }
        public string AlternateSKU { get; set; }
        public string? FamilyName { get; set; }
        public string ProductName { get; set; }
        public string ModelNumber { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceCategory { get; set; }
        public double StandbyCurrentmA { get; set; }
        public double AlarmCurrentmA { get; set; }
        public double PowerConsumptionW { get; set; }
        public int CandelaRating { get; set; }
        public bool HasStrobe { get; set; }
        public bool HasSpeaker { get; set; }
        public int UnitLoads { get; set; }
        public bool IsTTapCompatible { get; set; }
        public string MountingType { get; set; }
        public string EnvironmentalRating { get; set; }
        public bool IsULListed { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    /// <summary>
    /// Device catalog statistics
    /// </summary>
    public class CatalogStatistics
    {
        public int TotalDevices { get; set; }
        public int Categories { get; set; }
        public int Manufacturers { get; set; }
        public double AverageCurrentDraw { get; set; }
        public bool HasTTapDevices { get; set; }
    }
}