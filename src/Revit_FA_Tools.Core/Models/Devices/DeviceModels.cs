using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Address lock state for device assignments
    /// </summary>
    public enum AddressLockState
    {
        /// <summary>
        /// Address can be automatically reassigned
        /// </summary>
        Auto,
        
        /// <summary>
        /// Address is unlocked and can be changed
        /// </summary>
        Unlocked,
        
        /// <summary>
        /// Address is locked and should not be changed
        /// </summary>
        Locked,
        
        /// <summary>
        /// Address was manually set by user
        /// </summary>
        Manual
    }

    /// <summary>
    /// Thread-safe snapshot of a fire alarm device for off-thread analysis
    /// </summary>
    public record DeviceSnapshot(
        int ElementId, 
        string LevelName, 
        string FamilyName, 
        string TypeName,
        double Watts, 
        double Amps, 
        int UnitLoads,
        bool HasStrobe, 
        bool HasSpeaker, 
        bool IsIsolator, 
        bool IsRepeater,
        string? Zone = null,
        double X = 0.0,
        double Y = 0.0,
        double Z = 0.0,
        double StandbyCurrent = 0.0,
        bool HasOverride = false,
        Dictionary<string, object> CustomProperties = null
    )
    {
        public string Description => $"{FamilyName} - {TypeName}";
        
        public string DeviceType => GetDeviceType();
        
        // Initialize CustomProperties if null
        public Dictionary<string, object> ActualCustomProperties => CustomProperties ?? new Dictionary<string, object>();
        
        /// <summary>
        /// Alias for LevelName for backward compatibility
        /// </summary>
        public string Level => LevelName;
        
        /// <summary>
        /// Alias for Amps for alarm current calculations
        /// </summary>
        public double AlarmCurrent => Amps;
        
        
        private string GetDeviceType()
        {
            // Priority-based classification
            if (IsIsolator) return "ISOLATOR";
            if (IsRepeater) return "REPEATER";
            if (HasSpeaker && HasStrobe) return "SPEAKER_STROBE";
            if (HasSpeaker) return "SPEAKER";
            if (HasStrobe) return "STROBE";
            
            // Fallback to name-based classification
            var familyUpper = FamilyName?.ToUpper()?.Trim() ?? "";
            var typeUpper = TypeName?.ToUpper()?.Trim() ?? "";
            
            // Detection devices
            if (familyUpper.Contains("SMOKE") || typeUpper.Contains("SMOKE")) return "SMOKE_DETECTOR";
            if (familyUpper.Contains("HEAT") || typeUpper.Contains("HEAT")) return "HEAT_DETECTOR";
            
            // Manual stations
            if (familyUpper.Contains("MANUAL") || typeUpper.Contains("MANUAL") || 
                familyUpper.Contains("PULL") || typeUpper.Contains("PULL") ||
                familyUpper.Contains("STATION") || typeUpper.Contains("STATION")) return "MANUAL_STATION";
            
            // Notification devices
            if (familyUpper.Contains("HORN") || typeUpper.Contains("HORN")) return "HORN";
            if (familyUpper.Contains("BELL") || typeUpper.Contains("BELL")) return "BELL";
            if (familyUpper.Contains("CHIME") || typeUpper.Contains("CHIME")) return "CHIME";
            
            // Interface modules
            if (familyUpper.Contains("MODULE") || typeUpper.Contains("MODULE") ||
                familyUpper.Contains("INTERFACE") || typeUpper.Contains("INTERFACE")) return "MODULE";
            
            // Return type name as fallback if not empty
            if (!string.IsNullOrWhiteSpace(TypeName)) return TypeName.ToUpper().Replace(" ", "_");
            
            return "UNKNOWN";
        }
        
        /// <summary>
        /// Check if this device requires amplifier support
        /// </summary>
        public bool RequiresAmplifier => HasSpeaker || DeviceType == "SPEAKER" || DeviceType == "SPEAKER_STROBE";
        
        /// <summary>
        /// Get the computed zone designation for this device level (when Zone parameter is null)
        /// </summary>
        public string ComputedZone => GetZoneFromLevel(LevelName);
        
        /// <summary>
        /// Get the effective zone - uses the provided Zone parameter or computes from level
        /// </summary>
        public string EffectiveZone => Zone ?? ComputedZone;
        
        private string GetZoneFromLevel(string levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName)) return "UNASSIGNED";
            
            var levelUpper = levelName.ToUpper().Trim();
            
            // Basement zones
            if (levelUpper.Contains("BASEMENT") || levelUpper.Contains("B1") || levelUpper.Contains("B2") || 
                levelUpper.Contains("LOWER") || levelUpper.Contains("SUB"))
                return "BASEMENT";
                
            // Parking and garage zones
            if (levelUpper.Contains("PARKING") || levelUpper.Contains("GARAGE") || 
                levelUpper.Contains("P1") || levelUpper.Contains("P2"))
                return "PARKING";
                
            // Villa zones (for mixed-use buildings)
            if (levelUpper.Contains("VILLA") || levelUpper.Contains("RESIDENTIAL"))
                return "VILLA";
                
            // Mechanical zones
            if (levelUpper.Contains("MECH") || levelUpper.Contains("MECHANICAL") || 
                levelUpper.Contains("ROOF") || levelUpper.Contains("PENTHOUSE") ||
                levelUpper.Contains("EQUIPMENT"))
                return "MECHANICAL";
                
            // Ground floor variations
            if (levelUpper.Contains("GROUND") || levelUpper.Contains("G") || 
                levelUpper.Contains("LOBBY") || levelUpper.Contains("ENTRY"))
                return "GROUND";
                
            // Try to extract floor number for upper floors
            if (levelUpper.Contains("LEVEL") || levelUpper.Contains("FLOOR") || char.IsDigit(levelUpper[0]))
            {
                // Extract numeric part
                var digits = new string(levelUpper.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int floorNumber))
                {
                    if (floorNumber <= 3) return "LOWER_FLOORS";
                    if (floorNumber <= 10) return "MID_FLOORS";
                    return "UPPER_FLOORS";
                }
            }
            
            return "MAIN";
        }
        
        /// <summary>
        /// Validate this device snapshot for data integrity
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            
            // Required fields
            if (ElementId <= 0)
                result.AddError("Element ID must be positive", nameof(ElementId));
                
            if (string.IsNullOrWhiteSpace(FamilyName))
                result.AddError("Family name is required", nameof(FamilyName));
                
            if (string.IsNullOrWhiteSpace(TypeName))
                result.AddError("Type name is required", nameof(TypeName));
                
            if (string.IsNullOrWhiteSpace(LevelName))
                result.AddError("Level name is required", nameof(LevelName));
                
            // Value ranges
            if (Watts < 0)
                result.AddError("Wattage cannot be negative", nameof(Watts));
                
            if (Amps < 0)
                result.AddError("Amperage cannot be negative", nameof(Amps));
                
            if (UnitLoads < 0)
                result.AddError("Unit loads cannot be negative", nameof(UnitLoads));
                
            // Logical consistency checks
            if (HasSpeaker && Watts <= 0)
                result.AddWarning("Speaker devices typically have wattage greater than 0", nameof(Watts));
                
            if (HasStrobe && Amps <= 0)
                result.AddWarning("Strobe devices typically have current draw greater than 0", nameof(Amps));
                
            if (Amps > 0 && UnitLoads == 0)
                result.AddWarning("Device with current draw should have unit loads > 0", nameof(UnitLoads));
                
            // NFPA compliance checks
            if (Amps > 3.0)
                result.AddError("Device current exceeds NFPA 3.0A circuit limit", nameof(Amps));
                
            if (UnitLoads > 139)
                result.AddError("Device unit loads exceed NFPA 139 UL circuit limit", nameof(UnitLoads));
                
            return result;
        }
        
        /// <summary>
        /// Check if this device is valid for IDNAC calculations
        /// </summary>
        public bool IsValidForIDNAC => Validate().IsValid && (HasStrobe || HasSpeaker || Amps > 0);
    }
    
    /// <summary>
    /// Device assignment to specific panel, branch, and address
    /// </summary>
    public class DeviceAssignment : INotifyPropertyChanged
    {
        private int _elementId;
        private string _panelId = string.Empty;
        private string _branchId = string.Empty;
        private string _riserZone = string.Empty;
        private int _address;
        private int _addressSlots = 1;

        public int ElementId 
        { 
            get => _elementId; 
            set 
            { 
                if (_elementId != value) 
                { 
                    _elementId = value; 
                    OnPropertyChanged(); 
                } 
            } 
        }

        public string PanelId 
        { 
            get => _panelId; 
            set 
            { 
                if (_panelId != value) 
                { 
                    _panelId = value ?? string.Empty; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(FullAddress));
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }

        public string BranchId 
        { 
            get => _branchId; 
            set 
            { 
                if (_branchId != value) 
                { 
                    _branchId = value ?? string.Empty; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(FullAddress));
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }

        public string RiserZone 
        { 
            get => _riserZone; 
            set 
            { 
                if (_riserZone != value) 
                { 
                    _riserZone = value ?? string.Empty; 
                    OnPropertyChanged(); 
                } 
            } 
        }

        public int Address 
        { 
            get => _address; 
            set 
            { 
                if (_address != value) 
                { 
                    _address = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(FullAddress));
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }

        public int AddressSlots 
        { 
            get => _addressSlots; 
            set 
            { 
                if (_addressSlots != value) 
                { 
                    _addressSlots = Math.Max(1, value); 
                    OnPropertyChanged(); 
                } 
            } 
        }
        
        private AddressLockState _lockState = AddressLockState.Auto;
        private bool _isManualAddress = false;
        private bool _isAssigned = false;
        
        /// <summary>
        /// Address lock state - Auto allows automatic reassignment, Locked preserves current address
        /// </summary>
        public AddressLockState LockState 
        { 
            get => _lockState; 
            set 
            { 
                if (_lockState != value) 
                { 
                    _lockState = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }
        
        /// <summary>
        /// Whether the address was manually entered by the user
        /// </summary>
        public bool IsManualAddress 
        { 
            get => _isManualAddress; 
            set 
            { 
                if (_isManualAddress != value) 
                { 
                    _isManualAddress = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }
        
        public bool IsAssigned 
        { 
            get => _isAssigned; 
            set 
            { 
                if (_isAssigned != value) 
                { 
                    _isAssigned = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(StatusDescription));
                } 
            } 
        }
        
        // Additional properties for service compatibility
        public string CircuitNumber { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        
        public string FullAddress => $"{PanelId}.{BranchId}.{Address:D3}";
        
        /// <summary>
        /// Status description for UI display
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (!IsAssigned) return "Unassigned";
                
                var status = FullAddress;
                if (LockState == AddressLockState.Locked)
                    status += " [LOCKED]";
                else if (IsManualAddress)
                    status += " [MANUAL]";
                
                return status;
            }
        }
        
        public override string ToString()
        {
            return StatusDescription;
        }
        
        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    
    /// <summary>
    /// Circuit branch containing devices with capacity tracking
    /// </summary>
    public class CircuitBranch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string PanelId { get; set; } = string.Empty;
        public int BranchNumber { get; set; }
        
        /// <summary>
        /// Alias for TotalAmps - required by CircuitOrganizationService
        /// </summary>
        public double TotalAlarmCurrent => TotalAmps;
        
        /// <summary>
        /// Associated power supply ID for this circuit branch
        /// </summary>
        public string PowerSupplyId { get; set; } = string.Empty;
        
        private List<DeviceSnapshot> _devices = new List<DeviceSnapshot>();
        
        public List<DeviceSnapshot> Devices 
        { 
            get => _devices; 
            set => _devices = value ?? new List<DeviceSnapshot>(); 
        }
        
        public double TotalAmps => Devices?.Sum(d => d.Amps) ?? 0.0;
        public double TotalWatts => Devices?.Sum(d => d.Watts) ?? 0.0;
        public int TotalUnitLoads => Devices?.Sum(d => d.UnitLoads) ?? 0;
        public int TotalDeviceCount => Devices?.Count ?? 0;
        
        /// <summary>
        /// Total standby current for all devices in this branch
        /// </summary>
        public double TotalStandbyCurrent => Devices?.Sum(d => d.StandbyCurrent) ?? 0.0;
        
        public bool HasRepeater => Devices?.Any(d => d.IsRepeater) ?? false;
        public bool HasIsolator => Devices?.Any(d => d.IsIsolator) ?? false;
        public bool RequiresAmplifier => Devices?.Any(d => d.RequiresAmplifier) ?? false;
        
        public string LimitingFactor { get; set; } = "None";
        public double CapacityUtilization { get; set; }
        public double VoltageDropPercent { get; set; }
        
        /// <summary>
        /// Total cable length for this circuit branch in feet
        /// </summary>
        public double CableLength { get; set; }
        
        /// <summary>
        /// Cable resistance in ohms per 1000 feet
        /// </summary>
        public double CableResistance { get; set; } = 1.5; // Default for typical fire alarm cable
        
        public ValidationResult ValidationResult { get; set; } = new ValidationResult();
        
        /// <summary>
        /// Validate circuit branch for NFPA compliance and configuration issues
        /// </summary>
        public ValidationResult ValidateBranch(double spareFraction = 0.20)
        {
            var result = new ValidationResult();
            
            // Basic validation
            if (string.IsNullOrWhiteSpace(Name))
                result.AddError("Circuit branch name is required", nameof(Name));
                
            if (string.IsNullOrWhiteSpace(PanelId))
                result.AddError("Panel ID is required", nameof(PanelId));
                
            if (BranchNumber <= 0)
                result.AddError("Branch number must be positive", nameof(BranchNumber));
            
            // NFPA hard limits (never exceeded)
            if (TotalAmps > 3.0)
                result.AddError($"Circuit exceeds NFPA 3.0A hard limit: {TotalAmps:F2}A", "CurrentLimit");
                
            if (TotalUnitLoads > 139)
                result.AddError($"Circuit exceeds NFPA 139 UL hard limit: {TotalUnitLoads} UL", "UnitLoadLimit");
            
            // Spare capacity enforcement
            var effectiveMaxA = 3.0 * (1.0 - spareFraction);
            var effectiveMaxUL = 139.0 * (1.0 - spareFraction);
            
            if (TotalAmps > effectiveMaxA)
                result.AddWarning($"Exceeds spare-adjusted current limit ({effectiveMaxA:F2}A): {TotalAmps:F2}A", "CurrentCapacity");
                
            if (TotalUnitLoads > effectiveMaxUL)
                result.AddWarning($"Exceeds spare-adjusted UL limit ({effectiveMaxUL:F0} UL): {TotalUnitLoads} UL", "ULCapacity");
            
            // Device validation
            if (Devices?.Any() == true)
            {
                foreach (var device in Devices)
                {
                    var deviceValidation = device.Validate();
                    if (!deviceValidation.IsValid)
                    {
                        result.AddError($"Device {device.ElementId} validation failed", "DeviceValidation");
                    }
                }
            }
            else
            {
                result.AddWarning("Circuit branch has no devices", "EmptyCircuit");
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if circuit is within NFPA hard limits
        /// </summary>
        public bool IsWithinHardLimits => TotalAmps <= 3.0 && TotalUnitLoads <= 139;
        
        /// <summary>
        /// Check if circuit is within spare-adjusted limits
        /// </summary>
        public bool IsWithinSpareCapacity(double spareFraction = 0.20) => 
            TotalAmps <= (3.0 * (1.0 - spareFraction)) && TotalUnitLoads <= (139.0 * (1.0 - spareFraction));
        
        /// <summary>
        /// Calculate capacity utilization percentage against spare-adjusted limits (0-100)
        /// </summary>
        public double GetCapacityUtilization(double spareFraction = 0.20)
        {
            var effectiveMaxA = 3.0 * (1.0 - spareFraction);
            var effectiveMaxUL = 139.0 * (1.0 - spareFraction);
            
            var currentUtil = TotalAmps / effectiveMaxA * 100;
            var ulUtil = TotalUnitLoads / effectiveMaxUL * 100;
            return Math.Max(currentUtil, ulUtil);
        }
        
        public override string ToString()
        {
            return $"{Name} ({TotalDeviceCount} devices, {TotalAmps:F2}A, {TotalUnitLoads} UL)";
        }
    }
    
    /// <summary>
    /// Validation result with issues and status
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
        
        public void AddError(string message, string? field = null)
        {
            IsValid = false;
            Issues.Add(new ValidationIssue 
            { 
                Severity = "ERROR", 
                Message = message, 
                Field = field 
            });
        }
        
        public void AddWarning(string message, string? field = null)
        {
            Issues.Add(new ValidationIssue 
            { 
                Severity = "WARNING", 
                Message = message, 
                Field = field 
            });
        }
        
        public bool HasErrors => Issues?.Any(i => i.Severity == "ERROR") ?? false;
        public bool HasWarnings => Issues?.Any(i => i.Severity == "WARNING") ?? false;
        public int ErrorCount => Issues?.Count(i => i.Severity == "ERROR") ?? 0;
        public int WarningCount => Issues?.Count(i => i.Severity == "WARNING") ?? 0;
        
        /// <summary>
        /// Get summary of validation issues
        /// </summary>
        public string GetSummary()
        {
            if (IsValid) return "Validation passed";
            
            var summary = new List<string>();
            if (HasErrors) summary.Add($"{ErrorCount} error(s)");
            if (HasWarnings) summary.Add($"{WarningCount} warning(s)");
            
            return string.Join(", ", summary);
        }
    }
    
    /// <summary>
    /// Individual validation issue
    /// </summary>
    public class ValidationIssue
    {
        public string Severity { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Analysis progress tracking
    /// </summary>
    public class AnalysisProgress
    {
        public string Operation { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Total { get; set; }
        public double PercentComplete => Total > 0 ? (double)Current / Total * 100 : 0;
        public string Message { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        
        public override string ToString()
        {
            return $"{Operation}: {Current}/{Total} ({PercentComplete:F1}%) - {Message}";
        }
    }
    
    /// <summary>
    /// Voltage drop calculation result
    /// </summary>
    public record VoltageDropResult(double Vdrop, double Percent)
    {
        public bool IsAcceptable(double maxPercent = 10.0) => Percent <= maxPercent;
        
        public string Status => IsAcceptable() ? "OK" : "EXCEEDED";
        
        public override string ToString()
        {
            return $"{Vdrop:F2}V ({Percent:F1}%) - {Status}";
        }
    }
    
    /// <summary>
    /// Power supply configuration for IDNAC circuits
    /// </summary>
    public class PowerSupply
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PanelId { get; set; } = string.Empty;
        public double MaxAlarmCurrent { get; set; } = 3.0;  // NFPA per IDNAC loop
        public double NominalVoltage { get; set; } = 24.0;
        public double SpareFraction { get; set; } = 0.20;   // default spare, editable in UI
        
        /// <summary>
        /// Alias for SpareFraction expressed as percentage (0-100)
        /// </summary>
        public double SparePercent 
        { 
            get => SpareFraction * 100.0; 
            set => SpareFraction = value / 100.0; 
        }
        
        /// <summary>
        /// Collection of circuit branches assigned to this power supply
        /// </summary>
        public List<CircuitBranch> Branches { get; set; } = new List<CircuitBranch>();
        
        /// <summary>
        /// Maximum number of branches this power supply can handle
        /// </summary>
        public int MaxBranches { get; set; } = 8;
        
        /// <summary>
        /// Total alarm current load from all assigned branches
        /// </summary>
        public double TotalAlarmLoad => Branches?.Sum(b => b.TotalAlarmCurrent) ?? 0.0;
        
        /// <summary>
        /// Total capacity available for alarm conditions
        /// </summary>
        public double TotalCapacity => MaxAlarmCurrent;
        
        /// <summary>
        /// Usable capacity accounting for spare requirements
        /// </summary>
        public double UsableCapacity => EffectiveMaxCurrent;
        
        /// <summary>
        /// Get the effective maximum current accounting for spare capacity
        /// </summary>
        public double EffectiveMaxCurrent => MaxAlarmCurrent * (1.0 - SpareFraction);
        
        /// <summary>
        /// Check if a given current is within safe operating limits
        /// </summary>
        public bool IsCurrentWithinLimits(double current) => current <= EffectiveMaxCurrent;
        
        /// <summary>
        /// Get utilization percentage for given current (0-100)
        /// </summary>
        public double GetUtilizationPercent(double current) => (current / EffectiveMaxCurrent) * 100.0;
        
        /// <summary>
        /// Total standby load across all branches
        /// </summary>
        public double TotalStandbyLoad => Branches?.Sum(b => b.TotalStandbyCurrent) ?? 0.0;
        
        /// <summary>
        /// Number of amplifier blocks required for speaker loads
        /// </summary>
        public int AmplifierBlocks => (int)Math.Ceiling(TotalAlarmLoad / 3.5); // Estimate based on typical amp capacity
        
        /// <summary>
        /// Current consumption of amplifiers
        /// </summary>
        public double AmplifierCurrent => AmplifierBlocks * 0.5; // Estimate standby current per amp block
        
        /// <summary>
        /// Validation result for this power supply
        /// </summary>
        public ValidationResult Validation { get; set; } = new ValidationResult();
        
        public override string ToString()
        {
            return $"{Id}: {EffectiveMaxCurrent:F2}A effective ({MaxAlarmCurrent:F1}A - {SpareFraction*100:F0}% spare)";
        }
    }

    /// <summary>
    /// Snapshot of a device for analysis calculations
    /// </summary>


    /// <summary>
    /// Panel placement recommendation
    /// </summary>
    public class PanelPlacementRecommendation
    {
        public string Id { get; set; }
        public string PanelType { get; set; }
        public string RecommendedLocation { get; set; }
        public string Level { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Reasoning { get; set; }
        public List<string> ServedDevices { get; set; } = new List<string>();
        public ValidationResult Validation { get; set; } = new ValidationResult();
    }

}