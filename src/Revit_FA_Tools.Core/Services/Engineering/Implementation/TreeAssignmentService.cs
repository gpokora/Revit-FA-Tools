using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;
using BalancingExclusions = Revit_FA_Tools.FireAlarmConfiguration.BalancingExclusions;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Service for managing tree assignment mutations with validation
    /// Handles device and branch movements, capacity validation, and change tracking
    /// </summary>
    public class TreeAssignmentService
    {
        // Internal collection to replace AssignmentStore functionality
        private readonly List<DeviceAssignment> _deviceAssignments = new List<DeviceAssignment>();
        
        public TreeAssignmentService()
        {
            // Initialize internal state
        }

        /// <summary>
        /// Move a device to a different branch with validation
        /// </summary>
        public ChangeSet MoveDevice(string deviceId, string targetBranchId)
        {
            var changeSet = new ChangeSet
            {
                ChangeType = ChangeType.MoveDevice,
                Description = $"Move device {deviceId} to branch {targetBranchId}"
            };

            try
            {
                // Find current assignment
                var currentAssignment = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId.ToString() == deviceId);
                
                if (currentAssignment == null)
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = $"Device {deviceId} not found in assignment store";
                    return changeSet;
                }

                // Parse target branch info (format: "PanelId-BranchId")
                if (!ParseBranchId(targetBranchId, out string targetPanelId, out string branchSuffix))
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = $"Invalid branch ID format: {targetBranchId}";
                    return changeSet;
                }

                // Store original assignment for rollback
                changeSet.OriginalAssignment = new DeviceAssignment
                {
                    ElementId = currentAssignment.ElementId,
                    PanelId = currentAssignment.PanelId,
                    BranchId = currentAssignment.BranchId,
                    RiserZone = currentAssignment.RiserZone,
                    Address = currentAssignment.Address,
                    AddressSlots = currentAssignment.AddressSlots
                };

                // Validate the move
                var validationResult = ValidateDeviceMove(currentAssignment, targetPanelId, targetBranchId);
                if (!validationResult.IsValid)
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = validationResult.ErrorMessage;
                    return changeSet;
                }

                // Create new assignment
                var newAssignment = new DeviceAssignment
                {
                    ElementId = currentAssignment.ElementId,
                    PanelId = targetPanelId,
                    BranchId = targetBranchId,
                    RiserZone = DetermineRiserZone(targetPanelId, targetBranchId),
                    Address = FindNextAvailableAddress(targetBranchId),
                    AddressSlots = currentAssignment.AddressSlots
                };

                changeSet.NewAssignment = newAssignment;
                changeSet.IsValid = true;

                // Apply the change
                ApplyDeviceMove(changeSet);

                return changeSet;
            }
            catch (Exception ex)
            {
                changeSet.IsValid = false;
                changeSet.ErrorMessage = $"Error moving device: {ex.Message}";
                return changeSet;
            }
        }

        /// <summary>
        /// Move a branch to a different panel with validation
        /// </summary>
        public ChangeSet MoveBranch(string branchId, string targetPanelId)
        {
            var changeSet = new ChangeSet
            {
                ChangeType = ChangeType.MoveBranch,
                Description = $"Move branch {branchId} to panel {targetPanelId}"
            };

            try
            {
                // Find all devices in the branch
                var branchDevices = _deviceAssignments
                    .Where(a => a.BranchId == branchId && a.IsAssigned)
                    .ToList();

                if (!branchDevices.Any())
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = $"Branch {branchId} not found or has no devices";
                    return changeSet;
                }

                // Validate the move
                var validationResult = ValidateBranchMove(branchId, targetPanelId);
                if (!validationResult.IsValid)
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = validationResult.ErrorMessage;
                    return changeSet;
                }

                // Store original assignments for rollback
                changeSet.OriginalBranchAssignments = branchDevices.Select(a => new DeviceAssignment
                {
                    ElementId = a.ElementId,
                    PanelId = a.PanelId,
                    BranchId = a.BranchId,
                    RiserZone = a.RiserZone,
                    Address = a.Address,
                    AddressSlots = a.AddressSlots
                }).ToList();

                // Generate new branch ID for target panel
                string newBranchId = GenerateNewBranchId(targetPanelId);
                
                // Create new assignments
                changeSet.NewBranchAssignments = branchDevices.Select(a => new DeviceAssignment
                {
                    ElementId = a.ElementId,
                    PanelId = targetPanelId,
                    BranchId = newBranchId,
                    RiserZone = DetermineRiserZone(targetPanelId, newBranchId),
                    Address = a.Address, // Keep same relative addressing
                    AddressSlots = a.AddressSlots
                }).ToList();

                changeSet.IsValid = true;

                // Apply the change
                ApplyBranchMove(changeSet);

                return changeSet;
            }
            catch (Exception ex)
            {
                changeSet.IsValid = false;
                changeSet.ErrorMessage = $"Error moving branch: {ex.Message}";
                return changeSet;
            }
        }

        /// <summary>
        /// Add a new panel with validation
        /// </summary>
        public ChangeSet AddPanel(string panelId)
        {
            var changeSet = new ChangeSet
            {
                ChangeType = ChangeType.AddPanel,
                Description = $"Add new panel {panelId}",
                IsValid = true // Panels can always be added
            };

            return changeSet;
        }

        /// <summary>
        /// Add a new branch to a panel with validation
        /// </summary>
        public ChangeSet AddBranch(string panelId, string branchId)
        {
            var changeSet = new ChangeSet
            {
                ChangeType = ChangeType.AddBranch,
                Description = $"Add branch {branchId} to panel {panelId}"
            };

            try
            {
                // Validate panel exists (implicitly through having devices)
                var panelExists = _deviceAssignments.Any(a => a.PanelId == panelId);
                
                // Check if branch ID already exists
                var branchExists = _deviceAssignments.Any(a => a.BranchId == branchId);
                
                if (branchExists)
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = $"Branch {branchId} already exists";
                    return changeSet;
                }

                changeSet.IsValid = true;
                return changeSet;
            }
            catch (Exception ex)
            {
                changeSet.IsValid = false;
                changeSet.ErrorMessage = $"Error adding branch: {ex.Message}";
                return changeSet;
            }
        }

        /// <summary>
        /// Insert a device into a branch at specified position
        /// </summary>
        public ChangeSet InsertDevice(string branchId, DeviceSnapshot deviceSnapshot, int position)
        {
            var changeSet = new ChangeSet
            {
                ChangeType = ChangeType.InsertDevice,
                Description = $"Insert device into branch {branchId} at position {position}"
            };

            try
            {
                // Parse branch info
                if (!ParseBranchId(branchId, out string panelId, out string branchSuffix))
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = $"Invalid branch ID format: {branchId}";
                    return changeSet;
                }

                // Create new assignment
                var newAssignment = new DeviceAssignment
                {
                    ElementId = deviceSnapshot.ElementId,
                    PanelId = panelId,
                    BranchId = branchId,
                    RiserZone = DetermineRiserZone(panelId, branchId),
                    Address = FindNextAvailableAddress(branchId),
                    AddressSlots = CalculateAddressSlots(deviceSnapshot)
                };

                // Validate capacity constraints
                var validationResult = ValidateDeviceInsert(newAssignment);
                if (!validationResult.IsValid)
                {
                    changeSet.IsValid = false;
                    changeSet.ErrorMessage = validationResult.ErrorMessage;
                    return changeSet;
                }

                changeSet.NewAssignment = newAssignment;
                changeSet.IsValid = true;

                // Apply the insertion
                _deviceAssignments.Add(newAssignment);

                return changeSet;
            }
            catch (Exception ex)
            {
                changeSet.IsValid = false;
                changeSet.ErrorMessage = $"Error inserting device: {ex.Message}";
                return changeSet;
            }
        }

        #region Validation Methods

        private ValidationResult ValidateDeviceMove(DeviceAssignment currentAssignment, string targetPanelId, string targetBranchId)
        {
            var config = ConfigurationService.Current;

            // Get device snapshot for load calculations
            var deviceSnapshot = GetDeviceSnapshot(currentAssignment.ElementId);
            if (deviceSnapshot == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Device snapshot not found for validation"
                };
            }

            // Calculate target branch load after adding this device
            var targetBranchLoad = CalculateBranchLoad(targetBranchId);
            var newCurrentLoad = targetBranchLoad.CurrentA + deviceSnapshot.Amps;
            var newULLoad = targetBranchLoad.UnitLoads + deviceSnapshot.UnitLoads;

            // Check dual limits with spare capacity
            var spareCurrentLimit = config.Capacity.IdnacAlarmCurrentLimitA * (1 - config.Spare.SpareFractionDefault);
            var spareULLimit = config.Capacity.IdnacStandbyUnitLoadLimit * (1 - config.Spare.SpareFractionDefault);

            if (newCurrentLoad > spareCurrentLimit)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Move would exceed current limit: {newCurrentLoad:F2}A > {spareCurrentLimit:F2}A (with {config.Spare.SpareFractionDefault*100:F0}% spare)"
                };
            }

            if (newULLoad > spareULLimit)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Move would exceed unit load limit: {newULLoad} UL > {spareULLimit:F0} UL (with {config.Spare.SpareFractionDefault*100:F0}% spare)"
                };
            }

            // Check device count limit
            var targetBranchDeviceCount = _deviceAssignments.Count(a => a.BranchId == targetBranchId && a.IsAssigned);
            if (targetBranchDeviceCount >= 127) // Max devices per IDNAC
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Target branch already has maximum devices (127)"
                };
            }

            // Check circuit balancing restrictions if enabled
            if (config.Balancing?.ForbiddenMixGroups?.Any() == true)
            {
                var currentLevel = GetDeviceLevel(currentAssignment.ElementId);
                var levelConflict = HasLevelConflict(targetBranchId, currentLevel, config.Balancing);
                
                if (levelConflict.HasConflict)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Level mixing violation: {levelConflict.ErrorMessage}"
                    };
                }
            }

            return new ValidationResult { IsValid = true };
        }

        private ValidationResult ValidateBranchMove(string branchId, string targetPanelId)
        {
            var config = ConfigurationService.Current;

            // Calculate total branch load
            var branchLoad = CalculateBranchLoad(branchId);
            
            // Calculate target panel load after adding this branch
            var targetPanelLoad = CalculatePanelLoad(targetPanelId);
            var newPanelCurrentLoad = targetPanelLoad.CurrentA + branchLoad.CurrentA;
            var newPanelULLoad = targetPanelLoad.UnitLoads + branchLoad.UnitLoads;

            // Check if target panel can accommodate this branch
            var panelCurrentCapacity = GetPanelCurrentCapacity(targetPanelId);
            var panelULCapacity = GetPanelULCapacity(targetPanelId);

            if (newPanelCurrentLoad > panelCurrentCapacity)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Move would exceed panel current capacity: {newPanelCurrentLoad:F2}A > {panelCurrentCapacity:F2}A"
                };
            }

            if (newPanelULLoad > panelULCapacity)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Move would exceed panel UL capacity: {newPanelULLoad} UL > {panelULCapacity} UL"
                };
            }

            return new ValidationResult { IsValid = true };
        }

        private ValidationResult ValidateDeviceInsert(DeviceAssignment assignment)
        {
            // Similar validation to device move
            return ValidateDeviceMove(assignment, assignment.PanelId, assignment.BranchId);
        }

        #endregion

        #region Helper Methods

        private void ApplyDeviceMove(ChangeSet changeSet)
        {
            if (changeSet.OriginalAssignment != null && changeSet.NewAssignment != null)
            {
                // Find and update the existing assignment
                var existingAssignment = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId == changeSet.OriginalAssignment.ElementId);
                
                if (existingAssignment != null)
                {
                    existingAssignment.PanelId = changeSet.NewAssignment.PanelId;
                    existingAssignment.BranchId = changeSet.NewAssignment.BranchId;
                    existingAssignment.RiserZone = changeSet.NewAssignment.RiserZone;
                    existingAssignment.Address = changeSet.NewAssignment.Address;
                }
            }
        }

        private void ApplyBranchMove(ChangeSet changeSet)
        {
            if (changeSet.OriginalBranchAssignments != null && changeSet.NewBranchAssignments != null)
            {
                // Update all assignments in the branch
                foreach (var originalAssignment in changeSet.OriginalBranchAssignments)
                {
                    var existingAssignment = _deviceAssignments
                        .FirstOrDefault(a => a.ElementId == originalAssignment.ElementId);
                    
                    var newAssignment = changeSet.NewBranchAssignments
                        .FirstOrDefault(a => a.ElementId == originalAssignment.ElementId);
                    
                    if (existingAssignment != null && newAssignment != null)
                    {
                        existingAssignment.PanelId = newAssignment.PanelId;
                        existingAssignment.BranchId = newAssignment.BranchId;
                        existingAssignment.RiserZone = newAssignment.RiserZone;
                        existingAssignment.Address = newAssignment.Address;
                    }
                }
            }
        }

        private bool ParseBranchId(string branchId, out string panelId, out string branchSuffix)
        {
            panelId = string.Empty;
            branchSuffix = string.Empty;

            if (string.IsNullOrEmpty(branchId))
                return false;

            var parts = branchId.Split('-');
            if (parts.Length == 2)
            {
                panelId = parts[0];
                branchSuffix = parts[1];
                return true;
            }

            return false;
        }

        private string DetermineRiserZone(string panelId, string branchId)
        {
            // Simple zone determination - could be enhanced
            return $"Zone-{panelId}";
        }

        private int FindNextAvailableAddress(string branchId)
        {
            var branchAssignments = _deviceAssignments
                .Where(a => a.BranchId == branchId && a.IsAssigned)
                .OrderBy(a => a.Address)
                .ToList();

            if (!branchAssignments.Any())
                return 1;

            // Find first gap in addressing
            int expectedAddress = 1;
            foreach (var assignment in branchAssignments)
            {
                if (assignment.Address > expectedAddress)
                    return expectedAddress;
                
                expectedAddress = assignment.Address + assignment.AddressSlots;
            }

            return expectedAddress;
        }

        private string GenerateNewBranchId(string panelId)
        {
            var existingBranches = _deviceAssignments
                .Where(a => a.PanelId == panelId)
                .Select(a => a.BranchId)
                .Distinct()
                .Count();

            return $"{panelId}-B{existingBranches + 1:D2}";
        }

        private int CalculateAddressSlots(DeviceSnapshot deviceSnapshot)
        {
            // Most devices take 1 slot, some special devices take more
            if (deviceSnapshot.IsIsolator)
                return 2;
            if (deviceSnapshot.IsRepeater)
                return 2;
            
            return 1;
        }

        private BranchLoad CalculateBranchLoad(string branchId)
        {
            var branchDevices = _deviceAssignments
                .Where(a => a.BranchId == branchId && a.IsAssigned)
                .ToList();

            var totalCurrent = 0.0;
            var totalUL = 0;

            foreach (var assignment in branchDevices)
            {
                var deviceSnapshot = GetDeviceSnapshot(assignment.ElementId);
                if (deviceSnapshot != null)
                {
                    totalCurrent += deviceSnapshot.Amps;
                    totalUL += deviceSnapshot.UnitLoads;
                }
            }

            return new BranchLoad
            {
                CurrentA = totalCurrent,
                UnitLoads = totalUL,
                DeviceCount = branchDevices.Count
            };
        }

        private BranchLoad CalculatePanelLoad(string panelId)
        {
            var panelDevices = _deviceAssignments
                .Where(a => a.PanelId == panelId && a.IsAssigned)
                .ToList();

            var totalCurrent = 0.0;
            var totalUL = 0;

            foreach (var assignment in panelDevices)
            {
                var deviceSnapshot = GetDeviceSnapshot(assignment.ElementId);
                if (deviceSnapshot != null)
                {
                    totalCurrent += deviceSnapshot.Amps;
                    totalUL += deviceSnapshot.UnitLoads;
                }
            }

            return new BranchLoad
            {
                CurrentA = totalCurrent,
                UnitLoads = totalUL,
                DeviceCount = panelDevices.Count
            };
        }

        private double GetPanelCurrentCapacity(string panelId)
        {
            var config = ConfigurationService.Current;
            // Simplified - in reality would calculate based on number of IDNACs needed
            return config.Capacity.IdnacAlarmCurrentLimitA * 3; // Assume 3 IDNACs per panel max
        }

        private int GetPanelULCapacity(string panelId)
        {
            var config = ConfigurationService.Current;
            return config.Capacity.IdnacStandbyUnitLoadLimit * 3; // Assume 3 IDNACs per panel max
        }

        private DeviceSnapshot GetDeviceSnapshot(int elementId)
        {
            // In a real implementation, this would retrieve device snapshot from cache/store
            // For now, return a default snapshot
            return new DeviceSnapshot(elementId, "Unknown", "Unknown", "Unknown", 1.0, 0.1, 1, false, false, false, false);
        }

        private string GetDeviceLevel(int elementId)
        {
            // Would retrieve device level from Revit element
            return "Unknown";
        }

        private (bool HasConflict, string ErrorMessage) HasLevelConflict(string branchId, string deviceLevel, BalancingExclusions balancingConfig)
        {
            // Check if adding this device level to the branch would violate mixing rules
            // This is a simplified implementation
            return (false, string.Empty);
        }

        #endregion
    }

    /// <summary>
    /// Represents a change set for undo/redo functionality
    /// </summary>
    public class ChangeSet
    {
        public ChangeType ChangeType { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // For single device operations
        public DeviceAssignment OriginalAssignment { get; set; }
        public DeviceAssignment NewAssignment { get; set; }

        // For branch operations
        public List<DeviceAssignment> OriginalBranchAssignments { get; set; }
        public List<DeviceAssignment> NewBranchAssignments { get; set; }
    }

    /// <summary>
    /// Types of changes that can be made
    /// </summary>
    public enum ChangeType
    {
        MoveDevice,
        MoveBranch,
        AddPanel,
        AddBranch,
        InsertDevice,
        RemoveDevice,
        RemoveBranch
    }

    /// <summary>
    /// Validation result for assignment operations
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Branch load summary for calculations
    /// </summary>
    public class BranchLoad
    {
        public double CurrentA { get; set; }
        public int UnitLoads { get; set; }
        public int DeviceCount { get; set; }
    }
}