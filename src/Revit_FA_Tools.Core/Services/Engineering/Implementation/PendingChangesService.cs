using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Service for tracking and managing pending changes to devices before applying them to the Revit model
    /// </summary>
    public class PendingChangesService
    {
        private static PendingChangesService _instance;
        private static readonly object _lockObject = new object();

        public static PendingChangesService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                            _instance = new PendingChangesService();
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<int, PendingChange> _pendingChanges = new Dictionary<int, PendingChange>();
        
        public event EventHandler<PendingChangesEventArgs> PendingChangesUpdated;

        public bool HasPending => _pendingChanges.Count > 0;
        public int PendingCount => _pendingChanges.Count;
        public IReadOnlyDictionary<int, PendingChange> PendingChanges => _pendingChanges.ToDictionary(kv => kv.Key, kv => kv.Value);

        public void AddChange(int elementId, string propertyName, object oldValue, object newValue, PendingChangeType changeType = PendingChangeType.Modified)
        {
            var change = new PendingChange
            {
                ElementId = elementId,
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangeType = changeType,
                Timestamp = DateTime.Now
            };

            _pendingChanges[elementId] = change;
            OnPendingChangesUpdated(new PendingChangesEventArgs { ElementId = elementId, Change = change });
        }

        public void RemoveChange(int elementId)
        {
            if (_pendingChanges.Remove(elementId))
            {
                OnPendingChangesUpdated(new PendingChangesEventArgs { ElementId = elementId, Change = null });
            }
        }

        public void ClearChanges()
        {
            _pendingChanges.Clear();
            OnPendingChangesUpdated(new PendingChangesEventArgs { ElementId = -1, Change = null });
        }

        public PendingChange GetChange(int elementId)
        {
            _pendingChanges.TryGetValue(elementId, out PendingChange change);
            return change;
        }

        public bool HasChange(int elementId)
        {
            return _pendingChanges.ContainsKey(elementId);
        }

        public List<Revit_FA_Tools.Models.ValidationResult> ValidateAllChanges()
        {
            var results = new List<Revit_FA_Tools.Models.ValidationResult>();

            foreach (var change in _pendingChanges.Values)
            {
                var validationResult = ValidateChange(change);
                if (!validationResult.IsValid)
                {
                    results.Add(validationResult);
                }
            }

            // Check for address conflicts across all changes
            var addressChanges = _pendingChanges.Values
                .Where(c => c.PropertyName == "Address")
                .ToList();

            var addressGroups = addressChanges
                .GroupBy(c => new { Circuit = GetCircuitForElement(c.ElementId), Address = c.NewValue })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in addressGroups)
            {
                var conflictingElements = group.Select(c => c.ElementId).ToList();
                var validationResult = new Revit_FA_Tools.Models.ValidationResult();
                validationResult.AddError(
                    $"Address conflict: Address {group.Key.Address} is assigned to multiple devices on circuit {group.Key.Circuit}",
                    $"ElementId_{conflictingElements.First()}"
                );
                validationResult.AddError($"Conflicting elements: {string.Join(", ", conflictingElements)}");
                results.Add(validationResult);
            }

            return results;
        }

        private Revit_FA_Tools.Models.ValidationResult ValidateChange(PendingChange change)
        {
            var result = new Revit_FA_Tools.Models.ValidationResult();

            switch (change.PropertyName)
            {
                case "Address":
                    if (change.NewValue is int address)
                    {
                        if (address < 1 || address > 254)
                        {
                            result.AddError("Address must be between 1 and 254", "Address");
                        }
                    }
                    break;

                case "Current":
                    if (change.NewValue is double current)
                    {
                        if (current < 0 || current > 5.0)
                        {
                            result.AddError("Current must be between 0 and 5.0 Amps", "Current");
                        }
                    }
                    break;

                case "Wattage":
                    if (change.NewValue is double wattage)
                    {
                        if (wattage < 0 || wattage > 1000)
                        {
                            result.AddError("Wattage must be between 0 and 1000 Watts", "Wattage");
                        }
                    }
                    break;

                case "Panel":
                    if (string.IsNullOrWhiteSpace(change.NewValue?.ToString()))
                    {
                        result.AddError("Panel assignment cannot be empty", "PanelAssignment");
                    }
                    break;

                case "Circuit":
                    if (string.IsNullOrWhiteSpace(change.NewValue?.ToString()))
                    {
                        result.AddError("Circuit assignment cannot be empty", "CircuitAssignment");
                    }
                    break;
            }

            return result;
        }

        private string GetCircuitForElement(int elementId)
        {
            // This would need to be implemented to get the current circuit assignment for an element
            // For now, return a placeholder
            return "IDNAC-1";
        }

        protected virtual void OnPendingChangesUpdated(PendingChangesEventArgs e)
        {
            PendingChangesUpdated?.Invoke(this, e);
        }
    }

    public class PendingChange
    {
        public int ElementId { get; set; }
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public PendingChangeType ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum PendingChangeType
    {
        Added,
        Modified,
        Deleted
    }

    public class PendingChangesEventArgs : EventArgs
    {
        public int ElementId { get; set; }
        public PendingChange Change { get; set; }
    }

    // Using ValidationResult from Models.DeviceModels
}