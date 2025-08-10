using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Models.Addressing
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
        public string ElementId => SourceDevice?.ElementId.ToString() ?? "";
        
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
        public decimal CurrentDraw => (decimal)(SourceDevice?.Amps ?? GetEnhancedCurrentDraw());
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
        public string Level => SourceDevice?.LevelName ?? "";
        public string Zone => SourceDevice?.Zone ?? "";
        public double Elevation => SourceDevice?.Z ?? 0;
        
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
}