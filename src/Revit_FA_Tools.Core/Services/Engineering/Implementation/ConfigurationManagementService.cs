using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class ConfigurationManagementService
    {
        private readonly string _configDirectory;
        private readonly Dictionary<string, DeviceProfile> _deviceProfiles;
        private SystemConfiguration _systemConfig;
        
        public ConfigurationManagementService(string configDirectory = null)
        {
            _configDirectory = configDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IDNAC Calculator", "Configuration");
            
            Directory.CreateDirectory(_configDirectory);
            
            _deviceProfiles = LoadDeviceProfiles();
            _systemConfig = LoadSystemConfiguration();
        }
        
        public SystemConfiguration GetSystemConfiguration()
        {
            return _systemConfig;
        }
        
        public void UpdateSystemConfiguration(SystemConfiguration config)
        {
            _systemConfig = config;
            SaveSystemConfiguration();
        }
        
        public void UpdateSpareCapacityPercent(double percent)
        {
            _systemConfig.SpareCapacityPercent = Math.Max(10, Math.Min(50, percent));
            SaveSystemConfiguration();
        }
        
        public Dictionary<string, DeviceProfile> GetDeviceProfiles()
        {
            return new Dictionary<string, DeviceProfile>(_deviceProfiles);
        }
        
        public void AddDeviceProfile(string key, DeviceProfile profile)
        {
            _deviceProfiles[key] = profile;
            SaveDeviceProfiles();
        }
        
        public void RemoveDeviceProfile(string key)
        {
            if (_deviceProfiles.ContainsKey(key))
            {
                _deviceProfiles.Remove(key);
                SaveDeviceProfiles();
            }
        }
        
        public DeviceProfile GetDeviceProfile(string familyName, string typeName = null)
        {
            var searchKeys = new List<string>();
            
            if (!string.IsNullOrEmpty(typeName))
            {
                searchKeys.Add($"{familyName}|{typeName}".ToUpper());
            }
            searchKeys.Add(familyName?.ToUpper());
            
            foreach (var key in searchKeys)
            {
                if (_deviceProfiles.ContainsKey(key))
                {
                    return _deviceProfiles[key];
                }
                
                var matchingKey = _deviceProfiles.Keys
                    .FirstOrDefault(k => k.Contains(key) || key.Contains(k));
                
                if (matchingKey != null)
                {
                    return _deviceProfiles[matchingKey];
                }
            }
            
            return GetDefaultDeviceProfile(familyName);
        }
        
        private DeviceProfile GetDefaultDeviceProfile(string familyName)
        {
            var familyUpper = familyName?.ToUpper() ?? "";
            
            if (familyUpper.Contains("SMOKE") || familyUpper.Contains("HEAT"))
            {
                return new DeviceProfile
                {
                    Name = "Default Detector",
                    AlarmCurrent = 0.045,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "DETECTION"
                };
            }
            else if (familyUpper.Contains("STROBE") || familyUpper.Contains("HORN") || familyUpper.Contains("SPEAKER"))
            {
                return new DeviceProfile
                {
                    Name = "Default Notification",
                    AlarmCurrent = 0.177,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "NOTIFICATION"
                };
            }
            else if (familyUpper.Contains("MODULE") || familyUpper.Contains("INPUT") || familyUpper.Contains("OUTPUT"))
            {
                return new DeviceProfile
                {
                    Name = "Default Module",
                    AlarmCurrent = 0.025,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "MODULE"
                };
            }
            
            return new DeviceProfile
            {
                Name = "Default Device",
                AlarmCurrent = 0.050,
                StandbyCurrent = 0.0005,
                UnitLoads = 1,
                DeviceType = "UNKNOWN"
            };
        }
        
        public List<DeviceSnapshot> ApplyDeviceProfiles(List<DeviceSnapshot> devices)
        {
            var updatedDevices = new List<DeviceSnapshot>();
            
            foreach (var device in devices)
            {
                if (!device.HasOverride)
                {
                    var profile = GetDeviceProfile(device.FamilyName, device.TypeName);
                    var updatedDevice = ApplyProfileToDevice(device, profile);
                    updatedDevices.Add(updatedDevice);
                }
                else
                {
                    updatedDevices.Add(device);
                }
            }
            
            return updatedDevices;
        }
        
        private DeviceSnapshot ApplyProfileToDevice(DeviceSnapshot device, DeviceProfile profile)
        {
            if (profile == null)
            {
                return device;
            }
            
            // Create a new DeviceSnapshot with updated values
            var updatedProperties = new Dictionary<string, object>(device.ActualCustomProperties);
            updatedProperties["DeviceType"] = profile.DeviceType;
            updatedProperties["ProfileApplied"] = profile.Name;
            
            return device with
            {
                Amps = profile.AlarmCurrent,
                StandbyCurrent = profile.StandbyCurrent,
                UnitLoads = profile.UnitLoads,
                CustomProperties = updatedProperties
            };
        }
        
        public ConfigurationValidationResult ValidateConfiguration()
        {
            var result = new ConfigurationValidationResult();
            
            if (_systemConfig.SpareCapacityPercent < 10 || _systemConfig.SpareCapacityPercent > 50)
            {
                result.AddWarning("Spare capacity percentage should be between 10% and 50%");
            }
            
            if (_systemConfig.IDNACAlarmCurrentLimit <= 0 || _systemConfig.IDNACAlarmCurrentLimit > 5)
            {
                result.AddError("IDNAC alarm current limit should be between 0.1A and 5.0A");
            }
            
            if (_systemConfig.IDNACUnitLoadLimit <= 0 || _systemConfig.IDNACUnitLoadLimit > 200)
            {
                result.AddError("IDNAC unit load limit should be between 1 and 200");
            }
            
            if (_systemConfig.VoltageDropLimitPercent <= 0 || _systemConfig.VoltageDropLimitPercent > 20)
            {
                result.AddWarning("Voltage drop limit should be between 1% and 20%");
            }
            
            var invalidProfiles = _deviceProfiles.Values.Where(p => 
                p.AlarmCurrent < 0 || p.AlarmCurrent > 5 ||
                p.StandbyCurrent < 0 || p.StandbyCurrent > 1 ||
                p.UnitLoads < 1 || p.UnitLoads > 10).ToList();
            
            foreach (var profile in invalidProfiles)
            {
                result.AddWarning($"Device profile '{profile.Name}' has invalid parameters");
            }
            
            return result;
        }
        
        public void ResetToDefaults()
        {
            _systemConfig = CreateDefaultSystemConfiguration();
            _deviceProfiles.Clear();
            
            foreach (var profile in CreateDefaultDeviceProfiles())
            {
                _deviceProfiles[profile.Key] = profile.Value;
            }
            
            SaveSystemConfiguration();
            SaveDeviceProfiles();
        }
        
        public void ExportConfiguration(string filePath)
        {
            var exportData = new ConfigurationExport
            {
                SystemConfiguration = _systemConfig,
                DeviceProfiles = _deviceProfiles,
                ExportedAt = DateTime.Now,
                Version = "1.0"
            };
            
            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        public void ImportConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            
            var json = File.ReadAllText(filePath);
            var importData = JsonConvert.DeserializeObject<ConfigurationExport>(json);
            
            if (importData.SystemConfiguration != null)
            {
                _systemConfig = importData.SystemConfiguration;
                SaveSystemConfiguration();
            }
            
            if (importData.DeviceProfiles != null)
            {
                _deviceProfiles.Clear();
                foreach (var profile in importData.DeviceProfiles)
                {
                    _deviceProfiles[profile.Key] = profile.Value;
                }
                SaveDeviceProfiles();
            }
        }
        
        private SystemConfiguration LoadSystemConfiguration()
        {
            var configPath = Path.Combine(_configDirectory, "system.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<SystemConfiguration>(json) 
                           ?? CreateDefaultSystemConfiguration();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading system configuration: {ex.Message}");
                }
            }
            
            return CreateDefaultSystemConfiguration();
        }
        
        private void SaveSystemConfiguration()
        {
            var configPath = Path.Combine(_configDirectory, "system.json");
            var json = JsonConvert.SerializeObject(_systemConfig, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }
        
        private Dictionary<string, DeviceProfile> LoadDeviceProfiles()
        {
            var profilesPath = Path.Combine(_configDirectory, "device_profiles.json");
            
            if (File.Exists(profilesPath))
            {
                try
                {
                    var json = File.ReadAllText(profilesPath);
                    return JsonConvert.DeserializeObject<Dictionary<string, DeviceProfile>>(json)
                           ?? CreateDefaultDeviceProfiles();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading device profiles: {ex.Message}");
                }
            }
            
            return CreateDefaultDeviceProfiles();
        }
        
        private void SaveDeviceProfiles()
        {
            var profilesPath = Path.Combine(_configDirectory, "device_profiles.json");
            var json = JsonConvert.SerializeObject(_deviceProfiles, Formatting.Indented);
            File.WriteAllText(profilesPath, json);
        }
        
        private SystemConfiguration CreateDefaultSystemConfiguration()
        {
            return new SystemConfiguration
            {
                SpareCapacityPercent = 20.0,
                IDNACAlarmCurrentLimit = 3.0,
                IDNACStandbyCurrentLimit = 3.0,
                IDNACUnitLoadLimit = 139,
                IDNACDeviceLimit = 127,
                VoltageDropLimitPercent = 10.0,
                NominalVoltage = 24.0,
                ESPSCapacity = 9.5,
                MaxBranchesPerPS = 3,
                EnableAutoBalancing = true,
                ExcludeVillaLevels = true,
                ExcludeGarageLevels = true,
                TargetUtilizationPercent = 75.0
            };
        }
        
        private Dictionary<string, DeviceProfile> CreateDefaultDeviceProfiles()
        {
            return new Dictionary<string, DeviceProfile>
            {
                ["SMOKE DETECTOR"] = new DeviceProfile
                {
                    Name = "Smoke Detector",
                    AlarmCurrent = 0.045,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "DETECTION",
                    Description = "Standard photoelectric smoke detector"
                },
                ["HEAT DETECTOR"] = new DeviceProfile
                {
                    Name = "Heat Detector",
                    AlarmCurrent = 0.025,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "DETECTION",
                    Description = "Fixed temperature heat detector"
                },
                ["HORN STROBE"] = new DeviceProfile
                {
                    Name = "Horn/Strobe",
                    AlarmCurrent = 0.177,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "NOTIFICATION",
                    Description = "Combined audible/visual notification device"
                },
                ["SPEAKER"] = new DeviceProfile
                {
                    Name = "Speaker",
                    AlarmCurrent = 0.094,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "NOTIFICATION",
                    Description = "Voice evacuation speaker"
                },
                ["MANUAL STATION"] = new DeviceProfile
                {
                    Name = "Manual Pull Station",
                    AlarmCurrent = 0.025,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "INITIATION",
                    Description = "Manual fire alarm pull station"
                },
                ["INPUT MODULE"] = new DeviceProfile
                {
                    Name = "Input Module",
                    AlarmCurrent = 0.025,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "MODULE",
                    Description = "Supervised input module"
                },
                ["RELAY MODULE"] = new DeviceProfile
                {
                    Name = "Control Relay Module",
                    AlarmCurrent = 0.050,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 1,
                    DeviceType = "MODULE",
                    Description = "Control relay output module"
                },
                ["ISOLATOR"] = new DeviceProfile
                {
                    Name = "Short Circuit Isolator",
                    AlarmCurrent = 0.025,
                    StandbyCurrent = 0.0005,
                    UnitLoads = 4,
                    DeviceType = "PROTECTION",
                    Description = "Short circuit isolator"
                }
            };
        }
    }
    
    public class SystemConfiguration
    {
        public double SpareCapacityPercent { get; set; } = 20.0;
        public double IDNACAlarmCurrentLimit { get; set; } = 3.0;
        public double IDNACStandbyCurrentLimit { get; set; } = 3.0;
        public int IDNACUnitLoadLimit { get; set; } = 139;
        public int IDNACDeviceLimit { get; set; } = 127;
        public double VoltageDropLimitPercent { get; set; } = 10.0;
        public double NominalVoltage { get; set; } = 24.0;
        public double ESPSCapacity { get; set; } = 9.5;
        public int MaxBranchesPerPS { get; set; } = 3;
        public bool EnableAutoBalancing { get; set; } = true;
        public bool ExcludeVillaLevels { get; set; } = true;
        public bool ExcludeGarageLevels { get; set; } = true;
        public double TargetUtilizationPercent { get; set; } = 75.0;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
    
    public class DeviceProfile
    {
        public string Name { get; set; }
        public double AlarmCurrent { get; set; }
        public double StandbyCurrent { get; set; }
        public int UnitLoads { get; set; } = 1;
        public string DeviceType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }
    
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        
        public void AddError(string message)
        {
            IsValid = false;
            Errors.Add(message);
        }
        
        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }
    }
    
    public class ConfigurationExport
    {
        public SystemConfiguration SystemConfiguration { get; set; }
        public Dictionary<string, DeviceProfile> DeviceProfiles { get; set; }
        public DateTime ExportedAt { get; set; }
        public string Version { get; set; }
    }
}