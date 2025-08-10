using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Undo/Redo stack service for tracking and reversing assignment changes
    /// Supports multiple undo levels with automatic cleanup of old changes
    /// </summary>
    public class UndoStack
    {
        private readonly Stack<ChangeSet> _undoStack;
        private readonly Stack<ChangeSet> _redoStack;
        private readonly int _maxUndoLevels;
        // Internal collection to replace AssignmentStore functionality
        private readonly List<DeviceAssignment> _deviceAssignments = new List<DeviceAssignment>();

        public UndoStack(int maxUndoLevels = 50)
        {
            _undoStack = new Stack<ChangeSet>();
            _redoStack = new Stack<ChangeSet>();
            _maxUndoLevels = maxUndoLevels;
        }

        /// <summary>
        /// Whether undo operation is available
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Whether redo operation is available
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Number of undo operations available
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Number of redo operations available
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Push a new changeset onto the undo stack
        /// </summary>
        public void Push(ChangeSet changeSet)
        {
            if (changeSet?.IsValid != true)
                return;

            try
            {
                // Clear redo stack when new change is made
                _redoStack.Clear();

                // Add to undo stack
                _undoStack.Push(changeSet);

                // Maintain max undo levels
                while (_undoStack.Count > _maxUndoLevels)
                {
                    // Remove oldest changes
                    var stackItems = _undoStack.ToArray().Reverse().ToArray();
                    _undoStack.Clear();
                    
                    for (int i = 1; i < stackItems.Length; i++) // Skip first (oldest)
                    {
                        _undoStack.Push(stackItems[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pushing to undo stack: {ex.Message}");
            }
        }

        /// <summary>
        /// Undo the last operation
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo)
                return false;

            try
            {
                var changeSet = _undoStack.Pop();
                
                // Perform the undo operation
                bool success = PerformUndo(changeSet);
                
                if (success)
                {
                    // Move to redo stack
                    _redoStack.Push(changeSet);
                    return true;
                }
                else
                {
                    // Put it back on undo stack if undo failed
                    _undoStack.Push(changeSet);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during undo: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Redo the last undone operation
        /// </summary>
        public bool Redo()
        {
            if (!CanRedo)
                return false;

            try
            {
                var changeSet = _redoStack.Pop();
                
                // Perform the redo operation
                bool success = PerformRedo(changeSet);
                
                if (success)
                {
                    // Move back to undo stack
                    _undoStack.Push(changeSet);
                    return true;
                }
                else
                {
                    // Put it back on redo stack if redo failed
                    _redoStack.Push(changeSet);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during redo: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear all undo/redo history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        /// <summary>
        /// Get descriptions of available undo operations
        /// </summary>
        public List<string> GetUndoDescriptions(int maxCount = 10)
        {
            return _undoStack
                .Take(maxCount)
                .Select(cs => cs.Description)
                .ToList();
        }

        /// <summary>
        /// Get descriptions of available redo operations
        /// </summary>
        public List<string> GetRedoDescriptions(int maxCount = 10)
        {
            return _redoStack
                .Take(maxCount)
                .Select(cs => cs.Description)
                .ToList();
        }

        /// <summary>
        /// Perform undo operation based on change type
        /// </summary>
        private bool PerformUndo(ChangeSet changeSet)
        {
            switch (changeSet.ChangeType)
            {
                case ChangeType.MoveDevice:
                    return UndoDeviceMove(changeSet);
                    
                case ChangeType.MoveBranch:
                    return UndoBranchMove(changeSet);
                    
                case ChangeType.InsertDevice:
                    return UndoDeviceInsert(changeSet);
                    
                case ChangeType.AddPanel:
                    return UndoAddPanel(changeSet);
                    
                case ChangeType.AddBranch:
                    return UndoAddBranch(changeSet);
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"Unsupported undo operation: {changeSet.ChangeType}");
                    return false;
            }
        }

        /// <summary>
        /// Perform redo operation based on change type
        /// </summary>
        private bool PerformRedo(ChangeSet changeSet)
        {
            switch (changeSet.ChangeType)
            {
                case ChangeType.MoveDevice:
                    return RedoDeviceMove(changeSet);
                    
                case ChangeType.MoveBranch:
                    return RedoBranchMove(changeSet);
                    
                case ChangeType.InsertDevice:
                    return RedoDeviceInsert(changeSet);
                    
                case ChangeType.AddPanel:
                    return RedoAddPanel(changeSet);
                    
                case ChangeType.AddBranch:
                    return RedoAddBranch(changeSet);
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"Unsupported redo operation: {changeSet.ChangeType}");
                    return false;
            }
        }

        #region Undo Operations

        private bool UndoDeviceMove(ChangeSet changeSet)
        {
            if (changeSet.OriginalAssignment == null)
                return false;

            try
            {
                // Find the current assignment
                var currentAssignment = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId == changeSet.OriginalAssignment.ElementId);
                
                if (currentAssignment == null)
                    return false;

                // Restore original values
                currentAssignment.PanelId = changeSet.OriginalAssignment.PanelId;
                currentAssignment.BranchId = changeSet.OriginalAssignment.BranchId;
                currentAssignment.RiserZone = changeSet.OriginalAssignment.RiserZone;
                currentAssignment.Address = changeSet.OriginalAssignment.Address;
                currentAssignment.AddressSlots = changeSet.OriginalAssignment.AddressSlots;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing device move: {ex.Message}");
                return false;
            }
        }

        private bool UndoBranchMove(ChangeSet changeSet)
        {
            if (changeSet.OriginalBranchAssignments?.Any() != true)
                return false;

            try
            {
                // Restore all assignments in the branch
                foreach (var originalAssignment in changeSet.OriginalBranchAssignments)
                {
                    var currentAssignment = _deviceAssignments
                        .FirstOrDefault(a => a.ElementId == originalAssignment.ElementId);
                    
                    if (currentAssignment != null)
                    {
                        currentAssignment.PanelId = originalAssignment.PanelId;
                        currentAssignment.BranchId = originalAssignment.BranchId;
                        currentAssignment.RiserZone = originalAssignment.RiserZone;
                        currentAssignment.Address = originalAssignment.Address;
                        currentAssignment.AddressSlots = originalAssignment.AddressSlots;
                        // IsAssigned is computed automatically from PanelId, BranchId, and Address
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing branch move: {ex.Message}");
                return false;
            }
        }

        private bool UndoDeviceInsert(ChangeSet changeSet)
        {
            if (changeSet.NewAssignment == null)
                return false;

            try
            {
                // Remove the inserted device
                var assignmentToRemove = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId == changeSet.NewAssignment.ElementId);
                
                if (assignmentToRemove != null)
                {
                    _deviceAssignments.Remove(assignmentToRemove);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing device insert: {ex.Message}");
                return false;
            }
        }

        private bool UndoAddPanel(ChangeSet changeSet)
        {
            // Panel additions are logical - no actual assignment store changes
            // The UI will handle removing the empty panel from display
            return true;
        }

        private bool UndoAddBranch(ChangeSet changeSet)
        {
            // Branch additions are logical - no actual assignment store changes
            // The UI will handle removing the empty branch from display
            return true;
        }

        #endregion

        #region Redo Operations

        private bool RedoDeviceMove(ChangeSet changeSet)
        {
            if (changeSet.NewAssignment == null)
                return false;

            try
            {
                // Find the current assignment
                var currentAssignment = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId == changeSet.NewAssignment.ElementId);
                
                if (currentAssignment == null)
                    return false;

                // Apply new values
                currentAssignment.PanelId = changeSet.NewAssignment.PanelId;
                currentAssignment.BranchId = changeSet.NewAssignment.BranchId;
                currentAssignment.RiserZone = changeSet.NewAssignment.RiserZone;
                currentAssignment.Address = changeSet.NewAssignment.Address;
                currentAssignment.AddressSlots = changeSet.NewAssignment.AddressSlots;
                // IsAssigned is computed automatically from PanelId, BranchId, and Address

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error redoing device move: {ex.Message}");
                return false;
            }
        }

        private bool RedoBranchMove(ChangeSet changeSet)
        {
            if (changeSet.NewBranchAssignments?.Any() != true)
                return false;

            try
            {
                // Apply all new assignments in the branch
                foreach (var newAssignment in changeSet.NewBranchAssignments)
                {
                    var currentAssignment = _deviceAssignments
                        .FirstOrDefault(a => a.ElementId == newAssignment.ElementId);
                    
                    if (currentAssignment != null)
                    {
                        currentAssignment.PanelId = newAssignment.PanelId;
                        currentAssignment.BranchId = newAssignment.BranchId;
                        currentAssignment.RiserZone = newAssignment.RiserZone;
                        currentAssignment.Address = newAssignment.Address;
                        currentAssignment.AddressSlots = newAssignment.AddressSlots;
                        // IsAssigned is computed automatically from PanelId, BranchId, and Address
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error redoing branch move: {ex.Message}");
                return false;
            }
        }

        private bool RedoDeviceInsert(ChangeSet changeSet)
        {
            if (changeSet.NewAssignment == null)
                return false;

            try
            {
                // Re-add the device assignment
                var existingAssignment = _deviceAssignments
                    .FirstOrDefault(a => a.ElementId == changeSet.NewAssignment.ElementId);
                
                if (existingAssignment == null)
                {
                    _deviceAssignments.Add(new DeviceAssignment
                    {
                        ElementId = changeSet.NewAssignment.ElementId,
                        PanelId = changeSet.NewAssignment.PanelId,
                        BranchId = changeSet.NewAssignment.BranchId,
                        RiserZone = changeSet.NewAssignment.RiserZone,
                        Address = changeSet.NewAssignment.Address,
                        AddressSlots = changeSet.NewAssignment.AddressSlots
                    });
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error redoing device insert: {ex.Message}");
                return false;
            }
        }

        private bool RedoAddPanel(ChangeSet changeSet)
        {
            // Panel additions are logical - no actual assignment store changes
            return true;
        }

        private bool RedoAddBranch(ChangeSet changeSet)
        {
            // Branch additions are logical - no actual assignment store changes
            return true;
        }

        #endregion

        /// <summary>
        /// Get memory usage statistics for the undo stack
        /// </summary>
        public UndoStackStats GetStatistics()
        {
            return new UndoStackStats
            {
                UndoCount = UndoCount,
                RedoCount = RedoCount,
                MaxUndoLevels = _maxUndoLevels,
                OldestUndoTimestamp = _undoStack.Count > 0 ? 
                    _undoStack.ToArray().Last().Timestamp : (DateTime?)null,
                NewestUndoTimestamp = _undoStack.Count > 0 ? 
                    _undoStack.Peek().Timestamp : (DateTime?)null
            };
        }
    }

    /// <summary>
    /// Statistics about the undo stack
    /// </summary>
    public class UndoStackStats
    {
        public int UndoCount { get; set; }
        public int RedoCount { get; set; }
        public int MaxUndoLevels { get; set; }
        public DateTime? OldestUndoTimestamp { get; set; }
        public DateTime? NewestUndoTimestamp { get; set; }
    }
}