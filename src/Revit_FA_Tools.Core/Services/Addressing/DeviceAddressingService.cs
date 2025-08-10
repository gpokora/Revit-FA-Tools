using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;
using static Revit_FA_Tools.Models.AddressLockState;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Device addressing service with support for locked addresses and first-available allocation
    /// Handles sequential assignment, resequencing, gap filling, and conflict resolution
    /// </summary>
    public class DeviceAddressingService
    {
        /// <summary>
        /// Auto-assign sequential addresses to devices in a branch, respecting locks and manual values
        /// </summary>
        public void AutoAssign(IEnumerable<DeviceAssignment> branch, AddressingOptions options)
        {
            if (branch == null || !branch.Any())
                return;

            try
            {
                var devices = branch.Where(d => d.IsAssigned).OrderBy(d => d.Address).ToList();
                var occupiedIntervals = BuildOccupiedIntervals(devices, options);
                
                int currentAddress = options.StartAddress;
                
                foreach (var device in devices)
                {
                    // Skip locked devices and manual addresses if preserving them
                    if (device.LockState == AddressLockState.Locked || 
                        (options.PreserveManual && device.IsManualAddress))
                    {
                        continue;
                    }
                    
                    // Find next available contiguous block
                    int addressSlots = GetAddressSlots(device);
                    int nextAvailable = FindNextAvailableAddress(currentAddress, addressSlots, occupiedIntervals);
                    
                    if (nextAvailable > 0)
                    {
                        device.Address = nextAvailable;
                        device.AddressSlots = addressSlots;
                        
                        // Update occupied intervals
                        occupiedIntervals.Add(new AddressInterval(nextAvailable, nextAvailable + addressSlots - 1));
                        occupiedIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));
                        
                        currentAddress = nextAvailable + addressSlots;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error during auto-assign: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resequence addresses to compact gaps, moving only Auto devices and preserving Locked ones
        /// </summary>
        public void Resequence(IEnumerable<DeviceAssignment> branch, AddressingOptions options)
        {
            if (branch == null || !branch.Any())
                return;

            try
            {
                var devices = branch.Where(d => d.IsAssigned).OrderBy(d => d.Address).ToList();
                var lockedDevices = devices.Where(d => d.LockState == AddressLockState.Locked).ToList();
                var autoDevices = devices.Where(d => d.LockState == AddressLockState.Auto && 
                                                   (!options.PreserveManual || !d.IsManualAddress)).ToList();
                
                // Build occupied intervals from locked devices only
                var lockedIntervals = BuildOccupiedIntervals(lockedDevices, options);
                
                int currentAddress = options.StartAddress;
                
                foreach (var device in autoDevices)
                {
                    int addressSlots = GetAddressSlots(device);
                    
                    // Find next available block that doesn't conflict with locked devices
                    int nextAvailable = FindNextAvailableAddress(currentAddress, addressSlots, lockedIntervals);
                    
                    if (nextAvailable > 0)
                    {
                        device.Address = nextAvailable;
                        device.AddressSlots = addressSlots;
                        device.IsManualAddress = false; // Clear manual flag after resequencing
                        
                        currentAddress = nextAvailable + addressSlots;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error during resequence: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fill gaps in address sequence by moving only unassigned Auto devices
        /// </summary>
        public void GapFill(IEnumerable<DeviceAssignment> branch, AddressingOptions options)
        {
            if (branch == null || !branch.Any())
                return;

            try
            {
                var devices = branch.Where(d => d.IsAssigned).ToList();
                var occupiedIntervals = BuildOccupiedIntervals(devices, options);
                
                var unassignedDevices = devices.Where(d => d.Address <= 0 && 
                                                         d.LockState == AddressLockState.Auto).ToList();
                
                foreach (var device in unassignedDevices)
                {
                    int addressSlots = GetAddressSlots(device);
                    int nextAvailable = FindNextAvailableAddress(options.StartAddress, addressSlots, occupiedIntervals);
                    
                    if (nextAvailable > 0)
                    {
                        device.Address = nextAvailable;
                        device.AddressSlots = addressSlots;
                        
                        // Update occupied intervals
                        occupiedIntervals.Add(new AddressInterval(nextAvailable, nextAvailable + addressSlots - 1));
                        occupiedIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error during gap fill: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Assign first available address block to a specific device
        /// </summary>
        public bool FirstAvailableForDevice(DeviceAssignment device, IEnumerable<DeviceAssignment> branch, AddressingOptions options)
        {
            if (device == null || branch == null)
                return false;

            try
            {
                // Don't assign to locked devices unless specifically allowed
                if (device.LockState == AddressLockState.Locked && options.RespectLocks)
                    return false;

                var allDevices = branch.Where(d => d.IsAssigned && d.ElementId != device.ElementId).ToList();
                var occupiedIntervals = BuildOccupiedIntervals(allDevices, options);
                
                int addressSlots = GetAddressSlots(device);
                int nextAvailable = FindNextAvailableAddress(options.StartAddress, addressSlots, occupiedIntervals);
                
                if (nextAvailable > 0)
                {
                    device.Address = nextAvailable;
                    device.AddressSlots = addressSlots;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding first available for device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate address assignments for conflicts and overlaps
        /// </summary>
        public AddressingValidationResult ValidateAddressing(IEnumerable<DeviceAssignment> branch)
        {
            var result = new AddressingValidationResult();

            try
            {
                var devices = branch.Where(d => d.IsAssigned && d.Address > 0).ToList();
                var addressMap = new Dictionary<int, List<DeviceAssignment>>();
                
                // Build address occupation map
                foreach (var device in devices)
                {
                    for (int addr = device.Address; addr < device.Address + device.AddressSlots; addr++)
                    {
                        if (!addressMap.ContainsKey(addr))
                            addressMap[addr] = new List<DeviceAssignment>();
                        
                        addressMap[addr].Add(device);
                    }
                }
                
                // Check for conflicts
                foreach (var kvp in addressMap.Where(kv => kv.Value.Count > 1))
                {
                    var conflict = new AddressConflict
                    {
                        Address = kvp.Key,
                        ConflictingDevices = kvp.Value.ToList(),
                        CanAutoResolve = kvp.Value.All(d => d.LockState == AddressLockState.Auto)
                    };
                    
                    result.Conflicts.Add(conflict);
                }
                
                // Calculate address range
                if (devices.Any())
                {
                    result.AddressRangeStart = devices.Min(d => d.Address);
                    result.AddressRangeEnd = devices.Max(d => d.Address + d.AddressSlots - 1);
                    result.LockedDeviceCount = devices.Count(d => d.LockState == AddressLockState.Locked);
                    result.AutoDeviceCount = devices.Count(d => d.LockState == AddressLockState.Auto);
                    result.TotalDeviceCount = devices.Count;
                }
                
                result.IsValid = !result.Conflicts.Any();
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Validation error: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Resolve addressing conflicts by gap-filling Auto devices
        /// </summary>
        public bool ResolveConflicts(IEnumerable<DeviceAssignment> branch, AddressingOptions options)
        {
            try
            {
                var validation = ValidateAddressing(branch);
                if (validation.IsValid)
                    return true;

                // Get all conflicted Auto devices
                var conflictedAutoDevices = validation.Conflicts
                    .SelectMany(c => c.ConflictingDevices)
                    .Where(d => d.LockState == AddressLockState.Auto)
                    .Distinct()
                    .ToList();

                // Clear addresses of conflicted Auto devices
                foreach (var device in conflictedAutoDevices)
                {
                    device.Address = 0;
                    device.IsManualAddress = false;
                }

                // Gap fill the cleared devices
                GapFill(branch, options);

                // Re-validate
                var revalidation = ValidateAddressing(branch);
                return revalidation.IsValid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving conflicts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get suggested address slots for device based on configuration and overrides
        /// </summary>
        public int GetAddressSlots(DeviceAssignment device)
        {
            if (device.AddressSlots > 0)
                return device.AddressSlots;

            // Try to get device snapshot for slot calculation
            var deviceSnapshot = GetDeviceSnapshot(device.ElementId);
            if (deviceSnapshot != null)
            {
                return CalculateAddressSlots(deviceSnapshot);
            }

            return 1; // Default
        }

        /// <summary>
        /// Generate address range summary for reporting
        /// </summary>
        public string GetAddressRangeSummary(IEnumerable<DeviceAssignment> branch)
        {
            var validation = ValidateAddressing(branch);
            
            if (validation.TotalDeviceCount == 0)
                return "No devices";

            var summary = $"Addr: {validation.AddressRangeStart}-{validation.AddressRangeEnd}";
            
            if (validation.LockedDeviceCount > 0)
                summary += $" (Locked: {validation.LockedDeviceCount})";
                
            if (!validation.IsValid)
                summary += $" [CONFLICTS: {validation.Conflicts.Count}]";
                
            return summary;
        }

        #region Private Helper Methods

        private List<AddressInterval> BuildOccupiedIntervals(IEnumerable<DeviceAssignment> devices, AddressingOptions options)
        {
            var intervals = new List<AddressInterval>();

            foreach (var device in devices.Where(d => d.Address > 0))
            {
                // Include locked devices and manual addresses if preserving them
                if (device.LockState == AddressLockState.Locked || 
                    (options.PreserveManual && device.IsManualAddress))
                {
                    int slots = GetAddressSlots(device);
                    intervals.Add(new AddressInterval(device.Address, device.Address + slots - 1));
                }
            }

            // Merge overlapping intervals
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            var merged = new List<AddressInterval>();

            foreach (var interval in intervals)
            {
                if (merged.Any() && merged.Last().End >= interval.Start - 1)
                {
                    merged.Last().End = Math.Max(merged.Last().End, interval.End);
                }
                else
                {
                    merged.Add(interval);
                }
            }

            return merged;
        }

        private int FindNextAvailableAddress(int startAddress, int slotsNeeded, List<AddressInterval> occupiedIntervals)
        {
            int candidate = startAddress;

            foreach (var interval in occupiedIntervals.OrderBy(i => i.Start))
            {
                // Check if candidate range fits before this interval
                if (candidate + slotsNeeded - 1 < interval.Start)
                {
                    return candidate;
                }

                // Move candidate past this interval
                if (interval.End >= candidate)
                {
                    candidate = interval.End + 1;
                }
            }

            // Check if candidate range fits after all intervals
            return candidate;
        }

        private DeviceSnapshot GetDeviceSnapshot(int elementId)
        {
            // In a real implementation, would retrieve from cache or services
            // For now, return default
            return new DeviceSnapshot(elementId, "Unknown", "Unknown", "Unknown", 1.0, 0.1, 1, false, false, false, false, "Unknown", 0.0, 0.0, 0.0, 0.0, false, null);
        }

        private int CalculateAddressSlots(DeviceSnapshot deviceSnapshot)
        {
            // Address slot calculation based on device type
            if (deviceSnapshot.IsIsolator)
                return 2;
            if (deviceSnapshot.IsRepeater)
                return 2;
            if (deviceSnapshot.HasStrobe && deviceSnapshot.HasSpeaker)
                return 2; // Combo units may need 2 slots

            return 1;
        }

        #endregion
    }

    // Using AddressLockState from Models.DeviceModels

    /// <summary>
    /// Options for addressing operations
    /// </summary>
    public class AddressingOptions
    {
        public int StartAddress { get; set; } = 1;
        public bool PreserveManual { get; set; } = true;      // Do not overwrite user-entered values
        public bool RespectLocks { get; set; } = true;       // Do not move devices marked Locked
        public bool GapFill { get; set; } = true;            // Fill gaps left by removals
    }

    /// <summary>
    /// Address interval for tracking occupied ranges
    /// </summary>
    public class AddressInterval
    {
        public int Start { get; set; }
        public int End { get; set; }

        public AddressInterval(int start, int end)
        {
            Start = start;
            End = end;
        }

        public bool Overlaps(AddressInterval other)
        {
            return Start <= other.End && End >= other.Start;
        }

        public bool Contains(int address)
        {
            return address >= Start && address <= End;
        }
    }

    /// <summary>
    /// Result of address validation
    /// </summary>
    public class AddressingValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<AddressConflict> Conflicts { get; set; } = new List<AddressConflict>();
        
        public int AddressRangeStart { get; set; }
        public int AddressRangeEnd { get; set; }
        public int LockedDeviceCount { get; set; }
        public int AutoDeviceCount { get; set; }
        public int TotalDeviceCount { get; set; }
    }

    /// <summary>
    /// Address conflict information
    /// </summary>
    public class AddressConflict
    {
        public int Address { get; set; }
        public List<DeviceAssignment> ConflictingDevices { get; set; } = new List<DeviceAssignment>();
        public bool CanAutoResolve { get; set; }
        
        public string Description => 
            $"Address {Address}: {ConflictingDevices.Count} devices conflict " +
            $"({string.Join(", ", ConflictingDevices.Select(d => $"#{d.ElementId}"))})";
    }
}