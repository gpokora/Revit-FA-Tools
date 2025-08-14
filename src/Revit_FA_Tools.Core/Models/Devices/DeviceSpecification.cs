using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Core.Models.Devices
{
    /// <summary>
    /// Complete device specification for unified fire alarm analysis
    /// </summary>
    public class DeviceSpecification
    {
        /// <summary>
        /// Gets or sets the Revit element ID
        /// </summary>
        public ElementId ElementId { get; set; }

        /// <summary>
        /// Gets or sets the device family name
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// Gets or sets the device type name
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the building level name
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// Gets or sets the device location coordinates
        /// </summary>
        public DeviceLocation Location { get; set; } = new DeviceLocation();

        // Electrical Properties
        /// <summary>
        /// Gets or sets the current draw in amperes
        /// </summary>
        public double CurrentDraw { get; set; }

        /// <summary>
        /// Gets or sets the power consumption in watts
        /// </summary>
        public double PowerConsumption { get; set; }

        /// <summary>
        /// Gets or sets the unit loads for circuit calculations
        /// </summary>
        public int UnitLoads { get; set; }

        /// <summary>
        /// Gets or sets the standby current in amperes
        /// </summary>
        public double StandbyCurrent { get; set; }

        // Device Characteristics
        /// <summary>
        /// Gets or sets whether the device has strobe functionality
        /// </summary>
        public bool HasStrobe { get; set; }

        /// <summary>
        /// Gets or sets whether the device has speaker functionality
        /// </summary>
        public bool HasSpeaker { get; set; }

        /// <summary>
        /// Gets or sets whether the device is an isolator
        /// </summary>
        public bool IsIsolator { get; set; }

        /// <summary>
        /// Gets or sets whether the device is a repeater
        /// </summary>
        public bool IsRepeater { get; set; }

        /// <summary>
        /// Gets or sets the candela rating for strobe devices
        /// </summary>
        public int CandelaRating { get; set; }

        // Repository/Catalog Information
        /// <summary>
        /// Gets or sets the manufacturer SKU
        /// </summary>
        public string SKU { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer name
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the product name
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Gets or sets whether the device is T-Tap compatible
        /// </summary>
        public bool IsTTapCompatible { get; set; }

        /// <summary>
        /// Gets or sets the mounting type
        /// </summary>
        public string MountingType { get; set; }

        /// <summary>
        /// Gets or sets the environmental rating (IP rating, weatherproof, etc.)
        /// </summary>
        public string EnvironmentalRating { get; set; }

        /// <summary>
        /// Gets or sets whether the device is UL listed
        /// </summary>
        public bool IsULListed { get; set; }

        // Addressing Information
        /// <summary>
        /// Gets or sets the device address
        /// </summary>
        public int? Address { get; set; }

        /// <summary>
        /// Gets or sets the loop or branch identifier
        /// </summary>
        public string LoopId { get; set; }

        /// <summary>
        /// Gets or sets the zone identifier
        /// </summary>
        public string Zone { get; set; }

        // Analysis Results
        /// <summary>
        /// Gets or sets the circuit assignment
        /// </summary>
        public string CircuitAssignment { get; set; }

        /// <summary>
        /// Gets or sets any validation warnings for this device
        /// </summary>
        public List<string> ValidationWarnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets any validation errors for this device
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets custom properties and technical specifications
        /// </summary>
        public Dictionary<string, object> TechnicalSpecs { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets custom properties for extensibility
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the analysis timestamp
        /// </summary>
        public DateTime AnalysisTimestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a DeviceSpecification from a DeviceSnapshot
        /// </summary>
        public static DeviceSpecification FromDeviceSnapshot(DeviceSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            return new DeviceSpecification
            {
                ElementId = snapshot.ElementId,
                FamilyName = snapshot.FamilyName,
                TypeName = snapshot.TypeName,
                LevelName = snapshot.Level,
                Location = new DeviceLocation
                {
                    X = snapshot.Location?.X ?? 0,
                    Y = snapshot.Location?.Y ?? 0,
                    Z = snapshot.Location?.Z ?? 0
                },
                CurrentDraw = snapshot.Current,
                PowerConsumption = snapshot.PowerConsumption,
                UnitLoads = snapshot.AddressSlots,
                Zone = snapshot.Zone,
                ValidationErrors = new List<string>(snapshot.ValidationErrors ?? new List<string>()),
                CustomProperties = new Dictionary<string, object>(snapshot.Parameters ?? new Dictionary<string, object>())
            };
        }

        /// <summary>
        /// Gets a display name for the device
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ProductName) ? ProductName : $"{FamilyName} - {TypeName}";

        /// <summary>
        /// Gets the device type based on characteristics
        /// </summary>
        public string DeviceType
        {
            get
            {
                if (IsIsolator) return "Isolator";
                if (IsRepeater) return "Repeater";
                if (HasStrobe && HasSpeaker) return "Horn/Strobe";
                if (HasStrobe) return "Strobe";
                if (HasSpeaker) return "Speaker";
                
                var familyUpper = FamilyName?.ToUpperInvariant() ?? "";
                var typeUpper = TypeName?.ToUpperInvariant() ?? "";
                
                if (familyUpper.Contains("SMOKE") || typeUpper.Contains("SMOKE")) return "Smoke Detector";
                if (familyUpper.Contains("HEAT") || typeUpper.Contains("HEAT")) return "Heat Detector";
                if (familyUpper.Contains("PULL") || typeUpper.Contains("PULL")) return "Pull Station";
                if (familyUpper.Contains("HORN") || typeUpper.Contains("HORN")) return "Horn";
                if (familyUpper.Contains("BELL") || typeUpper.Contains("BELL")) return "Bell";
                
                return "Fire Alarm Device";
            }
        }

        /// <summary>
        /// Gets whether this device is a notification device
        /// </summary>
        public bool IsNotificationDevice => HasStrobe || HasSpeaker || DeviceType.Contains("Horn") || DeviceType.Contains("Bell");

        /// <summary>
        /// Gets whether this device is a detection device
        /// </summary>
        public bool IsDetectionDevice => DeviceType.Contains("Detector") || DeviceType.Contains("Pull");

        /// <summary>
        /// Validates the device specification
        /// </summary>
        public bool IsValid(out List<string> validationMessages)
        {
            validationMessages = new List<string>();

            if (string.IsNullOrWhiteSpace(FamilyName))
                validationMessages.Add("Family name is required");

            if (string.IsNullOrWhiteSpace(TypeName))
                validationMessages.Add("Type name is required");

            if (ElementId == null || ElementId == ElementId.InvalidElementId)
                validationMessages.Add("Valid element ID is required");

            if (IsNotificationDevice && CurrentDraw <= 0)
                validationMessages.Add("Notification devices must have current draw > 0");

            return validationMessages.Count == 0;
        }
    }

    /// <summary>
    /// Device location information
    /// </summary>
    public class DeviceLocation
    {
        /// <summary>
        /// Gets or sets the X coordinate
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Gets or sets the room identifier
        /// </summary>
        public string Room { get; set; }

        /// <summary>
        /// Gets or sets the space identifier
        /// </summary>
        public string Space { get; set; }

        /// <summary>
        /// Calculates distance to another location
        /// </summary>
        public double DistanceTo(DeviceLocation other)
        {
            if (other == null) return double.MaxValue;
            
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}