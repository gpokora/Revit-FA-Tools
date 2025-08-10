using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Service for synchronizing UI changes back to the Revit model
    /// </summary>
    public class ModelSyncService
    {
        private readonly Document _document;
        private readonly UIDocument _uiDocument;
        private readonly PendingChangesService _pendingChanges;

        public ModelSyncService(Document document, UIDocument uiDocument)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _uiDocument = uiDocument ?? throw new ArgumentNullException(nameof(uiDocument));
            _pendingChanges = PendingChangesService.Instance;
        }

        public event EventHandler<SyncProgressEventArgs>? SyncProgress;
        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// Apply all pending changes to the Revit model in a single transaction
        /// </summary>
        public async Task<SyncResult> ApplyPendingChangesToModel()
        {
            return await Task.Run(() =>
            {
                var result = new SyncResult();

                try
                {
                    // Validate all changes first
                    var validationResults = _pendingChanges.ValidateAllChanges();
                    var invalidChanges = validationResults.Where(r => !r.IsValid).ToList();

                    if (invalidChanges.Any())
                    {
                        result.Success = false;
                        result.Message = $"Validation failed for {invalidChanges.Count} changes";
                        result.Errors.AddRange(invalidChanges.SelectMany(v => v.Issues.Select(i => i.Message)));
                        return result;
                    }

                    // Group changes by element for efficient processing
                    var changesByElement = _pendingChanges.PendingChanges
                        .GroupBy(kv => kv.Key)
                        .ToDictionary(g => g.Key, g => g.Select(kv => kv.Value).ToList());

                    int totalChanges = changesByElement.Count;
                    int processedChanges = 0;

                    using (var transactionGroup = new TransactionGroup(_document, "Apply Device Changes"))
                    {
                        transactionGroup.Start();

                        try
                        {
                            using (var transaction = new Transaction(_document, "Update Device Properties"))
                            {
                                transaction.Start();

                                foreach (var elementChanges in changesByElement)
                                {
                                    int elementId = elementChanges.Key;
                                    var changes = elementChanges.Value;

                                    var element = _document.GetElement(new ElementId((long)elementId));
                                    if (element == null)
                                    {
                                        result.Warnings.Add($"Element {elementId} not found in model");
                                        continue;
                                    }

                                    // Apply changes to the element
                                    bool elementModified = ApplyChangesToElement(element, changes);
                                    if (elementModified)
                                    {
                                        result.ModifiedElements.Add(elementId);
                                    }

                                    processedChanges++;
                                    OnSyncProgress(new SyncProgressEventArgs
                                    {
                                        Current = processedChanges,
                                        Total = totalChanges,
                                        ElementId = elementId,
                                        Message = $"Updated {element.Name}"
                                    });
                                }

                                transaction.Commit();
                                result.ChangesApplied = processedChanges;
                            }

                            transactionGroup.Assimilate();
                            result.Success = true;
                            result.Message = $"Successfully applied {result.ChangesApplied} changes to {result.ModifiedElements.Count} elements";

                            // Clear pending changes after successful application
                            _pendingChanges.ClearChanges();
                        }
                        catch (Exception)
                        {
                            transactionGroup.RollBack();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Failed to apply changes: {ex.Message}";
                    result.Errors.Add(ex.ToString());
                }

                OnSyncCompleted(new SyncCompletedEventArgs { Result = result });
                return result;
            });
        }

        private bool ApplyChangesToElement(Element element, List<PendingChange> changes)
        {
            bool modified = false;

            foreach (var change in changes)
            {
                try
                {
                    switch (change.PropertyName)
                    {
                        case "Panel":
                            if (SetParameterValue(element, "Panel", change.NewValue?.ToString()))
                                modified = true;
                            break;

                        case "Circuit":
                            if (SetParameterValue(element, "Circuit", change.NewValue?.ToString()))
                                modified = true;
                            break;

                        case "Address":
                            if (change.NewValue is int address)
                            {
                                if (SetParameterValue(element, "Address", address))
                                    modified = true;
                            }
                            break;

                        case "Zone":
                            if (SetParameterValue(element, "Zone", change.NewValue?.ToString()))
                                modified = true;
                            break;

                        case "Current":
                            if (change.NewValue is double current)
                            {
                                if (SetParameterValue(element, "Current", current))
                                    modified = true;
                            }
                            break;

                        case "Wattage":
                            if (change.NewValue is double wattage)
                            {
                                if (SetParameterValue(element, "Wattage", wattage))
                                    modified = true;
                            }
                            break;

                        default:
                            // Handle custom parameters
                            if (SetParameterValue(element, change.PropertyName, change.NewValue))
                                modified = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other changes
                    System.Diagnostics.Debug.WriteLine($"Failed to apply change {change.PropertyName} to element {element.Id}: {ex.Message}");
                }
            }

            return modified;
        }

        private bool SetParameterValue(Element element, string parameterName, object? value)
        {
            try
            {
                // First try to get parameter by name
                Parameter? parameter = element.LookupParameter(parameterName);
                
                if (parameter == null)
                {
                    // Try common variations
                    string[] variations = {
                        parameterName,
                        $"FA_{parameterName}",
                        $"Fire Alarm {parameterName}",
                        parameterName.ToUpper(),
                        parameterName.ToLower()
                    };

                    foreach (string variation in variations)
                    {
                        parameter = element.LookupParameter(variation);
                        if (parameter != null) break;
                    }
                }

                if (parameter == null || parameter.IsReadOnly)
                {
                    return false;
                }

                // Set the parameter value based on its storage type
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        parameter.Set(value?.ToString() ?? "");
                        return true;

                    case StorageType.Integer:
                        if (value is int intValue)
                        {
                            parameter.Set(intValue);
                            return true;
                        }
                        else if (int.TryParse(value?.ToString(), out int parsedInt))
                        {
                            parameter.Set(parsedInt);
                            return true;
                        }
                        break;

                    case StorageType.Double:
                        if (value is double doubleValue)
                        {
                            parameter.Set(doubleValue);
                            return true;
                        }
                        else if (double.TryParse(value?.ToString(), out double parsedDouble))
                        {
                            parameter.Set(parsedDouble);
                            return true;
                        }
                        break;

                    case StorageType.ElementId:
                        if (value is ElementId elementIdValue)
                        {
                            parameter.Set(elementIdValue);
                            return true;
                        }
                        else if (value is int intId)
                        {
                            parameter.Set(new ElementId((long)intId));
                            return true;
                        }
                        break;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set parameter {parameterName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refresh the UI data after model changes
        /// </summary>
        public async Task RefreshUIData()
        {
            try
            {
                // This would trigger a refresh of the UI grids and data
                // Implementation depends on how the UI data is structured
                await Task.Run(() =>
                {
                    // Placeholder for UI refresh logic
                    System.Threading.Thread.Sleep(100);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh UI data: {ex.Message}");
            }
        }

        protected virtual void OnSyncProgress(SyncProgressEventArgs e)
        {
            SyncProgress?.Invoke(this, e);
        }

        protected virtual void OnSyncCompleted(SyncCompletedEventArgs e)
        {
            SyncCompleted?.Invoke(this, e);
        }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ChangesApplied { get; set; }
        public List<int> ModifiedElements { get; set; } = new List<int>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class SyncProgressEventArgs : EventArgs
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public int ElementId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SyncCompletedEventArgs : EventArgs
    {
        public SyncResult Result { get; set; } = new SyncResult();
    }
}