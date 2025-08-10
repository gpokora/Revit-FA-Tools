using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services;

namespace Revit_FA_Tools.Views
{
    /// <summary>
    /// Device addressing management window with detailed controls for address assignment
    /// </summary>
    public partial class AddressingPanelWindow : ThemedWindow
    {
        #region Private Fields
        
        private readonly Document _document;
        private readonly UIDocument _uiDocument;
        private readonly DeviceAddressingService _addressingService;
        private readonly AssignmentStore _assignmentStore;
        private ObservableCollection<AddressingGridItem> _devices;
        private string _selectedBranchId;

        #endregion

        #region Constructor

        public AddressingPanelWindow(Document document, UIDocument uiDocument)
        {
            InitializeComponent();
            
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _uiDocument = uiDocument ?? throw new ArgumentNullException(nameof(uiDocument));
            _addressingService = new DeviceAddressingService();
            _assignmentStore = AssignmentStore.Instance;
            _devices = new ObservableCollection<AddressingGridItem>();
            
            InitializeWindow();
            LoadData();
        }

        #endregion

        #region Initialization

        private void InitializeWindow()
        {
            // Set up grid data context
            DeviceGrid.ItemsSource = _devices;
            
            // Subscribe to TableView.CellValueChanged instead of GridControl.CellValueChanged
            var view = DeviceGrid.View as DevExpress.Xpf.Grid.TableView;
            if (view != null)
            {
                view.CellValueChanged += OnCellValueChanged;
            }

            // Set initial options
            StartAddressSpinner.Value = 1;
            PreserveManualChk.IsChecked = true;
            RespectLocksChk.IsChecked = true;
            GapFillChk.IsChecked = true;
        }

        private void LoadData()
        {
            try
            {
                var branches = _assignmentStore.DeviceAssignments
                     .Where(a => a.IsAssigned && !string.IsNullOrEmpty(a.BranchId))
                     .Select(a => a.BranchId)
                     .Distinct()
                     .OrderBy(b => b)
                     .ToList();

                BranchSelector.Items.Clear();
                BranchSelector.Items.Add("All Branches");
                foreach (var branch in branches)
                {
                    BranchSelector.Items.Add(branch);
                }

                if (BranchSelector.Items.Count > 1)
                {
                    BranchSelector.SelectedIndex = 1; // Select first actual branch
                }
                else
                {
                    BranchSelector.SelectedIndex = 0; // Select "All Branches"
                }

                RefreshDeviceGrid();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error loading data: {ex.Message}", "Data Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Data Management

        private void RefreshDeviceGrid()
        {
            try
            {
                _devices.Clear();
                
                var assignments = _assignmentStore.DeviceAssignments
                    .Where(a => a.IsAssigned)
                    .Where(a => string.IsNullOrEmpty(_selectedBranchId) || 
                               _selectedBranchId == "All Branches" || 
                               a.BranchId == _selectedBranchId)
                    .OrderBy(a => a.BranchId)
                    .ThenBy(a => a.Address)
                    .ToList();

                foreach (var assignment in assignments)
                {
                    var gridItem = new AddressingGridItem
                    {
                        Assignment = assignment,
                        ElementId = assignment.ElementId,
                        Level = GetDeviceLevel(assignment.ElementId),
                        Family = GetDeviceFamily(assignment.ElementId),
                        Type = GetDeviceType(assignment.ElementId),
                        BranchId = assignment.BranchId,
                        Address = assignment.Address,
                        AddressSlots = assignment.AddressSlots,
                        LockState = assignment.LockState.ToString(),
                        StatusDescription = assignment.StatusDescription,
                        ValidationMessage = ValidateDevice(assignment)
                    };

                    _devices.Add(gridItem);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error refreshing grid: {ex.Message}", "Grid Refresh Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSummary()
        {
            try
            {
                var currentBranchDevices = GetCurrentBranchDevices();
                
                if (currentBranchDevices.Any())
                {
                    var validation = _addressingService.ValidateAddressing(currentBranchDevices);
                    var summary = _addressingService.GetAddressRangeSummary(currentBranchDevices);

                    AddressRangeText.Text = $"{validation.AddressRangeStart}-{validation.AddressRangeEnd}";
                    TotalDevicesText.Text = validation.TotalDeviceCount.ToString();
                    LockedDevicesText.Text = validation.LockedDeviceCount.ToString();
                    ConflictsText.Text = validation.Conflicts.Count.ToString();
                    
                    ConflictsText.Foreground = validation.IsValid ? 
                        System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                }
                else
                {
                    AddressRangeText.Text = "No devices";
                    TotalDevicesText.Text = "0";
                    LockedDevicesText.Text = "0";
                    ConflictsText.Text = "0";
                    ConflictsText.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating summary: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void BranchSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BranchSelector.SelectedItem != null)
            {
                _selectedBranchId = BranchSelector.SelectedItem.ToString();
                if (_selectedBranchId == "All Branches")
                    _selectedBranchId = null;
                    
                RefreshDeviceGrid();
                UpdateSummary();
            }
        }

        private void OnCellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            try
            {
                var gridItem = e.Row as AddressingGridItem;
                if (gridItem?.Assignment == null) return;

                // Update assignment based on changed column
                switch (e.Column.FieldName)
                {
                    case "Address":
                        if (int.TryParse(e.Value?.ToString(), out int newAddress))
                        {
                            gridItem.Assignment.Address = newAddress;
                            gridItem.Assignment.IsManualAddress = true;
                            gridItem.Address = newAddress;
                            gridItem.StatusDescription = gridItem.Assignment.StatusDescription;
                        }
                        break;
                        
                    case "AddressSlots":
                        if (int.TryParse(e.Value?.ToString(), out int newSlots))
                        {
                            gridItem.Assignment.AddressSlots = Math.Max(1, newSlots);
                            gridItem.AddressSlots = gridItem.Assignment.AddressSlots;
                        }
                        break;
                        
                    case "LockState":
                        if (Enum.TryParse<AddressLockState>(e.Value?.ToString(), out var newLockState))
                        {
                            gridItem.Assignment.LockState = newLockState;
                            gridItem.LockState = newLockState.ToString();
                            gridItem.StatusDescription = gridItem.Assignment.StatusDescription;
                        }
                        break;
                }

                // Update validation
                gridItem.ValidationMessage = ValidateDevice(gridItem.Assignment);
                
                // Refresh summary
                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling cell value change: {ex.Message}");
            }
        }

        #endregion

        #region Button Event Handlers

        private void AutoAssign_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = GetAddressingOptions();
                var branchDevices = GetCurrentBranchDevices();
                
                if (!branchDevices.Any())
                {
                    DXMessageBox.Show("No devices found in selected branch.", "Auto Assign", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _addressingService.AutoAssign(branchDevices, options);
                
                RefreshDeviceGrid();
                UpdateSummary();
                
                DXMessageBox.Show($"Auto-assigned addresses for {branchDevices.Count()} devices.\n\n" +
                    "Locked addresses were preserved.\nManual addresses were respected.",
                    "Auto Assign Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error during auto-assignment: {ex.Message}", "Auto Assign Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Resequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show(
                    "This will compact address gaps by moving only Auto devices.\n\n" +
                    "Locked devices will keep their current addresses.\n" +
                    "Manual addresses will be preserved.\n\n" +
                    "Continue with resequencing?",
                    "Confirm Resequencing", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var options = GetAddressingOptions();
                var branchDevices = GetCurrentBranchDevices();
                
                if (!branchDevices.Any())
                {
                    DXMessageBox.Show("No devices found in selected branch.", "Resequence", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _addressingService.Resequence(branchDevices, options);
                
                RefreshDeviceGrid();
                UpdateSummary();
                
                DXMessageBox.Show($"Resequenced addresses for {branchDevices.Count()} devices.\n\n" +
                    "Only Auto devices were moved.\nLocked and manual addresses were preserved.",
                    "Resequencing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error during resequencing: {ex.Message}", "Resequence Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GapFill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = GetAddressingOptions();
                var branchDevices = GetCurrentBranchDevices();
                
                if (!branchDevices.Any())
                {
                    DXMessageBox.Show("No devices found in selected branch.", "Gap Fill", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int unassignedCount = branchDevices.Count(d => d.Address <= 0);
                
                _addressingService.GapFill(branchDevices, options);
                
                RefreshDeviceGrid();
                UpdateSummary();
                
                DXMessageBox.Show($"Gap-filled addresses for {unassignedCount} unassigned devices.",
                    "Gap Fill Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error during gap fill: {ex.Message}", "Gap Fill Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FirstAvailable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = DeviceGrid.GetFocusedRow() as AddressingGridItem;
                if (selectedItem?.Assignment == null)
                {
                    DXMessageBox.Show("Please select a device first.", "First Available", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var options = GetAddressingOptions();
                var branchDevices = GetCurrentBranchDevices();
                
                bool success = _addressingService.FirstAvailableForDevice(selectedItem.Assignment, branchDevices, options);
                
                if (success)
                {
                    RefreshDeviceGrid();
                    UpdateSummary();
                    
                    DXMessageBox.Show($"Assigned device to address {selectedItem.Assignment.Address}.",
                        "First Available Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    int slotsNeeded = _addressingService.GetAddressSlots(selectedItem.Assignment);
                    DXMessageBox.Show($"No contiguous address block available (needs {slotsNeeded} slot{(slotsNeeded > 1 ? "s" : "")}).",
                        "First Available Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error finding first available: {ex.Message}", "First Available Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var branchDevices = GetCurrentBranchDevices();
                
                if (!branchDevices.Any())
                {
                    DXMessageBox.Show("No devices found in selected branch.", "Validate", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var validation = _addressingService.ValidateAddressing(branchDevices);
                
                if (validation.IsValid)
                {
                    var summary = _addressingService.GetAddressRangeSummary(branchDevices);
                    DXMessageBox.Show($"ADDRESS VALIDATION PASSED\n\n{summary}\n\n" +
                        $"Total Devices: {validation.TotalDeviceCount}\n" +
                        $"Locked Devices: {validation.LockedDeviceCount}\n" +
                        $"Auto Devices: {validation.AutoDeviceCount}",
                        "Validation Results", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var conflictMessage = "ADDRESS CONFLICTS DETECTED:\n\n";
                    
                    foreach (var conflict in validation.Conflicts)
                    {
                        conflictMessage += $"• {conflict.Description}\n";
                    }
                    
                    conflictMessage += "\nUse 'Resolve Conflicts' to automatically fix Auto device conflicts.";

                    DXMessageBox.Show(conflictMessage, "Validation Results", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Refresh validation messages in grid
                RefreshDeviceGrid();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error during validation: {ex.Message}", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResolveConflicts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = GetAddressingOptions();
                options.PreserveManual = false; // Allow resolving manual conflicts
                
                var branchDevices = GetCurrentBranchDevices();
                
                if (!branchDevices.Any())
                {
                    DXMessageBox.Show("No devices found in selected branch.", "Resolve Conflicts", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool resolved = _addressingService.ResolveConflicts(branchDevices, options);
                
                RefreshDeviceGrid();
                UpdateSummary();
                
                if (resolved)
                {
                    DXMessageBox.Show("Successfully resolved conflicts.\n\n" +
                        "Auto devices were re-assigned to available addresses.\n" +
                        "Locked devices were not moved.",
                        "Conflict Resolution Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DXMessageBox.Show("No conflicts were found or all conflicts involve locked devices.",
                        "Conflict Resolution", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error during conflict resolution: {ex.Message}", "Conflict Resolution Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveToRevit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show(
                    "This will write address assignments back to Revit parameters:\n\n" +
                    "• FA_Address (int)\n" +
                    "• FA_AddressLock (\"Auto\"/\"Locked\")\n\n" +
                    "Changes will be made to the Revit model.\n\n" +
                    "Continue?",
                    "Confirm Save to Revit", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Apply pending changes to Revit model
                var syncService = new ModelSyncService(_document, _uiDocument);
                var syncResult = await syncService.ApplyPendingChangesToModel();

                if (syncResult.Success)
                {
                    DXMessageBox.Show(
                        $"Successfully saved {syncResult.UpdatedCount} changes to Revit model.",
                        "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DXMessageBox.Show(
                        $"Failed to save changes: {syncResult.Message}",
                        "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error saving to Revit: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Use 'this.Close()' to close the window
        }

        #endregion

        #region Helper Methods

        private AddressingOptions GetAddressingOptions()
        {
            int startAddress = (int)StartAddressSpinner.Value;
            return new AddressingOptions
            {
                StartAddress = startAddress,
                PreserveManual = PreserveManualChk.IsChecked == true,
                RespectLocks = RespectLocksChk.IsChecked == true,
                GapFill = GapFillChk.IsChecked == true
            };
        }

        private IEnumerable<DeviceAssignment> GetCurrentBranchDevices()
        {
            var assignments = _assignmentStore.DeviceAssignments.Where(a => a.IsAssigned);
            
            if (!string.IsNullOrEmpty(_selectedBranchId) && _selectedBranchId != "All Branches")
            {
                assignments = assignments.Where(a => a.BranchId == _selectedBranchId);
            }

            return assignments;
        }

        private string ValidateDevice(DeviceAssignment assignment)
        {
            var branchDevices = _assignmentStore.DeviceAssignments
                .Where(a => a.BranchId == assignment.BranchId && a.IsAssigned)
                .ToList();

            var validation = _addressingService.ValidateAddressing(branchDevices);
            
            var conflicts = validation.Conflicts
                .Where(c => c.ConflictingDevices.Any(d => d.ElementId == assignment.ElementId))
                .ToList();

            if (conflicts.Any())
            {
                return $"CONFLICT: Address {conflicts.First().Address} occupied by {conflicts.First().ConflictingDevices.Count} devices";
            }

            if (assignment.Address <= 0)
                return "Unassigned";

            return "OK";
        }

        private string GetDeviceLevel(int elementId)
        {
            // In a real implementation, would query Revit element
            return "Level 1";
        }

        private string GetDeviceFamily(int elementId)
        {
            // In a real implementation, would query Revit element
            return "Fire Alarm Device";
        }

        private string GetDeviceType(int elementId)
        {
            // In a real implementation, would query Revit element
            return "Smoke Detector";
        }

        #endregion
    }

    /// <summary>
    /// Grid item for addressing panel
    /// </summary>
    public class AddressingGridItem : INotifyPropertyChanged
    {
        private int _address;
        private int _addressSlots;
        private string _lockState;
        private string _statusDescription;
        private string _validationMessage;

        public DeviceAssignment Assignment { get; set; }
        public int ElementId { get; set; }
        public string Level { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string BranchId { get; set; }

        public int Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged();
                }
            }
        }

        public int AddressSlots
        {
            get => _addressSlots;
            set
            {
                if (_addressSlots != value)
                {
                    _addressSlots = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LockState
        {
            get => _lockState;
            set
            {
                if (_lockState != value)
                {
                    _lockState = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (_statusDescription != value)
                {
                    _statusDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}