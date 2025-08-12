using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Revit_FA_Tools.Models;
using Revit_FA_Tools;
using Revit_FA_Tools.Services.Addressing;

namespace Revit_FA_Tools.Core.Models.Addressing
{
    /// <summary>
    /// Circuit model for addressing that integrates with existing IDNAC analysis
    /// </summary>
    public class AddressingCircuit : INotifyPropertyChanged
    {
        private string _name;
        private double _safeCapacityThreshold = 0.8; // 80%
        
        public AddressingCircuit()
        {
            Id = Guid.NewGuid().ToString();
            Devices = new ObservableCollection<SmartDeviceNode>();
            AddressPool = new AddressPoolManager(159); // Standard IDNAC capacity
        }
        
        // Circuit identity
        public string Id { get; set; }
        public string Name 
        { 
            get => _name; 
            set => SetProperty(ref _name, value); 
        }
        public string Description { get; set; }
        
        // Integration with existing IDNAC models - DO NOT MODIFY EXISTING MODELS
        public string PanelId { get; set; }
        public string IDNACCardId { get; set; }
        public string CircuitNumber { get; set; } = "";
        
        // Capacity and limits
        public int MaxAddresses { get; set; } = 159;
        public double SafeCapacityThreshold 
        { 
            get => _safeCapacityThreshold; 
            set => SetProperty(ref _safeCapacityThreshold, value); 
        }
        
        // Device collection
        public ObservableCollection<SmartDeviceNode> Devices { get; set; }
        public AddressPoolManager AddressPool { get; set; }
        
        // Calculated properties
        public int UsedCapacity => Devices.Count(d => d.IsAddressed);
        public int PhysicalDeviceCount => Devices.Count;
        public double UtilizationPercentage => (double)UsedCapacity / MaxAddresses;
        public bool IsNearCapacity => UtilizationPercentage > SafeCapacityThreshold;
        
        // Additional properties expected by services
        public int DeviceCount => PhysicalDeviceCount;
        public double DeviceUtilization => UtilizationPercentage;
        public decimal MaxCurrent { get; set; } = 3.0m; // NFPA limit
        public int MaxDevices 
        { 
            get => MaxAddresses; 
            set => MaxAddresses = value; 
        }
        
        // Electrical calculations (integration with existing services)
        public decimal TotalCurrent => Devices.Sum(d => d.CurrentDraw);
        public decimal TotalPower => Devices.Sum(d => d.PowerConsumption);
        public int TotalUnitLoads => Devices.Sum(d => d.UnitLoads);
        
        /// <summary>
        /// Update utilization calculations - called by services
        /// </summary>
        public void UpdateUtilization()
        {
            OnPropertyChanged(nameof(DeviceCount));
            OnPropertyChanged(nameof(DeviceUtilization));
            OnPropertyChanged(nameof(UtilizationPercentage));
            OnPropertyChanged(nameof(UsedCapacity));
            OnPropertyChanged(nameof(TotalCurrent));
            OnPropertyChanged(nameof(TotalPower));
        }
        
        // CRITICAL OPERATION: Add device to circuit
        public void AddDevice(SmartDeviceNode device, int? position = null)
        {
            device.ParentCircuit = this;
            device.PhysicalPosition = position ?? GetNextPhysicalPosition();
            Devices.Add(device);
            
            OnPropertyChanged(nameof(PhysicalDeviceCount));
            OnPropertyChanged(nameof(UtilizationPercentage));
            OnPropertyChanged(nameof(TotalCurrent));
            OnPropertyChanged(nameof(TotalPower));
        }
        
        // CRITICAL OPERATION: Remove device (returns address automatically)
        public void RemoveDevice(SmartDeviceNode device)
        {
            // CRITICAL: Return address to pool if assigned
            device.ReturnAddressToPool();
            
            // Remove from circuit
            Devices.Remove(device);
            device.ParentCircuit = null;
            
            // Update physical positions
            UpdatePhysicalSequence();
            
            OnPropertyChanged(nameof(PhysicalDeviceCount));
            OnPropertyChanged(nameof(UsedCapacity));
            OnPropertyChanged(nameof(UtilizationPercentage));
        }
        
        // CRITICAL OPERATION: Physical reordering (preserves addresses)
        public void ReorderDevicePhysically(SmartDeviceNode device, int newPosition)
        {
            // CRITICAL: Only change physical position - address stays with device
            device.PhysicalPosition = newPosition;
            UpdatePhysicalSequence();
        }
        
        private int GetNextPhysicalPosition()
        {
            return Devices.Count > 0 ? Devices.Max(d => d.PhysicalPosition) + 1 : 1;
        }
        
        private void UpdatePhysicalSequence()
        {
            var sortedDevices = Devices.OrderBy(d => d.PhysicalPosition).ToList();
            for (int i = 0; i < sortedDevices.Count; i++)
            {
                sortedDevices[i].PhysicalPosition = i + 1;
            }
        }
        
        // Auto-assignment methods
        public void AutoAssignSequential(int startAddress = 1)
        {
            var unaddressedDevices = Devices
                .Where(d => !d.IsAddressed)
                .OrderBy(d => d.PhysicalPosition)
                .ToList();
            
            int nextAddress = startAddress;
            foreach (var device in unaddressedDevices)
            {
                nextAddress = AddressPool.GetNextAvailableAddress(nextAddress);
                if (nextAddress > 0)
                {
                    AddressPool.AssignAddress(nextAddress, device);
                    nextAddress++;
                }
            }
            
            OnPropertyChanged(nameof(UsedCapacity));
            OnPropertyChanged(nameof(UtilizationPercentage));
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
    
    /// <summary>
    /// Panel model that integrates with existing panel analysis
    /// </summary>
    public class AddressingPanel : INotifyPropertyChanged
    {
        public AddressingPanel()
        {
            Id = Guid.NewGuid().ToString();
            Circuits = new ObservableCollection<AddressingCircuit>();
        }
        
        public string Id { get; set; }
        public string Name { get; set; }
        public string PanelType { get; set; } // FACP, IDNAC, etc.
        public string Location { get; set; }
        
        // Alias for Id to match service expectations
        public string PanelId 
        { 
            get => Id; 
            set => Id = value; 
        }
        
        // Integration with existing models - DO NOT MODIFY EXISTING MODELS
        public string RevitElementId { get; set; }
        public PanelPlacementRecommendation PlacementInfo { get; set; }
        
        public ObservableCollection<AddressingCircuit> Circuits { get; set; }
        
        // Calculated properties
        public int TotalDevices => Circuits.Sum(c => c.PhysicalDeviceCount);
        public int TotalAddressedDevices => Circuits.Sum(c => c.UsedCapacity);
        public decimal TotalCurrent => Circuits.Sum(c => c.TotalCurrent);
        public decimal TotalPower => Circuits.Sum(c => c.TotalPower);
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}