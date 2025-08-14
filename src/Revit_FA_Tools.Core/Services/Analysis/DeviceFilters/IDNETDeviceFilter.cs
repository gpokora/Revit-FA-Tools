using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Interfaces.Analysis;

namespace Revit_FA_Tools.Core.Services.Analysis.DeviceFilters
{
    /// <summary>
    /// Filter for IDNET (Intelligent Detection Network) devices
    /// </summary>
    public class IDNETDeviceFilter : IDeviceFilter
    {
        private readonly object _logger;

        /// <summary>
        /// Keywords that identify detection devices
        /// </summary>
        private static readonly string[] DetectionKeywords = {
            "DETECTORS", "DETECTOR", "MODULE", "PULL", "STATION", "MANUAL",
            "MONITOR", "INPUT", "OUTPUT", "SENSOR", "SENSING", "BEAM",
            "HEAT", "SMOKE", "THERMAL", "PHOTOELECTRIC", "IONIZATION"
        };

        /// <summary>
        /// Keywords that identify notification devices to exclude
        /// </summary>
        private static readonly string[] ExcludedNotificationKeywords = {
            "SPEAKER", "STROBE", "HORN", "BELL", "CHIME", "SOUNDER",
            "NOTIFICATION", "APPLIANCE", "NAC", "IDNAC", "AUDIBLE", "VISUAL"
        };

        public CircuitType SupportedCircuitType => CircuitType.IDNET;

        public IDNETDeviceFilter(object logger = null)
        {
            _logger = logger;
        }

        public async Task<List<FamilyInstance>> FilterDevicesAsync(List<FamilyInstance> allDevices)
        {
            if (allDevices == null || allDevices.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("IDNET Filter: No devices to filter");
                return new List<FamilyInstance>();
            }

            System.Diagnostics.Debug.WriteLine($"IDNET Filter: Processing {allDevices.Count} candidate devices");

            var filteredDevices = new List<FamilyInstance>();
            var excludedCount = 0;
            var detectionDeviceCount = 0;

            foreach (var device in allDevices)
            {
                var filterResult = GetFilterReason(device);
                
                if (filterResult.IsIncluded)
                {
                    filteredDevices.Add(device);
                    detectionDeviceCount++;
                    
                    System.Diagnostics.Debug.WriteLine($"IDNET: Included '{filterResult.FamilyName}' - {filterResult.Reason}");
                }
                else
                {
                    excludedCount++;
                    System.Diagnostics.Debug.WriteLine($"IDNET: Excluded '{filterResult.FamilyName}' - {filterResult.Reason}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"IDNET Filter Complete: {detectionDeviceCount} detection devices found, {excludedCount} excluded");

            // Log category breakdown for debugging
            var categoryBreakdown = filteredDevices
                .GroupBy(d => d.Category?.Name ?? "Unknown")
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            
            if (categoryBreakdown.Any())
            {
                System.Diagnostics.Debug.WriteLine($"IDNET Device Categories: {string.Join(", ", categoryBreakdown)}");
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

                // First check: Explicitly exclude notification devices
                var matchedNotificationKeywords = ExcludedNotificationKeywords
                    .Where(keyword => combined.Contains(keyword) || categoryUpper.Contains(keyword))
                    .ToList();

                if (matchedNotificationKeywords.Any())
                {
                    return new DeviceFilterResult
                    {
                        IsIncluded = false,
                        Reason = "Notification device excluded from IDNET analysis",
                        FamilyName = familyName,
                        TypeName = typeName,
                        MatchedKeywords = matchedNotificationKeywords,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["Category"] = categoryName,
                            ["ExclusionType"] = "NotificationDevice"
                        }
                    };
                }

                // Second check: Include detection devices by keyword matching
                var matchedDetectionKeywords = DetectionKeywords
                    .Where(keyword => combined.Contains(keyword) || categoryUpper.Contains(keyword))
                    .ToList();

                if (matchedDetectionKeywords.Any())
                {
                    // Additional validation: Check for FA_DeviceType parameter if available
                    var deviceTypeParam = device.LookupParameter("FA_DeviceType");
                    var deviceType = deviceTypeParam?.AsString();
                    
                    return new DeviceFilterResult
                    {
                        IsIncluded = true,
                        Reason = "Detection device identified by keywords",
                        FamilyName = familyName,
                        TypeName = typeName,
                        MatchedKeywords = matchedDetectionKeywords,
                        HasRequiredParameters = CheckRequiredParameters(device),
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["Category"] = categoryName,
                            ["DeviceType"] = deviceType ?? "Not specified",
                            ["InclusionType"] = "DetectionDevice"
                        }
                    };
                }

                // Third check: Fire Alarm category devices that aren't notification
                if (categoryUpper.Contains("FIRE ALARM") || categoryUpper.Contains("FIRE_ALARM"))
                {
                    // Check if it has detection-related parameters
                    if (HasDetectionParameters(device))
                    {
                        return new DeviceFilterResult
                        {
                            IsIncluded = true,
                            Reason = "Fire alarm device with detection parameters",
                            FamilyName = familyName,
                            TypeName = typeName,
                            HasRequiredParameters = true,
                            AdditionalInfo = new Dictionary<string, object>
                            {
                                ["Category"] = categoryName,
                                ["InclusionType"] = "FireAlarmDetection"
                            }
                        };
                    }
                }

                // Default: Exclude if no detection keywords matched
                return new DeviceFilterResult
                {
                    IsIncluded = false,
                    Reason = "No detection keywords matched",
                    FamilyName = familyName,
                    TypeName = typeName,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["Category"] = categoryName,
                        ["ExclusionType"] = "NoKeywordMatch"
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering IDNET device: {ex.Message}");
                return DeviceFilterResult.Excluded($"Error during filtering: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if device has required parameters for IDNET analysis
        /// </summary>
        private bool CheckRequiredParameters(FamilyInstance device)
        {
            try
            {
                // Check for IDNET-specific parameters
                var requiredParams = new[] { "FA_Address", "FA_Loop", "FA_DeviceType" };
                var hasAddress = false;
                
                foreach (var paramName in requiredParams)
                {
                    var param = device.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        if (paramName == "FA_Address")
                            hasAddress = true;
                    }
                }

                return hasAddress;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if device has detection-related parameters
        /// </summary>
        private bool HasDetectionParameters(FamilyInstance device)
        {
            try
            {
                // Check for detection-specific parameters
                var detectionParams = new[] 
                { 
                    "FA_DeviceType", "Detection_Type", "Sensor_Type", 
                    "Coverage_Area", "Sensitivity", "FA_Zone" 
                };

                foreach (var paramName in detectionParams)
                {
                    var param = device.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        var value = param.AsString() ?? param.AsValueString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            // Check if the value indicates a detection device
                            var valueUpper = value.ToUpperInvariant();
                            if (DetectionKeywords.Any(k => valueUpper.Contains(k)))
                                return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}