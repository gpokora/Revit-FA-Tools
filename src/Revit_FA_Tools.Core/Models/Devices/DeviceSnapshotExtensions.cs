using System;
using System.Collections.Generic;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services.Integration;
using Revit_FA_Tools.Services.ParameterMapping;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Extensions for DeviceSnapshot to support parameter mapping integration
    /// </summary>
    public static class DeviceSnapshotExtensions
    {
        private static ParameterMappingIntegrationService _integrationService = new ParameterMappingIntegrationService();
        
        /// <summary>
        /// Create DeviceSnapshot with parameter-driven enhancements
        /// </summary>
        public static DeviceSnapshot CreateWithParameters(
            int elementId,
            string familyName,
            string typeName,
            string levelName = "Unknown",
            Dictionary<string, object> parameters = null)
        {
            // Create basic snapshot
            var basic = new DeviceSnapshot(
                ElementId: elementId,
                LevelName: levelName,
                FamilyName: familyName,
                TypeName: typeName,
                Watts: 0,
                Amps: 0,
                UnitLoads: 1,
                HasStrobe: false,
                HasSpeaker: false,
                IsIsolator: false,
                IsRepeater: false,
                Zone: null,
                X: 0.0,
                Y: 0.0,
                Z: 0.0,
                StandbyCurrent: 0.0,
                HasOverride: false,
                CustomProperties: parameters ?? new Dictionary<string, object>()
            );
            
            // Apply parameter mapping
            var result = _integrationService.ProcessDeviceComprehensively(basic);
            return result.Success ? result.ParameterMapping.EnhancedSnapshot : basic;
        }
        
        /// <summary>
        /// Get enhanced electrical specifications
        /// </summary>
        public static DeviceSpecification GetSpecification(this DeviceSnapshot device)
        {
            var result = _integrationService.ProcessDeviceComprehensively(device);
            return result.ElectricalSpecifications;
        }
        
        /// <summary>
        /// Check if device has accurate repository-driven specifications
        /// </summary>
        public static bool HasRepositorySpecifications(this DeviceSnapshot device)
        {
            return device.CustomProperties?.ContainsKey("SKU") == true;
        }
        
        /// <summary>
        /// Get SKU from repository specifications
        /// </summary>
        public static string GetSKU(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("SKU", out var sku) == true ? sku?.ToString() : null;
        }
        
        /// <summary>
        /// Check T-Tap compatibility
        /// </summary>
        public static bool IsTTapCompatible(this DeviceSnapshot device)
        {
            if (device.CustomProperties?.TryGetValue("T_TAP_COMPATIBLE", out var compatible) == true)
            {
                return compatible is bool b ? b : bool.TryParse(compatible?.ToString(), out var parsed) && parsed;
            }
            return false; // Default to false if not specified
        }
        
        /// <summary>
        /// Get device mounting type
        /// </summary>
        public static string GetMountingType(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("MOUNTING_TYPE", out var mounting) == true 
                ? mounting?.ToString() 
                : "WALL"; // Default
        }
        
        /// <summary>
        /// Get environmental rating
        /// </summary>
        public static string GetEnvironmentalRating(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("ENVIRONMENTAL_RATING", out var rating) == true 
                ? rating?.ToString() 
                : "INDOOR"; // Default
        }
        
        /// <summary>
        /// Get candela rating from device parameters
        /// </summary>
        public static int GetCandelaRating(this DeviceSnapshot device)
        {
            if (device.CustomProperties?.TryGetValue("CANDELA", out var candela) == true)
            {
                if (int.TryParse(candela?.ToString(), out var rating))
                    return rating;
            }
            
            if (device.CustomProperties?.TryGetValue("CANDELA_RATING", out var rating2) == true)
            {
                if (int.TryParse(rating2?.ToString(), out var rating))
                    return rating;
            }
            
            return 75; // Default candela rating
        }
        
        /// <summary>
        /// Get manufacturer from device specifications
        /// </summary>
        public static string GetManufacturer(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("MANUFACTURER", out var mfg) == true 
                ? mfg?.ToString() 
                : "Unknown";
        }
        
        /// <summary>
        /// Get model number from device
        /// </summary>
        public static string GetModelNumber(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("MODEL_NUMBER", out var model) == true 
                ? model?.ToString() 
                : null;
        }
        
        /// <summary>
        /// Check if device is UL listed
        /// </summary>
        public static bool IsULListed(this DeviceSnapshot device)
        {
            if (device.CustomProperties?.TryGetValue("UL_LISTED", out var listed) == true)
            {
                return listed is bool b ? b : bool.TryParse(listed?.ToString(), out var parsed) && parsed;
            }
            return true; // Default to true (assume UL listed)
        }
        
        /// <summary>
        /// Get current draw in milliamps
        /// </summary>
        public static double GetCurrentDrawMA(this DeviceSnapshot device)
        {
            // Check for alarm current first
            if (device.CustomProperties?.TryGetValue("ALARM_CURRENT_MA", out var alarmCurrent) == true)
            {
                if (double.TryParse(alarmCurrent?.ToString(), out var ma))
                    return ma;
            }
            
            // Fallback to calculated from amps
            return device.Amps * 1000; // Convert A to mA
        }
        
        /// <summary>
        /// Get standby current draw in milliamps
        /// </summary>
        public static double GetStandbyCurrentMA(this DeviceSnapshot device)
        {
            if (device.CustomProperties?.TryGetValue("STANDBY_CURRENT_MA", out var standbyCurrent) == true)
            {
                if (double.TryParse(standbyCurrent?.ToString(), out var ma))
                    return ma;
            }
            
            // Fallback: assume standby is 10% of alarm current
            return GetCurrentDrawMA(device) * 0.1;
        }
        
        /// <summary>
        /// Get device category from classification
        /// </summary>
        public static string GetDeviceCategory(this DeviceSnapshot device)
        {
            if (device.CustomProperties?.TryGetValue("DEVICE_CATEGORY", out var category) == true)
            {
                return category?.ToString();
            }
            
            // Infer from device characteristics
            if (device.HasStrobe && device.HasSpeaker)
                return "HORN_STROBE";
            if (device.HasStrobe)
                return "STROBE";
            if (device.HasSpeaker)
                return "SPEAKER";
            if (device.IsIsolator)
                return "ISOLATOR";
            if (device.IsRepeater)
                return "REPEATER";
            
            return "UNKNOWN";
        }
        
        /// <summary>
        /// Check if device has enhanced parameter mapping
        /// </summary>
        public static bool HasEnhancedMapping(this DeviceSnapshot device)
        {
            return device.CustomProperties?.ContainsKey("PARAMETER_MAPPING_APPLIED") == true;
        }
        
        /// <summary>
        /// Get all technical specifications as formatted string
        /// </summary>
        public static string GetTechnicalSummary(this DeviceSnapshot device)
        {
            var summary = new List<string>();
            
            // Basic electrical specs
            if (device.Watts > 0)
                summary.Add($"Power: {device.Watts}W");
            
            if (device.Amps > 0)
                summary.Add($"Current: {device.Amps:F3}A");
            
            // Candela rating
            var candela = device.GetCandelaRating();
            if (candela > 0)
                summary.Add($"Candela: {candela}cd");
            
            // Device characteristics
            var characteristics = new List<string>();
            if (device.HasStrobe) characteristics.Add("Strobe");
            if (device.HasSpeaker) characteristics.Add("Speaker");
            if (device.IsIsolator) characteristics.Add("Isolator");
            if (device.IsRepeater) characteristics.Add("Repeater");
            
            if (characteristics.Count > 0)
                summary.Add($"Type: {string.Join(", ", characteristics)}");
            
            // T-Tap compatibility
            if (device.IsTTapCompatible())
                summary.Add("T-Tap Compatible");
            
            return string.Join(" | ", summary);
        }
        
        /// <summary>
        /// Create a copy of DeviceSnapshot with updated parameters
        /// </summary>
        public static DeviceSnapshot WithUpdatedParameters(this DeviceSnapshot device, Dictionary<string, object> newParameters)
        {
            var mergedProperties = new Dictionary<string, object>(device.CustomProperties ?? new Dictionary<string, object>());
            
            if (newParameters != null)
            {
                foreach (var kvp in newParameters)
                {
                    mergedProperties[kvp.Key] = kvp.Value;
                }
            }
            
            return new DeviceSnapshot(
                ElementId: device.ElementId,
                LevelName: device.LevelName,
                FamilyName: device.FamilyName,
                TypeName: device.TypeName,
                Watts: device.Watts,
                Amps: device.Amps,
                UnitLoads: device.UnitLoads,
                HasStrobe: device.HasStrobe,
                HasSpeaker: device.HasSpeaker,
                IsIsolator: device.IsIsolator,
                IsRepeater: device.IsRepeater,
                CustomProperties: mergedProperties
            );
        }
        
        /// <summary>
        /// Validate device specifications for consistency
        /// </summary>
        public static ValidationResult ValidateSpecifications(this DeviceSnapshot device)
        {
            var result = new ValidationResult { IsValid = true };

            // Electrical consistency checks
            if (device.Watts > 0 && device.Amps > 0)
            {
                var calculatedWatts = device.Amps * 24; // Assuming 24V
                var powerDifference = Math.Abs(device.Watts - calculatedWatts) / Math.Max(device.Watts, calculatedWatts);

                if (powerDifference > 0.2) // 20% tolerance
                {
                    result.AddWarning($"Power consumption inconsistency: {device.Watts}W vs calculated {calculatedWatts:F1}W");
                }
            }

            // Device characteristic consistency
            if (device.HasStrobe && device.GetCandelaRating() == 0)
            {
                result.AddWarning("Strobe device should have candela rating");
            }

            if (device.HasSpeaker && device.Watts == 0)
            {
                result.AddWarning("Speaker device should have wattage specification");
            }

            return result;
        }
        
        /// <summary>
        /// Get address property (placeholder for DeviceSnapshot compatibility)
        /// </summary>
        public static string GetAddress(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("Address", out var addr) == true ? addr?.ToString() : "";
        }
        
        /// <summary>
        /// Get circuit number property (placeholder for DeviceSnapshot compatibility)
        /// </summary>
        public static string GetCircuitNumber(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("CircuitNumber", out var circuit) == true ? circuit?.ToString() : "";
        }
        
        /// <summary>
        /// Get current draw property (uses Amps)
        /// </summary>
        public static double GetCurrentDraw(this DeviceSnapshot device)
        {
            return device.Amps;
        }
        
        /// <summary>
        /// Get device function (uses DeviceType)
        /// </summary>
        public static string GetDeviceFunction(this DeviceSnapshot device)
        {
            return device.DeviceType;
        }
        
        /// <summary>
        /// Get room property (placeholder)
        /// </summary>
        public static string GetRoom(this DeviceSnapshot device)
        {
            return device.CustomProperties?.TryGetValue("Room", out var room) == true ? room?.ToString() : "";
        }
        
        /// <summary>
        /// Check if device is notification device
        /// </summary>
        public static bool GetIsNotificationDevice(this DeviceSnapshot device)
        {
            return device.HasStrobe || device.HasSpeaker;
        }
        
        /// <summary>
        /// Get candela rating
        /// </summary>
        public static double GetCandela(this DeviceSnapshot device)
        {
            return device.GetCandelaRating();
        }
    }
}