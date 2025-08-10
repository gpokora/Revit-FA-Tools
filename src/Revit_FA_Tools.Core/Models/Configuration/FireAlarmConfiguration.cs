using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Fire alarm system configuration parameters
    /// Replaces hardcoded values throughout the application
    /// </summary>
    public class FireAlarmConfiguration
    {
        public IDNACLimits IDNAC { get; set; } = new IDNACLimits();
        public IDNETLimits IDNET { get; set; } = new IDNETLimits();
        public AmplifierSpecs Amplifiers { get; set; } = new AmplifierSpecs();
        public SystemDefaults Defaults { get; set; } = new SystemDefaults();
        public PowerSupplySpecs PowerSupply { get; set; } = new PowerSupplySpecs();
        public SparePolicy Spare { get; set; } = new SparePolicy();
        public CapacityPolicy Capacity { get; set; } = new CapacityPolicy();
        public RepeaterPolicy Repeater { get; set; } = new RepeaterPolicy();
        public BalancingExclusions Balancing { get; set; } = new BalancingExclusions();
        public DeviceOverrides Overrides { get; set; } = new DeviceOverrides();

        /// <summary>
        /// IDNAC notification system limits and parameters
        /// </summary>
        public class IDNACLimits
        {
            public double MaxCurrent { get; set; } = 3.0; // Maximum IDNAC current (Amps)
            public double MaxCurrentReduced { get; set; } = 2.4; // Reduced current limit (Amps)
            public double NominalVoltage { get; set; } = 24.0; // Fire alarm system voltage
            public int MaxDevicesStandard { get; set; } = 127; // Standard device limit
            public int MaxDevicesReduced { get; set; } = 101; // Reduced device limit
            public double SpareCapacityPercent { get; set; } = 20.0; // Required spare capacity %
            public double TargetUtilizationMin { get; set; } = 60.0; // Minimum target utilization %
            public double TargetUtilizationMax { get; set; } = 80.0; // Maximum target utilization %
            public double VoltageDropLimit { get; set; } = 10.0; // Maximum voltage drop %
        }

        /// <summary>
        /// IDNET detection system limits and parameters
        /// </summary>
        public class IDNETLimits
        {
            public int MaxDevicesPerChannel { get; set; } = 250; // Maximum devices per IDNET channel
            public double MaxWireLength { get; set; } = 12500.0; // Maximum wire run length (feet)
            public int UsableDevicesPerChannel { get; set; } = 200; // Conservative device limit
            public double DefaultSupervisionCurrent { get; set; } = 0.5; // Default supervision current (mA)
            public double AlarmCurrentMultiplier { get; set; } = 2.0; // Alarm current multiplier
            public int RecommendedNetworkChannels { get; set; } = 4; // Default network channels
        }

        /// <summary>
        /// Amplifier system specifications
        /// </summary>
        public class AmplifierSpecs
        {
            public double Flex35Current { get; set; } = 1.8; // Flex-35 current draw (A)
            public double Flex50Current { get; set; } = 2.4; // Flex-50 current draw (A)
            public double Flex100Current { get; set; } = 9.6; // Flex-100 current draw (A)
            public int BlocksPerAmplifier { get; set; } = 1; // Blocks per amplifier
            public double EfficiencyFactor { get; set; } = 0.85; // Amplifier efficiency
        }

        /// <summary>
        /// System default values and behaviors
        /// </summary>
        public class SystemDefaults
        {
            public string DefaultScope { get; set; } = "Active View"; // Default analysis scope
            public string PreferredExportFormat { get; set; } = "CSV"; // Default export format
            public bool AutoSaveEnabled { get; set; } = true; // Auto-save analysis results
            public int DebugLogRetentionDays { get; set; } = 7; // Debug log retention period
            public bool EnableProgressReporting { get; set; } = true; // Show progress during analysis
            public int ProgressUpdateInterval { get; set; } = 100; // Progress update frequency
        }

        /// <summary>
        /// Power supply specifications (ES-PS)
        /// </summary>
        public class PowerSupplySpecs
        {
            public double TotalDCOutput { get; set; } = 9.5; // Total DC output (A)
            public double TotalDCOutputWithFan { get; set; } = 9.7; // With fan and IDNAC modules (A)
            public double MaxOutput { get; set; } = 12.7; // Maximum output with fan only (A)
            public int IDNACCircuitsPerPS { get; set; } = 3; // IDNAC circuits per power supply
            public int AvailableBlocksSingleBay { get; set; } = 4; // Available blocks in single bay
            public int AvailableBlocksTwoBay { get; set; } = 14; // Available blocks in two bay
            public int AvailableBlocksThreeBay { get; set; } = 22; // Available blocks in three bay
        }

        /// <summary>
        /// Spare capacity policy settings
        /// </summary>
        public class SparePolicy
        {
            public double SpareFractionDefault { get; set; } = 0.20;
            public bool EnforceOnCurrent { get; set; } = true;
            public bool EnforceOnUL { get; set; } = true;
            public bool EnforceOnDevices { get; set; } = true;
            public bool EnforceOnPower { get; set; } = true;
        }

        /// <summary>
        /// System capacity limits and thresholds
        /// </summary>
        public class CapacityPolicy
        {
            public double IdnacAlarmCurrentLimitA { get; set; } = 3.0;
            public int IdnacStandbyUnitLoadLimit { get; set; } = 139;
            public int IdnetChannelUnitLoadLimit { get; set; } = 250;
            public double MaxVoltageDropPct { get; set; } = 0.10;
        }

        /// <summary>
        /// Repeater handling policy
        /// </summary>
        public class RepeaterPolicy
        {
            public bool TreatRepeaterAsFreshBudget { get; set; } = true;
            public int RepeaterUnitLoad { get; set; } = 4;
        }

        /// <summary>
        /// Circuit balancing exclusion rules
        /// </summary>
        public class BalancingExclusions
        {
            public List<string> ForbiddenMixGroups { get; set; } = new List<string> { "villa", "garage", "parking" };
        }

        /// <summary>
        /// Device-specific override settings
        /// </summary>
        public class DeviceOverride
        {
            public int UnitLoads { get; set; } = 1;
            public int AddressSlots { get; set; } = 1;
        }

        /// <summary>
        /// Collection of device overrides
        /// </summary>
        public class DeviceOverrides
        {
            public Dictionary<string, DeviceOverride> Overrides { get; set; } = new Dictionary<string, DeviceOverride>();
        }

        /// <summary>
        /// Load configuration from JSON file, create default if not exists
        /// </summary>
        public static FireAlarmConfiguration Load(string? configPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(configPath))
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var configDir = Path.Combine(appDataPath, "Autocall Tools");
                    Directory.CreateDirectory(configDir);
                    configPath = Path.Combine(configDir, "FireAlarmConfig.json");
                }

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<FireAlarmConfiguration>(json);
                    System.Diagnostics.Debug.WriteLine($"Loaded fire alarm configuration from: {configPath}");
                    return config ?? new FireAlarmConfiguration();
                }
                else
                {
                    // Create default configuration file
                    var defaultConfig = new FireAlarmConfiguration();
                    defaultConfig.Save(configPath);
                    System.Diagnostics.Debug.WriteLine($"Created default fire alarm configuration: {configPath}");
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading fire alarm configuration: {ex.Message}");
                return new FireAlarmConfiguration(); // Return default if loading fails
            }
        }

        /// <summary>
        /// Save configuration to JSON file
        /// </summary>
        public void Save(string? configPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(configPath))
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var configDir = Path.Combine(appDataPath, "Autocall Tools");
                    Directory.CreateDirectory(configDir);
                    configPath = Path.Combine(configDir, "FireAlarmConfig.json");
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
                System.Diagnostics.Debug.WriteLine($"Saved fire alarm configuration to: {configPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving fire alarm configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate configuration values and provide warnings for invalid settings
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            // Validate IDNAC limits
            if (IDNAC.MaxCurrent <= 0 || IDNAC.MaxCurrent > 10)
                result.AddWarning("IDNAC MaxCurrent should be between 0.1 and 10.0 Amps");

            if (IDNAC.NominalVoltage < 12 || IDNAC.NominalVoltage > 48)
                result.AddWarning("IDNAC NominalVoltage should be between 12 and 48 Volts");

            if (IDNAC.SpareCapacityPercent < 10 || IDNAC.SpareCapacityPercent > 50)
                result.AddWarning("IDNAC SpareCapacityPercent should be between 10% and 50%");

            // Validate IDNET limits
            if (IDNET.MaxDevicesPerChannel <= 0 || IDNET.MaxDevicesPerChannel > 500)
                result.AddWarning("IDNET MaxDevicesPerChannel should be between 1 and 500");

            if (IDNET.MaxWireLength <= 0 || IDNET.MaxWireLength > 20000)
                result.AddWarning("IDNET MaxWireLength should be between 100 and 20,000 feet");

            // Validate amplifier specs
            if (Amplifiers.Flex35Current <= 0 || Amplifiers.Flex35Current > 5)
                result.AddWarning("Amplifier Flex35Current should be between 0.1 and 5.0 Amps");

            return result;
        }

        public class ValidationResult
        {
            public bool IsValid => Warnings.Count == 0 && Errors.Count == 0;
            public System.Collections.Generic.List<string> Warnings { get; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<string> Errors { get; } = new System.Collections.Generic.List<string>();

            public void AddWarning(string message) => Warnings.Add(message);
            public void AddError(string message) => Errors.Add(message);
        }
    }

    /// <summary>
    /// Configuration service for managing fire alarm configuration
    /// </summary>
    public static class ConfigurationService
    {
        private static FireAlarmConfiguration _current;
        private static readonly object _lock = new object();

        /// <summary>
        /// Get current fire alarm configuration (singleton pattern)
        /// </summary>
        public static FireAlarmConfiguration Current
        {
            get
            {
                if (_current == null)
                {
                    lock (_lock)
                    {
                        if (_current == null)
                        {
                            _current = FireAlarmConfiguration.Load();
                        }
                    }
                }
                return _current;
            }
        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public static void Reload()
        {
            lock (_lock)
            {
                _current = FireAlarmConfiguration.Load();
            }
        }

        /// <summary>
        /// Save current configuration
        /// </summary>
        public static void Save()
        {
            lock (_lock)
            {
                _current?.Save();
            }
        }
    }
}