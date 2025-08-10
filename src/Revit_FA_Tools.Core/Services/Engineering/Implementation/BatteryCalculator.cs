using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Enhanced battery calculation service for fire alarm systems
    /// Implements NFPA 72 battery calculations with unit load methodology
    /// StandbyCurrent = ΣUL * 0.0008A per specification
    /// Enhanced for Autocall 4100ES with separate speaker wattage calculations
    /// </summary>
    public class BatteryCalculator
    {
        /// <summary>
        /// Standard unit load to current conversion factor (0.8 mA per UL)
        /// Per NFPA 72 and fire alarm system specifications
        /// </summary>
        public const double UNIT_LOAD_TO_MILLIAMPS = 0.8;

        /// <summary>
        /// Standard battery standby time requirements (hours)
        /// </summary>
        public const double STANDARD_STANDBY_TIME_HOURS = 24.0;
        
        /// <summary>
        /// Standard alarm operation time (minutes)
        /// </summary>
        public const double STANDARD_ALARM_TIME_MINUTES = 5.0;

        /// <summary>
        /// Standard battery derating factors for temperature and age
        /// </summary>
        public const double BATTERY_DERATING_FACTOR = 0.8; // 80% capacity for aging and temperature

        /// <summary>
        /// Calculate battery requirements for fire alarm system
        /// </summary>
        /// <param name="devices">List of fire alarm devices</param>
        /// <param name="standbyTimeHours">Required standby time in hours (default 24)</param>
        /// <param name="alarmTimeMinutes">Required alarm operation time in minutes (default 5)</param>
        /// <returns>Complete battery calculation result</returns>
        public BatteryCalculationResult CalculateBatteryRequirements(
            List<DeviceSnapshot> devices, 
            double standbyTimeHours = STANDARD_STANDBY_TIME_HOURS, 
            double alarmTimeMinutes = STANDARD_ALARM_TIME_MINUTES)
        {
            try
            {
                if (devices == null || !devices.Any())
                {
                    return new BatteryCalculationResult
                    {
                        IsValid = false,
                        Message = "No devices provided for battery calculation"
                    };
                }

                var result = new BatteryCalculationResult
                {
                    StandbyTimeHours = standbyTimeHours,
                    AlarmTimeMinutes = alarmTimeMinutes,
                    DeviceCount = devices.Count
                };

                // Calculate system loads
                CalculateSystemLoads(devices, result);

                // Calculate battery capacity requirements
                CalculateBatteryCapacity(result);

                // Recommend battery configuration
                RecommendBatteryConfiguration(result);

                result.IsValid = true;
                result.Message = $"Battery calculation complete for {devices.Count} devices";

                return result;
            }
            catch (Exception ex)
            {
                return new BatteryCalculationResult
                {
                    IsValid = false,
                    Message = $"Error calculating battery requirements: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate battery requirements for Autocall 4100ES with enhanced speaker/notification separation
        /// </summary>
        public BatteryCalculationResult CalculateAutocall4100ESRequirements(
            List<DeviceSnapshot> devices,
            double standbyTimeHours = STANDARD_STANDBY_TIME_HOURS,
            double alarmTimeMinutes = STANDARD_ALARM_TIME_MINUTES,
            bool separateSpeakerWattage = true)
        {
            try
            {
                if (devices == null || !devices.Any())
                {
                    return new BatteryCalculationResult
                    {
                        IsValid = false,
                        Message = "No devices provided for Autocall 4100ES battery calculation"
                    };
                }

                var result = new BatteryCalculationResult
                {
                    StandbyTimeHours = standbyTimeHours,
                    AlarmTimeMinutes = alarmTimeMinutes,
                    DeviceCount = devices.Count,
                    SystemType = "Autocall 4100ES"
                };

                // Enhanced load calculation with speaker/notification separation
                CalculateAutocall4100ESLoads(devices, result, separateSpeakerWattage);

                // Calculate battery capacity with 4100ES specific factors
                CalculateAutocall4100ESCapacity(result);

                // Recommend 4100ES compatible battery configurations
                RecommendAutocall4100ESBatteries(result);

                result.IsValid = true;
                result.Message = $"Autocall 4100ES battery calculation complete for {devices.Count} devices";

                return result;
            }
            catch (Exception ex)
            {
                return new BatteryCalculationResult
                {
                    IsValid = false,
                    Message = $"Error calculating Autocall 4100ES battery requirements: {ex.Message}",
                    SystemType = "Autocall 4100ES"
                };
            }
        }

        /// <summary>
        /// Calculate system electrical loads with enhanced speaker/notification separation for 4100ES
        /// </summary>
        private void CalculateAutocall4100ESLoads(List<DeviceSnapshot> devices, BatteryCalculationResult result, bool separateSpeakerWattage)
        {
            // Separate device types using enhanced classification
            var detectionDevices = devices.Where(d => 
                d.DeviceType == "SMOKE_DETECTOR" || 
                d.DeviceType == "HEAT_DETECTOR" || 
                d.DeviceType == "MANUAL_STATION" ||
                d.DeviceType == "MODULE" ||
                d.IsIsolator ||
                (!d.HasStrobe && !d.HasSpeaker && d.DeviceType != "HORN")).ToList();

            var speakerDevices = devices.Where(d => d.HasSpeaker).ToList();
            var strobeDevices = devices.Where(d => d.HasStrobe && !d.HasSpeaker).ToList();
            var comboDevices = devices.Where(d => d.HasStrobe && d.HasSpeaker).ToList();
            var hornDevices = devices.Where(d => d.DeviceType == "HORN" && !d.HasSpeaker && !d.HasStrobe).ToList();

            // Set device counts
            result.DetectionDeviceCount = detectionDevices.Count;
            result.SpeakerDeviceCount = speakerDevices.Count + comboDevices.Count;
            result.StrobeDeviceCount = strobeDevices.Count + comboDevices.Count;
            result.NotificationDeviceCount = speakerDevices.Count + strobeDevices.Count + comboDevices.Count + hornDevices.Count;

            // Calculate IDNET standby current (detection devices)
            result.IdnetStandbyCurrentMA = detectionDevices.Sum(d => d.UnitLoads) * UNIT_LOAD_TO_MILLIAMPS;
            result.IdnetStandbyCurrentA = result.IdnetStandbyCurrentMA / 1000.0;

            // Calculate IDNAC standby current (notification devices - using UL method)
            var notificationDevices = speakerDevices.Concat(strobeDevices).Concat(comboDevices).Concat(hornDevices);
            result.IdnacStandbyCurrentMA = notificationDevices.Sum(d => d.UnitLoads) * UNIT_LOAD_TO_MILLIAMPS;
            result.IdnacStandbyCurrentA = result.IdnacStandbyCurrentMA / 1000.0;

            if (separateSpeakerWattage)
            {
                // Separate speaker wattage for audio/battery calculations
                result.TotalSpeakerWattage = speakerDevices.Sum(d => d.Watts) + comboDevices.Sum(d => d.Watts);
                result.SpeakerStandbyCurrentA = 0.0; // Speakers have 0 amps in standby per classification rules
                
                // Calculate speaker alarm current from wattage (assume 24V system)
                result.SpeakerAlarmCurrentA = result.TotalSpeakerWattage / 24.0; // P = VI, so I = P/V
                
                // Non-speaker notification alarm current (strobes, horns)
                result.NonSpeakerAlarmCurrentA = strobeDevices.Sum(d => d.Amps) + hornDevices.Sum(d => d.Amps);
                
                // Combo devices: strobe current + calculated speaker current from wattage
                result.NonSpeakerAlarmCurrentA += comboDevices.Sum(d => d.Amps); // This includes strobe portion
                
                // Total IDNAC alarm current
                result.IdnacAlarmCurrentA = result.SpeakerAlarmCurrentA + result.NonSpeakerAlarmCurrentA;
            }
            else
            {
                // Traditional method - use device current values directly
                result.IdnacAlarmCurrentA = notificationDevices.Sum(d => d.Amps);
                result.TotalSpeakerWattage = speakerDevices.Sum(d => d.Watts) + comboDevices.Sum(d => d.Watts);
                result.SpeakerAlarmCurrentA = result.IdnacAlarmCurrentA; // Combined value
            }

            // 4100ES specific panel loads
            result.ControlPanelStandbyCurrentA = EstimateAutocall4100ESPanelCurrent(result.DeviceCount, standby: true);
            result.ControlPanelAlarmCurrentA = EstimateAutocall4100ESPanelCurrent(result.DeviceCount, standby: false);

            // Communication and auxiliary loads for 4100ES
            result.CommunicationCurrentA = EstimateAutocall4100ESCommsCurrent();
            
            // Add power supply overhead for 4100ES
            result.PowerSupplyOverheadCurrentA = EstimateAutocall4100ESPSOverhead(result.DeviceCount);

            // IDNET alarm current (detection devices don't change much in alarm)
            result.IdnetAlarmCurrentA = result.IdnetStandbyCurrentA * 1.1; // 10% increase for alarm indication

            // Total system currents
            result.TotalStandbyCurrentA = result.IdnetStandbyCurrentA + result.IdnacStandbyCurrentA + 
                                        result.ControlPanelStandbyCurrentA + result.CommunicationCurrentA + 
                                        result.PowerSupplyOverheadCurrentA;

            result.TotalAlarmCurrentA = result.IdnetAlarmCurrentA + result.IdnacAlarmCurrentA + 
                                      result.ControlPanelAlarmCurrentA + result.CommunicationCurrentA + 
                                      result.PowerSupplyOverheadCurrentA;
        }

        /// <summary>
        /// Estimate Autocall 4100ES control panel current
        /// </summary>
        private double EstimateAutocall4100ESPanelCurrent(int deviceCount, bool standby)
        {
            // 4100ES base current (from specifications)
            var baseCurrent = standby ? 0.250 : 0.400; // Higher base current for 4100ES
            
            // Additional current based on system size
            var deviceFactor = (deviceCount / 50) * (standby ? 0.025 : 0.050); // 25/50mA per 50 devices
            
            // 4100ES specific features (network display, advanced processing)
            var featureCurrent = standby ? 0.100 : 0.150; // Additional for 4100ES features
            
            return baseCurrent + deviceFactor + featureCurrent;
        }

        /// <summary>
        /// Estimate Autocall 4100ES communication current
        /// </summary>
        private double EstimateAutocall4100ESCommsCurrent()
        {
            // 4100ES typically includes network communications, dialer, etc.
            return 0.150; // 150mA for enhanced communication features
        }

        /// <summary>
        /// Estimate Autocall 4100ES power supply overhead
        /// </summary>
        private double EstimateAutocall4100ESPSOverhead(int deviceCount)
        {
            // Power supply efficiency and regulation overhead
            var baseOverhead = 0.050; // 50mA base overhead
            var deviceOverhead = (deviceCount / 100) * 0.025; // 25mA per 100 devices
            
            return baseOverhead + deviceOverhead;
        }

        /// <summary>
        /// Calculate battery capacity with Autocall 4100ES specific factors
        /// </summary>
        private void CalculateAutocall4100ESCapacity(BatteryCalculationResult result)
        {
            // Standard capacity calculation
            CalculateBatteryCapacity(result);
            
            // 4100ES specific derating factors (more conservative)
            result.Autocall4100ESDerating = 0.75; // 75% capacity (more conservative than standard)
            result.DeratedCapacityAH = result.TotalCapacityRequiredAH / result.Autocall4100ESDerating;
            
            // Enhanced safety margin for 4100ES
            result.SafetyMargin = 0.15; // 15% safety margin (higher than standard)
            result.FinalRequiredCapacityAH = result.DeratedCapacityAH * (1.0 + result.SafetyMargin);
        }

        /// <summary>
        /// Recommend Autocall 4100ES compatible battery configurations
        /// </summary>
        private void RecommendAutocall4100ESBatteries(BatteryCalculationResult result)
        {
            result.BatteryRecommendations = new List<BatteryRecommendation>();

            // 4100ES compatible battery capacities (based on cabinet space and connections)
            var autocall4100ESCapacities = new[] { 12, 18, 26, 33, 55, 75, 100 };

            foreach (var capacity in autocall4100ESCapacities)
            {
                if (capacity >= result.FinalRequiredCapacityAH)
                {
                    var recommendation = new BatteryRecommendation
                    {
                        Configuration = $"Autocall 4100ES: {capacity}AH @ 24V",
                        BatteryCount = 2, // 4100ES typically uses 24V (2x12V in series)
                        TotalCapacity = capacity,
                        ExcessCapacity = capacity - result.FinalRequiredCapacityAH,
                        ExcessCapacityPercent = ((capacity - result.FinalRequiredCapacityAH) / result.FinalRequiredCapacityAH) * 100,
                        EstimatedCost = EstimateAutocall4100ESBatteryCost(capacity),
                        PhysicalDimensions = GetAutocall4100ESBatteryDimensions(capacity),
                        Weight = GetBatteryWeight(capacity) * 2, // Two batteries
                        IsRecommended = result.BatteryRecommendations.Count == 0,
                        Notes = "Compatible with Autocall 4100ES battery cabinet",
                        SystemType = "Autocall 4100ES"
                    };
                    result.BatteryRecommendations.Add(recommendation);

                    // Stop after first few recommendations to avoid overwhelming options
                    if (result.BatteryRecommendations.Count >= 3) break;
                }
            }

            // If no single battery works, recommend high-capacity parallel configuration
            if (!result.BatteryRecommendations.Any())
            {
                var largestCapacity = autocall4100ESCapacities.Last();
                var batterySetsNeeded = (int)Math.Ceiling(result.FinalRequiredCapacityAH / largestCapacity);

                result.BatteryRecommendations.Add(new BatteryRecommendation
                {
                    Configuration = $"Autocall 4100ES: {batterySetsNeeded}x{largestCapacity}AH @ 24V (parallel sets)",
                    BatteryCount = batterySetsNeeded * 2, // Each set is 2 batteries in series
                    TotalCapacity = largestCapacity * batterySetsNeeded,
                    ExcessCapacity = (largestCapacity * batterySetsNeeded) - result.FinalRequiredCapacityAH,
                    ExcessCapacityPercent = (((largestCapacity * batterySetsNeeded) - result.FinalRequiredCapacityAH) / result.FinalRequiredCapacityAH) * 100,
                    EstimatedCost = EstimateAutocall4100ESBatteryCost(largestCapacity) * batterySetsNeeded,
                    PhysicalDimensions = GetAutocall4100ESBatteryDimensions(largestCapacity, batterySetsNeeded),
                    Weight = GetBatteryWeight(largestCapacity) * 2 * batterySetsNeeded,
                    IsRecommended = true,
                    Notes = "High-capacity parallel configuration - may require expanded battery cabinet",
                    SystemType = "Autocall 4100ES"
                });
            }

            // Set preferred configuration
            var preferred = result.BatteryRecommendations.FirstOrDefault(r => r.IsRecommended);
            if (preferred != null)
            {
                result.PreferredConfiguration = preferred.Configuration;
                result.EstimatedBatteryCost = preferred.EstimatedCost;
            }
        }

        /// <summary>
        /// Estimate battery cost for Autocall 4100ES (includes installation)
        /// </summary>
        private double EstimateAutocall4100ESBatteryCost(double capacityAH)
        {
            // 4100ES batteries are typically higher quality with installation
            var baseCost = capacityAH * 8.0; // $8 per AH (higher than standard)
            var installationCost = 150.0; // Installation and connection cost
            
            return (baseCost * 2) + installationCost; // Two batteries plus installation
        }

        /// <summary>
        /// Get Autocall 4100ES specific battery dimensions
        /// </summary>
        private string GetAutocall4100ESBatteryDimensions(double capacityAH, int sets = 1)
        {
            var baseDimension = GetBatteryDimensions(capacityAH);
            var cabinetInfo = capacityAH switch
            {
                <= 26 => "fits standard 4100ES battery cabinet",
                <= 55 => "fits expanded 4100ES battery cabinet",
                _ => "requires external battery cabinet"
            };

            if (sets > 1)
            {
                return $"{sets} sets of 2x{baseDimension} ({cabinetInfo})";
            }

            return $"2x{baseDimension} in series ({cabinetInfo})";
        }

        /// <summary>
        /// Calculate system electrical loads
        /// </summary>
        private void CalculateSystemLoads(List<DeviceSnapshot> devices, BatteryCalculationResult result)
        {
            // Separate detection and notification devices
            var detectionDevices = devices.Where(d => 
                d.DeviceType == "SMOKE_DETECTOR" || 
                d.DeviceType == "HEAT_DETECTOR" || 
                d.DeviceType == "MANUAL_STATION" ||
                d.DeviceType == "MODULE" ||
                d.IsIsolator ||
                (!d.HasStrobe && !d.HasSpeaker && d.DeviceType != "HORN")).ToList();

            var notificationDevices = devices.Where(d => 
                d.HasStrobe || d.HasSpeaker || d.DeviceType == "HORN").ToList();

            // Calculate standby currents using unit load methodology
            result.DetectionDeviceCount = detectionDevices.Count;
            result.NotificationDeviceCount = notificationDevices.Count;

            // IDNET (Detection) standby current: ΣUL * 0.0008A
            result.IdnetStandbyCurrentMA = detectionDevices.Sum(d => d.UnitLoads) * UNIT_LOAD_TO_MILLIAMPS;
            result.IdnetStandbyCurrentA = result.IdnetStandbyCurrentMA / 1000.0;

            // IDNAC (Notification) standby current: ΣUL * 0.0008A  
            result.IdnacStandbyCurrentMA = notificationDevices.Sum(d => d.UnitLoads) * UNIT_LOAD_TO_MILLIAMPS;
            result.IdnacStandbyCurrentA = result.IdnacStandbyCurrentMA / 1000.0;

            // Panel/control unit standby current (estimated)
            result.ControlPanelStandbyCurrentA = EstimateControlPanelCurrent(result.DeviceCount);

            // Total system standby current
            result.TotalStandbyCurrentA = result.IdnetStandbyCurrentA + result.IdnacStandbyCurrentA + result.ControlPanelStandbyCurrentA;

            // Alarm currents (notification devices at full load)
            result.IdnacAlarmCurrentA = notificationDevices.Sum(d => d.Amps);
            result.IdnetAlarmCurrentA = result.IdnetStandbyCurrentA; // Detection devices don't increase significantly in alarm

            // Add control panel alarm current (typically higher due to display, communications)
            result.ControlPanelAlarmCurrentA = result.ControlPanelStandbyCurrentA * 1.5; // 50% increase in alarm

            // Total system alarm current
            result.TotalAlarmCurrentA = result.IdnetAlarmCurrentA + result.IdnacAlarmCurrentA + result.ControlPanelAlarmCurrentA;

            // Additional system loads (communications, etc.)
            result.CommunicationCurrentA = EstimateCommunicationCurrent();
            result.TotalStandbyCurrentA += result.CommunicationCurrentA;
            result.TotalAlarmCurrentA += result.CommunicationCurrentA;
        }

        /// <summary>
        /// Calculate required battery capacity
        /// </summary>
        private void CalculateBatteryCapacity(BatteryCalculationResult result)
        {
            // Standby capacity requirement (24 hours typically)
            result.StandbyCapacityAH = result.TotalStandbyCurrentA * result.StandbyTimeHours;

            // Alarm capacity requirement (5 minutes typically)
            var alarmTimeHours = result.AlarmTimeMinutes / 60.0;
            result.AlarmCapacityAH = result.TotalAlarmCurrentA * alarmTimeHours;

            // Total theoretical capacity
            result.TotalCapacityRequiredAH = result.StandbyCapacityAH + result.AlarmCapacityAH;

            // Apply derating factor for aging and temperature
            result.DeratedCapacityAH = result.TotalCapacityRequiredAH / BATTERY_DERATING_FACTOR;

            // Add safety margin (10% additional)
            result.SafetyMargin = 0.10;
            result.FinalRequiredCapacityAH = result.DeratedCapacityAH * (1.0 + result.SafetyMargin);
        }

        /// <summary>
        /// Recommend battery configuration based on requirements
        /// </summary>
        private void RecommendBatteryConfiguration(BatteryCalculationResult result)
        {
            result.BatteryRecommendations = new List<BatteryRecommendation>();

            // Standard 12V battery capacities available
            var standardCapacities = new[] { 7, 12, 18, 26, 33, 55, 75, 100 };

            foreach (var capacity in standardCapacities)
            {
                if (capacity >= result.FinalRequiredCapacityAH)
                {
                    var recommendation = new BatteryRecommendation
                    {
                        Configuration = $"{capacity}AH @ 12V",
                        BatteryCount = 1,
                        TotalCapacity = capacity,
                        ExcessCapacity = capacity - result.FinalRequiredCapacityAH,
                        ExcessCapacityPercent = ((capacity - result.FinalRequiredCapacityAH) / result.FinalRequiredCapacityAH) * 100,
                        EstimatedCost = EstimateBatteryCost(capacity, 1),
                        PhysicalDimensions = GetBatteryDimensions(capacity),
                        Weight = GetBatteryWeight(capacity),
                        IsRecommended = result.BatteryRecommendations.Count == 0 // First match is recommended
                    };
                    result.BatteryRecommendations.Add(recommendation);

                    // Also consider 24V option (two batteries)
                    if (capacity <= 55) // Practical limit for dual battery configurations
                    {
                        var recommendation24V = new BatteryRecommendation
                        {
                            Configuration = $"{capacity}AH @ 24V (2x{capacity}AH in series)",
                            BatteryCount = 2,
                            TotalCapacity = capacity, // Same AH capacity, higher voltage
                            ExcessCapacity = capacity - result.FinalRequiredCapacityAH,
                            ExcessCapacityPercent = ((capacity - result.FinalRequiredCapacityAH) / result.FinalRequiredCapacityAH) * 100,
                            EstimatedCost = EstimateBatteryCost(capacity, 2),
                            PhysicalDimensions = GetBatteryDimensions(capacity, true),
                            Weight = GetBatteryWeight(capacity) * 2,
                            IsRecommended = false,
                            Notes = "Higher voltage system - may improve efficiency"
                        };
                        result.BatteryRecommendations.Add(recommendation24V);
                    }
                }
            }

            // If no single battery meets requirements, recommend parallel configuration
            if (!result.BatteryRecommendations.Any())
            {
                var largestBattery = standardCapacities.Last();
                var batteriesNeeded = (int)Math.Ceiling(result.FinalRequiredCapacityAH / largestBattery);

                result.BatteryRecommendations.Add(new BatteryRecommendation
                {
                    Configuration = $"{batteriesNeeded}x{largestBattery}AH @ 12V (parallel)",
                    BatteryCount = batteriesNeeded,
                    TotalCapacity = largestBattery * batteriesNeeded,
                    ExcessCapacity = (largestBattery * batteriesNeeded) - result.FinalRequiredCapacityAH,
                    ExcessCapacityPercent = (((largestBattery * batteriesNeeded) - result.FinalRequiredCapacityAH) / result.FinalRequiredCapacityAH) * 100,
                    EstimatedCost = EstimateBatteryCost(largestBattery, batteriesNeeded),
                    PhysicalDimensions = GetBatteryDimensions(largestBattery, false, batteriesNeeded),
                    Weight = GetBatteryWeight(largestBattery) * batteriesNeeded,
                    IsRecommended = true,
                    Notes = "Parallel configuration for high capacity requirements"
                });
            }

            // Set preferred recommendation
            var preferred = result.BatteryRecommendations.FirstOrDefault(r => r.IsRecommended);
            if (preferred != null)
            {
                result.PreferredConfiguration = preferred.Configuration;
                result.EstimatedBatteryCost = preferred.EstimatedCost;
            }
        }

        /// <summary>
        /// Estimate control panel standby current based on system size
        /// </summary>
        private double EstimateControlPanelCurrent(int deviceCount)
        {
            // Base panel current plus additional current for larger systems
            var baseCurrent = 0.150; // 150mA base
            var additionalCurrent = (deviceCount / 100) * 0.050; // 50mA per 100 devices
            
            return baseCurrent + additionalCurrent;
        }

        /// <summary>
        /// Estimate communication module current (network, cellular, etc.)
        /// </summary>
        private double EstimateCommunicationCurrent()
        {
            return 0.100; // 100mA typical for communication modules
        }

        /// <summary>
        /// Estimate battery cost based on capacity and quantity
        /// </summary>
        private double EstimateBatteryCost(double capacityAH, int quantity)
        {
            // Rough cost estimation (actual costs vary by manufacturer and features)
            var baseCost = capacityAH * 5.0; // $5 per AH rough estimate
            return baseCost * quantity;
        }

        /// <summary>
        /// Get estimated battery physical dimensions
        /// </summary>
        private string GetBatteryDimensions(double capacityAH, bool isSeries = false, int parallelCount = 1)
        {
            // Simplified dimension estimates for standard batteries
            string baseDimension = capacityAH switch
            {
                <= 7 => "6\"×3\"×4\"",
                <= 12 => "6\"×4\"×4\"",
                <= 18 => "7\"×3\"×6\"",
                <= 26 => "7\"×3\"×7\"",
                <= 33 => "8\"×6\"×7\"",
                <= 55 => "9\"×5\"×8\"",
                <= 75 => "12\"×6\"×9\"",
                _ => "13\"×7\"×10\""
            };

            if (isSeries)
            {
                return $"2x {baseDimension} (series)";
            }
            else if (parallelCount > 1)
            {
                return $"{parallelCount}x {baseDimension} (parallel)";
            }

            return baseDimension;
        }

        /// <summary>
        /// Get estimated battery weight
        /// </summary>
        private double GetBatteryWeight(double capacityAH)
        {
            // Rough weight estimation for sealed lead-acid batteries (lbs)
            return capacityAH * 1.5 + 5; // ~1.5 lbs per AH plus base weight
        }
    }

    /// <summary>
    /// Complete battery calculation result
    /// </summary>
    public class BatteryCalculationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;

        // System parameters
        public int DeviceCount { get; set; }
        public int DetectionDeviceCount { get; set; }
        public int NotificationDeviceCount { get; set; }
        public int SpeakerDeviceCount { get; set; }
        public int StrobeDeviceCount { get; set; }
        public double StandbyTimeHours { get; set; }
        public double AlarmTimeMinutes { get; set; }
        public string SystemType { get; set; } = "Standard";

        // Standby currents (using unit load methodology)
        public double IdnetStandbyCurrentMA { get; set; } // Detection system (mA)
        public double IdnetStandbyCurrentA { get; set; }   // Detection system (A)
        public double IdnacStandbyCurrentMA { get; set; } // Notification system (mA) 
        public double IdnacStandbyCurrentA { get; set; }   // Notification system (A)
        public double ControlPanelStandbyCurrentA { get; set; }
        public double CommunicationCurrentA { get; set; }
        public double TotalStandbyCurrentA { get; set; }

        // Alarm currents
        public double IdnetAlarmCurrentA { get; set; }
        public double IdnacAlarmCurrentA { get; set; }
        public double ControlPanelAlarmCurrentA { get; set; }
        public double TotalAlarmCurrentA { get; set; }

        // Enhanced speaker/notification separation
        public double TotalSpeakerWattage { get; set; }
        public double SpeakerStandbyCurrentA { get; set; }
        public double SpeakerAlarmCurrentA { get; set; }
        public double NonSpeakerAlarmCurrentA { get; set; }
        public double PowerSupplyOverheadCurrentA { get; set; }
        public double Autocall4100ESDerating { get; set; } = 0.80;

        // Capacity calculations
        public double StandbyCapacityAH { get; set; }
        public double AlarmCapacityAH { get; set; }
        public double TotalCapacityRequiredAH { get; set; }
        public double DeratedCapacityAH { get; set; }
        public double SafetyMargin { get; set; }
        public double FinalRequiredCapacityAH { get; set; }

        // Recommendations
        public List<BatteryRecommendation> BatteryRecommendations { get; set; } = new List<BatteryRecommendation>();
        public string PreferredConfiguration { get; set; } = string.Empty;
        public double EstimatedBatteryCost { get; set; }
    }

    /// <summary>
    /// Individual battery configuration recommendation
    /// </summary>
    public class BatteryRecommendation
    {
        public string Configuration { get; set; } = string.Empty;
        public int BatteryCount { get; set; }
        public double TotalCapacity { get; set; }
        public double ExcessCapacity { get; set; }
        public double ExcessCapacityPercent { get; set; }
        public double EstimatedCost { get; set; }
        public string PhysicalDimensions { get; set; } = string.Empty;
        public double Weight { get; set; }
        public bool IsRecommended { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string SystemType { get; set; } = "Standard";

        public override string ToString()
        {
            return $"{Configuration} - {TotalCapacity:F1}AH ({ExcessCapacityPercent:F1}% excess) - ${EstimatedCost:F0}";
        }
    }
}