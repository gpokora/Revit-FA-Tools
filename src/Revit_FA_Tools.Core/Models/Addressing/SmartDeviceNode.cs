using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Core.Models.Addressing
{
    /// <summary>
    /// Enhanced device model that combines existing DeviceSnapshot with addressing capabilities
    /// CRITICAL: Maintains complete separation between physical position and logical addressing
    /// </summary>
    public class SmartDeviceNode : INotifyPropertyChanged
    {
        private int? _assignedAddress;
        private int _physicalPosition;
        private bool _isAddressLocked;
        private string _deviceName;
        private bool _hasAddressConflict;
        
        public SmartDeviceNode()
        {
            DeviceId = Guid.NewGuid().ToString();
            Connections = new List<PhysicalConnection>();
        }
        
        // Integration with existing DeviceSnapshot - DO NOT MODIFY EXISTING MODELS
        public DeviceSnapshot SourceDevice { get; set; }
        
        // Device identity
        public string DeviceId { get; set; }
        public string DeviceName 
        { 
            get => _deviceName ?? SourceDevice?.FamilyName ?? "Unknown Device"; 
            set => SetProperty(ref _deviceName, value); 
        }
        public string DeviceType { get; set; }
        public NodeType NodeType { get; set; } = NodeType.Device;
        private string _elementId;
        public string ElementId 
        {
            get => _elementId ?? SourceDevice?.ElementId.ToString() ?? "";
            set => _elementId = value;
        }
        
        // CRITICAL: Physical properties (wire connections and installation order)
        public int PhysicalPosition 
        { 
            get => _physicalPosition; 
            set => SetProperty(ref _physicalPosition, value); 
        }
        public AddressingCircuit ParentCircuit { get; set; }
        public List<PhysicalConnection> Connections { get; set; }
        public bool IsPhysicallyInstalled { get; set; }
        
        // CRITICAL: Logical properties (addressing and programming) - COMPLETELY INDEPENDENT
        public int? AssignedAddress 
        { 
            get => _assignedAddress; 
            set => SetProperty(ref _assignedAddress, value); 
        }
        public bool IsAddressLocked 
        { 
            get => _isAddressLocked; 
            set => SetProperty(ref _isAddressLocked, value); 
        }
        public string LogicalZone { get; set; }
        public DateTime? AddressAssignedDate { get; set; }
        public string AssignedBy { get; set; }
        
        // Display and validation properties
        public string DisplayText => $"{DeviceName} [Pos-{PhysicalPosition}] {{{(AssignedAddress?.ToString() ?? "---")}}}";
        public bool IsAddressed => AssignedAddress.HasValue;
        public bool HasAddressConflict 
        { 
            get => _hasAddressConflict; 
            set => SetProperty(ref _hasAddressConflict, value); 
        }
        
        // Electrical properties (integrated with parameter mapping)
        private decimal? _currentDraw;
        public decimal CurrentDraw 
        { 
            get => _currentDraw ?? (decimal)(SourceDevice?.Amps ?? GetEnhancedCurrentDraw());
            set => _currentDraw = value;
        }
        public decimal PowerConsumption => (decimal)(SourceDevice?.Watts ?? 0);
        public int UnitLoads => SourceDevice?.UnitLoads ?? 1;
        
        /// <summary>
        /// Get enhanced current draw from parameter mapping if available
        /// </summary>
        private double GetEnhancedCurrentDraw()
        {
            if (SourceDevice?.HasEnhancedMapping() == true)
            {
                return SourceDevice.GetCurrentDrawMA() / 1000.0; // Convert mA to A
            }
            return 0.0;
        }
        
        // Location properties (from existing models)  
        private string _level;
        public string Level 
        { 
            get => _level ?? SourceDevice?.LevelName ?? "";
            set => _level = value;
        }
        public string Zone => SourceDevice?.Zone ?? "";
        public double Elevation => SourceDevice?.Z ?? 0;
        
        // Additional properties expected by services
        public string Address 
        { 
            get => AssignedAddress?.ToString() ?? "";
            set 
            {
                if (int.TryParse(value, out int addr))
                    AssignedAddress = addr;
                else
                    AssignedAddress = null;
            }
        }
        
        public AddressLockState LockState
        {
            get => IsAddressLocked ? AddressLockState.Locked : AddressLockState.Unlocked;
            set => IsAddressLocked = (value == AddressLockState.Locked || value == AddressLockState.Manual);
        }
        
        public AddressingCircuit Circuit
        {
            get => ParentCircuit;
            set => ParentCircuit = value;
        }
        
        public string Room { get; set; } = ""; // DeviceSnapshot doesn't have Room
        public double X { get; set; } = 0; // Can be set for device positioning
        public double Y { get; set; } = 0; // Can be set for device positioning  
        public double Z { get; set; } = 0; // Can be set for device positioning
        public double Candela { get; set; } = 0; // DeviceSnapshot doesn't have Candela
        public string CircuitNumber { get; set; } = ""; // Can be overridden
        public string Function => DeviceType;
        public string Area => Zone;
        public string NetworkSegment => "";
        public int AddressSlots { get; set; } = 1;
        public double CandelaRating => Candela;
        
        // Additional settable properties for service compatibility
        public string FamilyName { get; set; } = "";
        public string DeviceFunction { get; set; } = "";
        public bool IsNotificationDevice { get; set; } = false;
        
        // Initialize from SourceDevice when available
        private void InitializeFromSourceDevice()
        {
            if (SourceDevice != null)
            {
                if (string.IsNullOrEmpty(FamilyName))
                    FamilyName = SourceDevice.FamilyName;
                if (string.IsNullOrEmpty(DeviceFunction))
                    DeviceFunction = DeviceType;
                if (!IsNotificationDevice)
                    IsNotificationDevice = SourceDevice.HasStrobe || SourceDevice.HasSpeaker;
                if (X == 0) X = SourceDevice.X;
                if (Y == 0) Y = SourceDevice.Y;
                if (Z == 0) Z = SourceDevice.Z;
            }
        }
        
        // CRITICAL OPERATION: Physical move that preserves address
        public void MovePhysically(int newPosition)
        {
            PhysicalPosition = newPosition;
            // ADDRESS MUST REMAIN COMPLETELY UNCHANGED!
        }
        
        // CRITICAL OPERATION: Address assignment
        public bool TryAssignAddress(int address)
        {
            if (ParentCircuit?.AddressPool?.IsAddressAvailable(address) == true)
            {
                AssignedAddress = address;
                AddressAssignedDate = DateTime.Now;
                return true;
            }
            return false;
        }
        
        // CRITICAL OPERATION: Return address to pool
        public void ReturnAddressToPool()
        {
            if (AssignedAddress.HasValue)
            {
                ParentCircuit?.AddressPool?.ReturnAddress(AssignedAddress.Value);
                AssignedAddress = null;
                AddressAssignedDate = null;
            }
        }
        
        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
    
    public class PhysicalConnection
    {
        public string FromDeviceId { get; set; }
        public string ToDeviceId { get; set; }
        public ConnectionType Type { get; set; }
        public double WireDistance { get; set; }
        public string WireGauge { get; set; }
    }
    
    public enum ConnectionType
    {
        DaisyChain,     // Sequential device-to-device connection
        Branch,         // T-tap or junction box connection  
        HomeRun,        // Direct connection back to panel
        EndOfLine       // Last device with EOL resistor
    }
    
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public ValidationSeverity Severity { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<int> SuggestedAlternatives { get; set; } = new List<int>();
        public string DetailedExplanation { get; set; }
    }
    
    public enum ValidationSeverity
    {
        Info,
        Warning, 
        Error,
        Critical
    }

    /// <summary>
    /// Node type for tree structure categorization
    /// </summary>
    public enum NodeType
    {
        Device,
        CircuitBranch,
        Panel,
        Container
    }
}
