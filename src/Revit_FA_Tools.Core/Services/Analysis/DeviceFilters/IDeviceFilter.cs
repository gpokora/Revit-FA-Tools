using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Interfaces.Analysis;

namespace Revit_FA_Tools.Core.Services.Analysis.DeviceFilters
{
    /// <summary>
    /// Interface for filtering devices based on circuit type requirements
    /// </summary>
    public interface IDeviceFilter
    {
        /// <summary>
        /// Gets the circuit type this filter supports
        /// </summary>
        CircuitType SupportedCircuitType { get; }

        /// <summary>
        /// Filters devices based on circuit-specific criteria
        /// </summary>
        /// <param name="allDevices">All candidate devices to filter</param>
        /// <returns>List of devices that match the circuit type requirements</returns>
        Task<List<FamilyInstance>> FilterDevicesAsync(List<FamilyInstance> allDevices);

        /// <summary>
        /// Determines if a specific device is supported by this circuit type
        /// </summary>
        /// <param name="device">The device to check</param>
        /// <returns>True if the device is supported</returns>
        bool IsDeviceSupported(FamilyInstance device);

        /// <summary>
        /// Gets detailed information about why a device was included or excluded
        /// </summary>
        /// <param name="device">The device to analyze</param>
        /// <returns>Detailed filter result information</returns>
        DeviceFilterResult GetFilterReason(FamilyInstance device);
    }

    /// <summary>
    /// Result of device filtering with detailed reasoning
    /// </summary>
    public class DeviceFilterResult
    {
        /// <summary>
        /// Gets or sets whether the device is included in the filter
        /// </summary>
        public bool IsIncluded { get; set; }

        /// <summary>
        /// Gets or sets the reason for inclusion or exclusion
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the keywords that matched for this device
        /// </summary>
        public List<string> MatchedKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether the device has required parameters
        /// </summary>
        public bool HasRequiredParameters { get; set; }

        /// <summary>
        /// Gets or sets the device family name
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// Gets or sets the device type name
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets additional details about the filtering decision
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a filter result for an included device
        /// </summary>
        public static DeviceFilterResult Included(string reason, List<string> matchedKeywords = null)
        {
            return new DeviceFilterResult
            {
                IsIncluded = true,
                Reason = reason,
                MatchedKeywords = matchedKeywords ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates a filter result for an excluded device
        /// </summary>
        public static DeviceFilterResult Excluded(string reason)
        {
            return new DeviceFilterResult
            {
                IsIncluded = false,
                Reason = reason
            };
        }
    }
}