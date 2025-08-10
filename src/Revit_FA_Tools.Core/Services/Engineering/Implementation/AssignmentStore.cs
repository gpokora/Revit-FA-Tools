using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Centralized store for device assignment management
    /// </summary>
    public class AssignmentStore : INotifyPropertyChanged
    {
        private static AssignmentStore _instance;
        private static readonly object _lock = new object();
        
        private ObservableCollection<DeviceAssignment> _deviceAssignments;

        #region Singleton Implementation

        public static AssignmentStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AssignmentStore();
                        }
                    }
                }
                return _instance;
            }
        }

        private AssignmentStore()
        {
            _deviceAssignments = new ObservableCollection<DeviceAssignment>();
        }

        #endregion

        #region Properties

        public ObservableCollection<DeviceAssignment> DeviceAssignments
        {
            get => _deviceAssignments;
            private set
            {
                if (_deviceAssignments != value)
                {
                    _deviceAssignments = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add or update device assignment
        /// </summary>
        public void AddOrUpdateAssignment(DeviceAssignment assignment)
        {
            if (assignment == null) return;

            var existing = _deviceAssignments.FirstOrDefault(a => a.ElementId == assignment.ElementId);
            if (existing != null)
            {
                // Update existing assignment
                existing.PanelId = assignment.PanelId;
                existing.BranchId = assignment.BranchId;
                existing.RiserZone = assignment.RiserZone;
                existing.Address = assignment.Address;
                existing.AddressSlots = assignment.AddressSlots;
                existing.LockState = assignment.LockState;
                existing.IsManualAddress = assignment.IsManualAddress;
                existing.IsAssigned = assignment.IsAssigned;
            }
            else
            {
                // Add new assignment
                _deviceAssignments.Add(assignment);
            }
        }

        /// <summary>
        /// Remove device assignment
        /// </summary>
        public bool RemoveAssignment(int elementId)
        {
            var assignment = _deviceAssignments.FirstOrDefault(a => a.ElementId == elementId);
            if (assignment != null)
            {
                return _deviceAssignments.Remove(assignment);
            }
            return false;
        }

        /// <summary>
        /// Get device assignment by element ID
        /// </summary>
        public DeviceAssignment GetAssignment(int elementId)
        {
            return _deviceAssignments.FirstOrDefault(a => a.ElementId == elementId);
        }

        /// <summary>
        /// Get all assignments for a specific branch
        /// </summary>
        public IEnumerable<DeviceAssignment> GetBranchAssignments(string branchId)
        {
            return _deviceAssignments.Where(a => a.BranchId == branchId && a.IsAssigned);
        }

        /// <summary>
        /// Clear all assignments
        /// </summary>
        public void ClearAssignments()
        {
            _deviceAssignments.Clear();
        }

        /// <summary>
        /// Load assignments from Revit elements
        /// </summary>
        public void LoadFromRevitElements(IEnumerable<Element> elements)
        {
            ClearAssignments();

            foreach (var element in elements)
            {
                var assignment = CreateAssignmentFromElement(element);
                if (assignment != null)
                {
                    _deviceAssignments.Add(assignment);
                }
            }
        }

        #endregion

        #region Private Methods

        private DeviceAssignment CreateAssignmentFromElement(Element element)
        {
            try
            {
                var assignment = new DeviceAssignment
                {
                    ElementId = (int)element.Id.Value,
                    IsAssigned = true
                };

                // Read FA_Panel parameter
                var panelParam = element.LookupParameter("FA_Panel");
                if (panelParam != null && panelParam.HasValue)
                {
                    assignment.PanelId = panelParam.AsString() ?? "";
                }

                // Read FA_Branch parameter
                var branchParam = element.LookupParameter("FA_Branch");
                if (branchParam != null && branchParam.HasValue)
                {
                    assignment.BranchId = branchParam.AsString() ?? "";
                }

                // Read FA_RiserZone parameter
                var riserParam = element.LookupParameter("FA_RiserZone");
                if (riserParam != null && riserParam.HasValue)
                {
                    assignment.RiserZone = riserParam.AsString() ?? "";
                }

                // Read FA_Address parameter
                var addressParam = element.LookupParameter("FA_Address");
                if (addressParam != null && addressParam.HasValue)
                {
                    assignment.Address = addressParam.AsInteger();
                }

                // Read FA_AddressLock parameter
                var lockParam = element.LookupParameter("FA_AddressLock");
                if (lockParam != null && lockParam.HasValue)
                {
                    var lockValue = lockParam.AsString();
                    if (Enum.TryParse<AddressLockState>(lockValue, true, out var lockState))
                    {
                        assignment.LockState = lockState;
                    }
                }

                // Determine if address was manually set
                assignment.IsManualAddress = assignment.LockState == AddressLockState.Locked;

                // Set default address slots (can be overridden by device type logic)
                assignment.AddressSlots = 1;

                return assignment;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating assignment from element {element.Id}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }


}