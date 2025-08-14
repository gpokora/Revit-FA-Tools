using System;

namespace Revit_FA_Tools.Core.Services.ParameterMapping.Implementation
{
    /// <summary>
    /// Interface for fire alarm device catalog services
    /// Supports both IDNAC and IDNET device catalogs
    /// </summary>
    public interface IFireAlarmCatalogService
    {
        /// <summary>
        /// Find device specification for a given family and type
        /// </summary>
        IDeviceSpecResult FindDeviceSpec(string familyName, string typeName, string candela = null, double wattage = 0);
        
        /// <summary>
        /// Get catalog statistics
        /// </summary>
        ICatalogStats GetCatalogStats();
        
        /// <summary>
        /// Test catalog loading
        /// </summary>
        string TestCatalogLoading();
    }
    
    /// <summary>
    /// Base interface for device specification results
    /// </summary>
    public interface IDeviceSpecResult
    {
        string FamilyName { get; set; }
        string TypeName { get; set; }
        bool FoundMatch { get; set; }
        double Current { get; set; }
        int UnitLoads { get; set; }
        string Source { get; set; }
        string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Base interface for catalog statistics
    /// </summary>
    public interface ICatalogStats
    {
        int TotalDevices { get; set; }
        bool CatalogLoaded { get; set; }
        string Version { get; set; }
        DateTime LastUpdated { get; set; }
    }
    
    /// <summary>
    /// Device type enumeration for catalog routing
    /// </summary>
    public enum FireAlarmDeviceType
    {
        IDNAC_Notification,  // Horn, Strobe, Speaker devices
        IDNET_Initiating,    // Detectors, Manual Pull Stations, etc.
        Unknown
    }
    
    /// <summary>
    /// Factory for creating appropriate catalog services
    /// </summary>
    public static class FireAlarmCatalogFactory
    {
        /// <summary>
        /// Create appropriate catalog service based on device type
        /// </summary>
        public static IFireAlarmCatalogService CreateCatalogService(FireAlarmDeviceType deviceType)
        {
            return deviceType switch
            {
                FireAlarmDeviceType.IDNAC_Notification => new IDNACCatalogService(),
                FireAlarmDeviceType.IDNET_Initiating => new IDNETCatalogService(),
                _ => new IDNACCatalogService() // Default to IDNAC for now
            };
        }
        
        /// <summary>
        /// Determine device type from family name patterns
        /// </summary>
        public static FireAlarmDeviceType DetermineDeviceType(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLowerInvariant();
            
            // IDNAC notification device patterns
            if (combined.Contains("horn") || combined.Contains("strobe") || 
                combined.Contains("speaker") || combined.Contains("notification"))
            {
                return FireAlarmDeviceType.IDNAC_Notification;
            }
            
            // IDNET initiating device patterns (for future implementation)
            if (combined.Contains("detector") || combined.Contains("smoke") || 
                combined.Contains("pull") || combined.Contains("manual") ||
                combined.Contains("heat") || combined.Contains("beam"))
            {
                return FireAlarmDeviceType.IDNET_Initiating;
            }
            
            return FireAlarmDeviceType.Unknown;
        }
    }
}