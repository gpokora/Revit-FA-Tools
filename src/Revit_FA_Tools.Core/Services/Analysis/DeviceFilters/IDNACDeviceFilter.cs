using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Interfaces.Analysis;

namespace Revit_FA_Tools.Core.Services.Analysis.DeviceFilters
{
    /// <summary>
    /// Filter for IDNAC (Intelligent Notification Appliance Circuit) devices
    /// </summary>
    public class IDNACDeviceFilter : IDeviceFilter
    {
        private readonly object _logger;

        /// <summary>
        /// Keywords that identify notification devices
        /// </summary>
        private static readonly string[] NotificationKeywords = {
            "SPEAKER", "HORN", "STROBE", "BELL", "CHIME", "SOUNDER",
            "NOTIFICATION", "NAC", "APPLIANCE", "AUDIBLE", "VISUAL",
            "IDNAC", "HORN/STROBE", "SPEAKER/STROBE"
        };

        /// <summary>
        /// Keywords that identify detection devices to exclude
        /// </summary>
        private static readonly string[] ExcludedDetectionKeywords = {
            "DETECTORS", "DETECTOR", "MODULE", "PULL", "STATION", "MANUAL",
            "MONITOR", "INPUT", "OUTPUT", "SENSOR", "SENSING", "BEAM",
            "HEAT", "SMOKE", "THERMAL", "PHOTOELECTRIC", "IONIZATION"
        };

        /// <summary>
        /// Electrical parameters to check for notification devices
        /// </summary>
        private static readonly string[] ElectricalParameters = {
            "CURRENT DRAW", "Wattage", "Current", "Power", "Watts"
        };

        public CircuitType SupportedCircuitType => CircuitType.IDNAC;

        public IDNACDeviceFilter(object logger = null)
        {
            _logger = logger;
        }

        public async Task<List<FamilyInstance>> FilterDevicesAsync(List<FamilyInstance> allDevices)
        {
            if (allDevices == null || allDevices.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("IDNAC Filter: No devices to filter");
                return new List<FamilyInstance>();
            }

            System.Diagnostics.Debug.WriteLine($"IDNAC Filter: Processing {allDevices.Count} candidate devices");

            var filteredDevices = new List<FamilyInstance>();
            var excludedCount = 0;
            var notificationDeviceCount = 0;

            foreach (var device in allDevices)
            {
                var filterResult = GetFilterReason(device);
                
                if (filterResult.IsIncluded)
                {
                    filteredDevices.Add(device);
                    notificationDeviceCount++;
                    
                    System.Diagnostics.Debug.WriteLine($"IDNAC: Included '{filterResult.FamilyName}' - {filterResult.Reason}");
                }
                else
                {
                    excludedCount++;
                    System.Diagnostics.Debug.WriteLine($"IDNAC: Excluded '{filterResult.FamilyName}' - {filterResult.Reason}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"IDNAC Filter Complete: {notificationDeviceCount} notification devices found, {excludedCount} excluded");

            // Log device type breakdown for debugging
            var typeBreakdown = filteredDevices
                .GroupBy(d => GetDeviceType(d))
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            
            if (typeBreakdown.Any())
            {
                System.Diagnostics.Debug.WriteLine($"IDNAC Device Types: {string.Join(", ", typeBreakdown)}");
            }

            return await Task.FromResult(filteredDevices);
        }

        public bool IsDeviceSupported(FamilyInstance device)
        {
            return GetFilterReason(device).IsIncluded;
        }

        public DeviceFilterResult GetFilterReason(FamilyInstance device)
        {
            if (device?.Symbol?.Family == null)
            {
                return DeviceFilterResult.Excluded("Invalid device or missing family information");
            }

            try
            {
                var familyName = device.Symbol.Family.Name;
                var typeName = device.Symbol.Name;
                var categoryName = device.Category?.Name ?? "";
                
                var familyUpper = familyName.ToUpperInvariant();
                var typeUpper = typeName.ToUpperInvariant();
                var categoryUpper = categoryName.ToUpperInvariant();
                var combined = $"{familyUpper} {typeUpper}";

                // First check: Explicitly exclude detection devices
                var matchedDetectionKeywords = ExcludedDetectionKeywords
                    .Where(keyword => combined.Contains(keyword))
                    .ToList();

                if (matchedDetectionKeywords.Any())
                {
                    return new DeviceFilterResult
                    {
                        IsIncluded = false,
                        Reason = "Detection device excluded from IDNAC analysis",
                        FamilyName = familyName,
                        TypeName = typeName,
                        MatchedKeywords = matchedDetectionKeywords,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["Category"] = categoryName,
                            ["ExclusionType"] = "DetectionDevice"
                        }
                    };
                }

                // Second check: Include notification devices by keyword matching
                var matchedNotificationKeywords = NotificationKeywords
                    .Where(keyword => combined.Contains(keyword) || categoryUpper.Contains(keyword))
                    .ToList();

                bool hasElectricalParams = CheckElectricalParameters(device);

                if (matchedNotificationKeywords.Any())
                {
                    return new DeviceFilterResult
                    {
                        IsIncluded = true,
                        Reason = "Notification device identified by keywords",
                        FamilyName = familyName,
                        TypeName = typeName,
                        MatchedKeywords = matchedNotificationKeywords,
                        HasRequiredParameters = hasElectricalParams,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["Category"] = categoryName,
                            ["DeviceType"] = GetDeviceType(device),
                            ["HasElectricalParams"] = hasElectricalParams,
                            ["InclusionType"] = "NotificationDevice"
                        }
                    };
                }

                // Third check: Fire Alarm category with electrical parameters
                if ((categoryUpper.Contains("FIRE ALARM") || categoryUpper.Contains("FIRE_ALARM")) && 
                    hasElectricalParams)
                {
                    return new DeviceFilterResult
                    {
                        IsIncluded = true,
                        Reason = "Fire alarm device with electrical parameters",
                        FamilyName = familyName,
                        TypeName = typeName,
                        HasRequiredParameters = true,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["Category"] = categoryName,
                            ["DeviceType"] = GetDeviceType(device),
                            ["InclusionType"] = "ElectricalDevice"
                        }
                    };
                }

                // Fourth check: Any device with electrical parameters (catch-all for non-standard naming)
                if (hasElectricalParams)
                {
                    // Additional validation to ensure it's not a detection device
                    if (!ExcludedDetectionKeywords.Any(k => combined.Contains(k)))
                    {
                        return new DeviceFilterResult
                        {
                            IsIncluded = true,
                            Reason = "Device with electrical parameters (current/wattage)",
                            FamilyName = familyName,
                            TypeName = typeName,
                            HasRequiredParameters = true,
                            AdditionalInfo = new Dictionary<string, object>
                            {
                                ["Category"] = categoryName,
                                ["DeviceType"] = GetDeviceType(device),
                                ["InclusionType"] = "ElectricalParameterMatch"
                            }
                        };
                    }
                }

                // Default: Exclude if no notification criteria matched
                return new DeviceFilterResult
                {
                    IsIncluded = false,
                    Reason = "No notification keywords or electrical parameters found",
                    FamilyName = familyName,
                    TypeName = typeName,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["Category"] = categoryName,
                        ["ExclusionType"] = "NoMatch"
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering IDNAC device: {ex.Message}");
                return DeviceFilterResult.Excluded($"Error during filtering: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if device has electrical parameters (CURRENT DRAW or Wattage)
        /// </summary>
        private bool CheckElectricalParameters(FamilyInstance device)
        {
            try
            {
                // Check instance parameters first
                foreach (var paramName in ElectricalParameters)
                {
                    var param = device.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        if (param.StorageType == StorageType.Double)
                        {
                            var value = param.AsDouble();
                            if (value > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found electrical parameter '{paramName}' with value {value}");
                                return true;
                            }
                        }
                    }
                }

                // Check type parameters if no instance parameters found
                var symbol = device.Symbol;
                if (symbol != null)
                {
                    foreach (var paramName in ElectricalParameters)
                    {
                        var param = symbol.LookupParameter(paramName);
                        if (param != null && param.HasValue)
                        {
                            if (param.StorageType == StorageType.Double)
                            {
                                var value = param.AsDouble();
                                if (value > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Found type electrical parameter '{paramName}' with value {value}");
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking electrical parameters: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines the specific type of notification device
        /// </summary>
        private string GetDeviceType(FamilyInstance device)
        {
            try
            {
                var familyName = device.Symbol.Family.Name.ToUpperInvariant();
                var typeName = device.Symbol.Name.ToUpperInvariant();
                var combined = $"{familyName} {typeName}";

                // Check for device type combinations
                if (combined.Contains("SPEAKER") && combined.Contains("STROBE"))
                    return "Speaker/Strobe";
                if (combined.Contains("HORN") && combined.Contains("STROBE"))
                    return "Horn/Strobe";
                if (combined.Contains("SPEAKER"))
                    return "Speaker";
                if (combined.Contains("STROBE"))
                    return "Strobe";
                if (combined.Contains("HORN"))
                    return "Horn";
                if (combined.Contains("BELL"))
                    return "Bell";
                if (combined.Contains("CHIME"))
                    return "Chime";

                return "Notification Device";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}