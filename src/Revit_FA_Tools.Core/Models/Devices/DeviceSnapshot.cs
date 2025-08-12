using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Core.Models.Devices
{
    /// <summary>
    /// Thread-safe snapshot of a device for analysis
    /// </summary>
    public class DeviceSnapshot
    {
        public ElementId ElementId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public double PowerConsumption { get; set; }
        public double Current { get; set; }
        public int AddressSlots { get; set; } = 1;
        public XYZ Location { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Circuit { get; set; } = string.Empty;
        public int? AssignedAddress { get; set; }
        public bool IsAddressable { get; set; } = true;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Additional properties for electrical calculations
        /// </summary>
        public double Voltage { get; set; } = 24.0;
        public double Wattage => PowerConsumption;
        public bool IsEmergency { get; set; }
        public string Zone { get; set; } = string.Empty;
        
        /// <summary>
        /// Network-related properties
        /// </summary>
        public string NetworkType { get; set; } = string.Empty; // IDNAC, IDNET, etc.
        public int NetworkCapacity { get; set; } = 1;
        
        /// <summary>
        /// Validation status
        /// </summary>
        public bool IsValid { get; set; } = true;
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }
}