using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DevExpress.Mvvm;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services;

namespace Revit_FA_Tools.ViewModels.Addressing
{
    /// <summary>
    /// ViewModel for AddressingPanelWindow with proper MVVM data binding
    /// </summary>
    public class AddressingPanelViewModel : INotifyPropertyChanged
    {
        #region Private Fields
        
        private readonly Document _document;
        private readonly UIDocument _uiDocument;
        private readonly DeviceAddressingService _addressingService;
        private readonly AssignmentStore _assignmentStore;
        private ObservableCollection<AddressingGridItem> _devices;
        private ObservableCollection<string> _branches;
        private string _selectedBranch;
        private string _addressRange = "No devices";
        private string _totalDevices = "0";
        private string _lockedDevices = "0";
        private string _conflicts = "0";
        private bool _hasConflicts;
        private int _startAddress = 1;
        private bool _preserveManual = true;
        private bool _respectLocks = true;
        private bool _gapFill = true;
        
        #endregion

        #region Constructor

        public AddressingPanelViewModel(Document document, UIDocument uiDocument, 
            DeviceAddressingService addressingService, AssignmentStore assignmentStore)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _uiDocument = uiDocument ?? throw new ArgumentNullException(nameof(uiDocument));
            _addressingService = addressingService ?? throw new ArgumentNullException(nameof(addressingService));
            _assignmentStore = assignmentStore ?? throw new ArgumentNullException(nameof(assignmentStore));
            
            _devices = new ObservableCollection<AddressingGridItem>();
            _branches = new ObservableCollection<string>();
            
            InitializeCommands();
            LoadBranches();
        }

        #endregion

        #region Properties - Collections

        public ObservableCollection<AddressingGridItem> Devices
        {
            get => _devices;
            set => SetProperty(ref _devices, value);
        }

        public ObservableCollection<string> Branches
        {
            get => _branches;
            set => SetProperty(ref _branches, value);
        }

        #endregion

        #region Properties - Selected Values

        public string SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetProperty(ref _selectedBranch, value))
                {
                    RefreshDevices();
                }
            }
        }

        public int StartAddress
        {
            get => _startAddress;
            set => SetProperty(ref _startAddress, value);
        }

        public bool PreserveManual
        {
            get => _preserveManual;
            set => SetProperty(ref _preserveManual, value);
        }

        public bool RespectLocks
        {
            get => _respectLocks;
            set => SetProperty(ref _respectLocks, value);
        }

        public bool GapFill
        {
            get => _gapFill;
            set => SetProperty(ref _gapFill, value);
        }

        #endregion

        #region Properties - Summary

        public string AddressRange
        {
            get => _addressRange;
            set => SetProperty(ref _addressRange, value);
        }

        public string TotalDevices
        {
            get => _totalDevices;
            set => SetProperty(ref _totalDevices, value);
        }

        public string LockedDevices
        {
            get => _lockedDevices;
            set => SetProperty(ref _lockedDevices, value);
        }

        public string Conflicts
        {
            get => _conflicts;
            set => SetProperty(ref _conflicts, value);
        }

        public bool HasConflicts
        {
            get => _hasConflicts;
            set => SetProperty(ref _hasConflicts, value);
        }

        #endregion

        #region Commands

        public ICommand AutoAssignCommand { get; private set; }
        public ICommand ResequenceCommand { get; private set; }
        public ICommand GapFillCommand { get; private set; }
        public ICommand FirstAvailableCommand { get; private set; }
        public ICommand ValidateCommand { get; private set; }
        public ICommand ResolveConflictsCommand { get; private set; }
        public ICommand SaveToRevitCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        #endregion

        #region Command Initialization

        private void InitializeCommands()
        {
            AutoAssignCommand = new DelegateCommand(ExecuteAutoAssign);
            ResequenceCommand = new DelegateCommand(ExecuteResequence);
            GapFillCommand = new DelegateCommand(ExecuteGapFill);
            FirstAvailableCommand = new DelegateCommand<AddressingGridItem>(ExecuteFirstAvailable);
            ValidateCommand = new DelegateCommand(ExecuteValidate);
            ResolveConflictsCommand = new DelegateCommand(ExecuteResolveConflicts);
            SaveToRevitCommand = new DelegateCommand(ExecuteSaveToRevit);
            RefreshCommand = new DelegateCommand(RefreshDevices);
        }

        #endregion

        #region Data Loading

        private void LoadBranches()
        {
            try
            {
                var branches = _assignmentStore.DeviceAssignments
                    .Where(a => a.IsAssigned && !string.IsNullOrEmpty(a.BranchId))
                    .Select(a => a.BranchId)
                    .Distinct()
                    .OrderBy(b => b)
                    .ToList();

                Branches.Clear();
                Branches.Add("All Branches");
                foreach (var branch in branches)
                {
                    Branches.Add(branch);
                }

                if (Branches.Count > 1)
                {
                    SelectedBranch = Branches[1]; // Select first actual branch
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading branches: {ex.Message}");
            }
        }

        private void RefreshDevices()
        {
            try
            {
                Devices.Clear();

                var assignments = _assignmentStore.DeviceAssignments
                    .Where(a => a.IsAssigned)
                    .Where(a => string.IsNullOrEmpty(SelectedBranch) || 
                               SelectedBranch == "All Branches" || 
                               a.BranchId == SelectedBranch)
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

                    // Subscribe to property changes
                    gridItem.PropertyChanged += OnGridItemPropertyChanged;

                    Devices.Add(gridItem);
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing devices: {ex.Message}");
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

                    AddressRange = $"{validation.AddressRangeStart}-{validation.AddressRangeEnd}";
                    TotalDevices = validation.TotalDeviceCount.ToString();
                    LockedDevices = validation.LockedDeviceCount.ToString();
                    Conflicts = validation.Conflicts.Count.ToString();
                    HasConflicts = !validation.IsValid;
                }
                else
                {
                    AddressRange = "No devices";
                    TotalDevices = "0";
                    LockedDevices = "0";
                    Conflicts = "0";
                    HasConflicts = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating summary: {ex.Message}");
            }
        }

        #endregion

        #region Command Implementations

        private void ExecuteAutoAssign()
        {
            var options = GetAddressingOptions();
            var branchDevices = GetCurrentBranchDevices();

            if (!branchDevices.Any()) return;

            _addressingService.AutoAssign(branchDevices, options);
            RefreshDevices();
        }

        private void ExecuteResequence()
        {
            var options = GetAddressingOptions();
            var branchDevices = GetCurrentBranchDevices();

            if (!branchDevices.Any()) return;

            _addressingService.Resequence(branchDevices, options);
            RefreshDevices();
        }

        private void ExecuteGapFill()
        {
            var options = GetAddressingOptions();
            var branchDevices = GetCurrentBranchDevices();

            if (!branchDevices.Any()) return;

            _addressingService.GapFill(branchDevices, options);
            RefreshDevices();
        }

        private void ExecuteFirstAvailable(AddressingGridItem selectedItem)
        {
            if (selectedItem?.Assignment == null) return;

            var options = GetAddressingOptions();
            var branchDevices = GetCurrentBranchDevices();

            if (_addressingService.FirstAvailableForDevice(selectedItem.Assignment, branchDevices, options))
            {
                RefreshDevices();
            }
        }

        private void ExecuteValidate()
        {
            var branchDevices = GetCurrentBranchDevices();
            if (!branchDevices.Any()) return;

            var validation = _addressingService.ValidateAddressing(branchDevices);
            
            // Refresh to show validation messages
            RefreshDevices();
        }

        private void ExecuteResolveConflicts()
        {
            var options = GetAddressingOptions();
            options.PreserveManual = false; // Allow resolving manual conflicts

            var branchDevices = GetCurrentBranchDevices();
            if (!branchDevices.Any()) return;

            if (_addressingService.ResolveConflicts(branchDevices, options))
            {
                RefreshDevices();
            }
        }

        private async void ExecuteSaveToRevit()
        {
            try
            {
                var syncService = new ModelSyncService(_document, _uiDocument);
                var result = await syncService.ApplyPendingChangesToModel();

                if (!result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"Save to Revit failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving to Revit: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private AddressingOptions GetAddressingOptions()
        {
            return new AddressingOptions
            {
                StartAddress = StartAddress,
                PreserveManual = PreserveManual,
                RespectLocks = RespectLocks,
                GapFill = GapFill
            };
        }

        private IEnumerable<DeviceAssignment> GetCurrentBranchDevices()
        {
            var assignments = _assignmentStore.DeviceAssignments.Where(a => a.IsAssigned);

            if (!string.IsNullOrEmpty(SelectedBranch) && SelectedBranch != "All Branches")
            {
                assignments = assignments.Where(a => a.BranchId == SelectedBranch);
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
            // TODO: Query from Revit element
            return "Level 1";
        }

        private string GetDeviceFamily(int elementId)
        {
            // TODO: Query from Revit element
            return "Fire Alarm Device";
        }

        private string GetDeviceType(int elementId)
        {
            // TODO: Query from Revit element
            return "Smoke Detector";
        }

        private void OnGridItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is AddressingGridItem gridItem)
            {
                // Handle property changes from grid items
                switch (e.PropertyName)
                {
                    case nameof(AddressingGridItem.Address):
                    case nameof(AddressingGridItem.AddressSlots):
                    case nameof(AddressingGridItem.LockState):
                        UpdateSummary();
                        break;
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}