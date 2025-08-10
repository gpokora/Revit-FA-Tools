using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models.Addressing;

namespace Revit_FA_Tools.Services.Addressing
{
    /// <summary>
    /// Enhanced version of existing DeviceAddressingService with pool management
    /// CRITICAL: Manages address availability and automatic return to pool
    /// </summary>
    public class AddressPoolManager
    {
        private readonly SortedSet<int> _availableAddresses;
        private readonly Dictionary<int, SmartDeviceNode> _assignedAddresses;
        private readonly int _maxAddress;
        
        public AddressPoolManager(int maxAddress = 159)
        {
            _maxAddress = maxAddress;
            _availableAddresses = new SortedSet<int>(Enumerable.Range(1, maxAddress));
            _assignedAddresses = new Dictionary<int, SmartDeviceNode>();
        }
        
        // Properties
        public int MaxAddress => _maxAddress;
        public int AssignedCount => _assignedAddresses.Count;
        public int AvailableCount => _availableAddresses.Count;
        public double UtilizationPercentage => (double)AssignedCount / _maxAddress;
        
        // Address availability
        public bool IsAddressAvailable(int address)
        {
            return address >= 1 && address <= _maxAddress && _availableAddresses.Contains(address);
        }
        
        public bool IsAssigned(int address)
        {
            return _assignedAddresses.ContainsKey(address);
        }
        
        public SmartDeviceNode GetAssignedDevice(int address)
        {
            return _assignedAddresses.TryGetValue(address, out var device) ? device : null;
        }
        
        // Address retrieval
        public int GetNextAvailableAddress(int startingFrom = 1)
        {
            return _availableAddresses.FirstOrDefault(a => a >= startingFrom);
        }
        
        public List<int> GetAvailableAddressRange(int count, int startingFrom = 1)
        {
            return _availableAddresses
                .Where(a => a >= startingFrom)
                .Take(count)
                .ToList();
        }
        
        public List<int> GetNearbyAvailableAddresses(int targetAddress, int count)
        {
            var nearby = new List<int>();
            
            // Search around the target address
            for (int offset = 1; nearby.Count < count && offset <= 50; offset++)
            {
                if (IsAddressAvailable(targetAddress + offset))
                    nearby.Add(targetAddress + offset);
                if (nearby.Count < count && IsAddressAvailable(targetAddress - offset))
                    nearby.Add(targetAddress - offset);
            }
            
            return nearby.OrderBy(a => Math.Abs(a - targetAddress)).ToList();
        }
        
        // CRITICAL OPERATION: Address assignment
        public bool AssignAddress(int address, SmartDeviceNode device)
        {
            if (!IsAddressAvailable(address)) return false;
            
            // Return previous address if device was already addressed
            if (device.AssignedAddress.HasValue)
            {
                ReturnAddress(device.AssignedAddress.Value);
            }
            
            // Assign new address
            _availableAddresses.Remove(address);
            _assignedAddresses[address] = device;
            device.AssignedAddress = address;
            device.AddressAssignedDate = DateTime.Now;
            device.AssignedBy = Environment.UserName;
            
            return true;
        }
        
        // CRITICAL OPERATION: Return address to pool
        public void ReturnAddress(int address)
        {
            if (_assignedAddresses.ContainsKey(address))
            {
                var device = _assignedAddresses[address];
                device.AssignedAddress = null;
                device.AddressAssignedDate = null;
                device.AssignedBy = null;
                
                _assignedAddresses.Remove(address);
                _availableAddresses.Add(address);
            }
        }
        
        // Validation and utility methods
        public Models.Addressing.ValidationResult ValidateAddress(int address, SmartDeviceNode device)
        {
            var result = new Models.Addressing.ValidationResult { IsValid = true };
            
            // Range check
            if (address < 1 || address > _maxAddress)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Address {address} is outside valid range (1-{_maxAddress})";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                return result;
            }
            
            // Availability check
            if (IsAssigned(address))
            {
                var conflictDevice = GetAssignedDevice(address);
                if (conflictDevice != device)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Address {address} is already assigned to {conflictDevice.DeviceName}";
                    result.Severity = Models.Addressing.ValidationSeverity.Error;
                    result.SuggestedAlternatives = GetNearbyAvailableAddresses(address, 5);
                    return result;
                }
            }
            
            // Lock check
            if (IsAssigned(address))
            {
                var assignedDevice = GetAssignedDevice(address);
                if (assignedDevice.IsAddressLocked && assignedDevice != device)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Address {address} is locked (device installed in field)";
                    result.Severity = Models.Addressing.ValidationSeverity.Error;
                    return result;
                }
            }
            
            return result;
        }
    }
}