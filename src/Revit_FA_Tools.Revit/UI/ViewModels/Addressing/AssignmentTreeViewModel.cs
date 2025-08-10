using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevExpress.Mvvm;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services;

namespace Revit_FA_Tools.ViewModels.Tree
{
    /// <summary>
    /// Main view model for the cascading device-panel assignment tree
    /// </summary>
    public class AssignmentTreeViewModel : INotifyPropertyChanged
    {
        private readonly TreeAssignmentService _assignmentService;
        private readonly UndoStack _undoStack;
        private string _filterText = string.Empty;
        private bool _showOnlyViolations = false;
        private int _totalDevices;
        private int _totalViolations;
        private double _selectedBranchLoadA;
        private int _selectedBranchLoadUL;

        public AssignmentTreeViewModel()
        {
            _assignmentService = new TreeAssignmentService();
            _undoStack = new UndoStack();
            
            // Initialize commands
            AddPanelCmd = new DelegateCommand(AddPanel);
            AddBranchCmd = new DelegateCommand(AddBranch, () => SelectedPanel != null);
            AddDeviceCmd = new DelegateCommand(AddDevice, () => SelectedBranch != null);
            RecomputeCmd = new DelegateCommand(RecomputeAndRefresh);
            SaveToRevitCmd = new DelegateCommand(SaveToRevit);
            UndoCmd = new DelegateCommand(Undo, () => _undoStack.CanUndo);
            RedoCmd = new DelegateCommand(Redo, () => _undoStack.CanRedo);
        }

        #region Properties

        public ObservableCollection<PanelNodeVM> Panels { get; } = new ObservableCollection<PanelNodeVM>();

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFiltering();
                }
            }
        }

        public bool ShowOnlyViolations
        {
            get => _showOnlyViolations;
            set
            {
                if (SetProperty(ref _showOnlyViolations, value))
                {
                    ApplyFiltering();
                }
            }
        }

        public int TotalDevices
        {
            get => _totalDevices;
            private set => SetProperty(ref _totalDevices, value);
        }

        public int TotalViolations
        {
            get => _totalViolations;
            private set => SetProperty(ref _totalViolations, value);
        }

        public double SelectedBranchLoadA
        {
            get => _selectedBranchLoadA;
            private set => SetProperty(ref _selectedBranchLoadA, value);
        }

        public int SelectedBranchLoadUL
        {
            get => _selectedBranchLoadUL;
            private set => SetProperty(ref _selectedBranchLoadUL, value);
        }

        public PanelNodeVM SelectedPanel { get; set; }
        public BranchNodeVM SelectedBranch { get; set; }
        public DeviceNodeVM SelectedDevice { get; set; }

        #endregion

        #region Commands

        public ICommand AddPanelCmd { get; }
        public ICommand AddBranchCmd { get; }
        public ICommand AddDeviceCmd { get; }
        public ICommand RecomputeCmd { get; }
        public ICommand SaveToRevitCmd { get; }
        public ICommand UndoCmd { get; }
        public ICommand RedoCmd { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load tree structure from assignment store and analysis results
        /// </summary>
        public void LoadFrom(List<DeviceAssignment> assignments, ComprehensiveAnalysisResults results)
        {
            try
            {
                Panels.Clear();

                if (assignments == null || !assignments.Any())
                {
                    return;
                }

                // Group devices by panel, then by branch
                var panelGroups = assignments
                    .Where(a => a.IsAssigned)
                    .GroupBy(a => a.PanelId)
                    .OrderBy(g => g.Key);

                foreach (var panelGroup in panelGroups)
                {
                    var panelVM = CreatePanelVM(panelGroup.Key, panelGroup, results);
                    Panels.Add(panelVM);
                }

                UpdateSummaryStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading assignment tree: {ex.Message}");
            }
        }

        /// <summary>
        /// Recompute loads and refresh display after changes
        /// </summary>
        public void RecomputeAndRefresh()
        {
            try
            {
                // Recompute loads for all panels and branches
                foreach (var panel in Panels)
                {
                    RecomputePanelMetrics(panel);
                    
                    foreach (var branch in panel.Branches)
                    {
                        RecomputeBranchMetrics(branch);
                    }
                }

                UpdateSummaryStatistics();
                OnPropertyChanged(nameof(Panels));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recomputing assignment tree: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle device drag-drop operation
        /// </summary>
        public bool TryMoveDevice(string deviceId, string targetBranchId)
        {
            try
            {
                var changeSet = _assignmentService.MoveDevice(deviceId, targetBranchId);
                if (changeSet.IsValid)
                {
                    _undoStack.Push(changeSet);
                    ApplyChangeSet(changeSet);
                    RecomputeAndRefresh();
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handle branch drag-drop operation
        /// </summary>
        public bool TryMoveBranch(string branchId, string targetPanelId)
        {
            try
            {
                var changeSet = _assignmentService.MoveBranch(branchId, targetPanelId);
                if (changeSet.IsValid)
                {
                    _undoStack.Push(changeSet);
                    ApplyChangeSet(changeSet);
                    RecomputeAndRefresh();
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving branch: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        private PanelNodeVM CreatePanelVM(string panelId, IGrouping<string, DeviceAssignment> panelGroup, ComprehensiveAnalysisResults results)
        {
            var panelVM = new PanelNodeVM
            {
                Id = panelId,
                Name = $"Panel {panelId}",
                Level = "System"
            };

            // Group devices by branch
            var branchGroups = panelGroup.GroupBy(a => a.BranchId).OrderBy(g => g.Key);
            
            foreach (var branchGroup in branchGroups)
            {
                var branchVM = CreateBranchVM(branchGroup.Key, branchGroup, results);
                panelVM.Branches.Add(branchVM);
            }

            RecomputePanelMetrics(panelVM);
            return panelVM;
        }

        private BranchNodeVM CreateBranchVM(string branchId, IGrouping<string, DeviceAssignment> branchGroup, ComprehensiveAnalysisResults results)
        {
            var branchVM = new BranchNodeVM
            {
                Id = branchId,
                Name = $"Branch {branchId}",
                RiserZone = branchGroup.FirstOrDefault()?.RiserZone ?? "Unknown"
            };

            // Add devices to branch
            foreach (var assignment in branchGroup.OrderBy(a => a.Address))
            {
                var deviceVM = CreateDeviceVM(assignment, results);
                branchVM.Devices.Add(deviceVM);
            }

            // Calculate address range
            if (branchVM.Devices.Any())
            {
                branchVM.AddressStart = branchVM.Devices.Min(d => d.Address);
                branchVM.AddressEnd = branchVM.Devices.Max(d => d.Address + d.AddressSlots - 1);
            }

            RecomputeBranchMetrics(branchVM);
            return branchVM;
        }

        private DeviceNodeVM CreateDeviceVM(DeviceAssignment assignment, ComprehensiveAnalysisResults results)
        {
            // Try to find device snapshot in results (simplified lookup)
            var deviceSnapshot = FindDeviceSnapshot(assignment.ElementId, results);

            return new DeviceNodeVM
            {
                Id = assignment.ElementId.ToString(),
                ElementIdInt = assignment.ElementId,
                Name = deviceSnapshot?.Description ?? $"Device {assignment.ElementId}",
                Level = deviceSnapshot?.LevelName ?? "Unknown",
                Family = deviceSnapshot?.FamilyName ?? "Unknown",
                Type = deviceSnapshot?.TypeName ?? "Unknown",
                Address = assignment.Address,
                AddressSlots = assignment.AddressSlots,
                CurrentA = deviceSnapshot?.Amps ?? 0,
                UnitLoads = deviceSnapshot?.UnitLoads ?? 1,
                LockState = AddressLockState.Auto // Default state
            };
        }

        private DeviceSnapshot FindDeviceSnapshot(int elementId, ComprehensiveAnalysisResults results)
        {
            // In a real implementation, this would search through the results
            // For now, return null as results structure needs device snapshot access
            return null;
        }

        private void RecomputePanelMetrics(PanelNodeVM panel)
        {
            var config = ConfigurationService.Current;
            
            panel.CurrentA = panel.Branches.Sum(b => b.CurrentA);
            panel.UnitLoads = panel.Branches.Sum(b => b.UnitLoads);
            
            // Calculate cabinet requirements
            var idnacsNeeded = Math.Max(
                Math.Ceiling(panel.CurrentA / config.Capacity.IdnacAlarmCurrentLimitA),
                Math.Ceiling((double)panel.UnitLoads / config.Capacity.IdnacStandbyUnitLoadLimit));
            
            // Simplified cabinet calculation
            panel.CabinetBays = (int)Math.Ceiling(idnacsNeeded / 3.0); // 3 IDNACs per bay
            panel.BlocksUsed = panel.Branches.Count; // Simplified
            panel.BlocksAvail = (panel.CabinetBays * 4) - panel.BlocksUsed; // 4 blocks per bay
            
            // Calculate spare capacity
            var spareCurrentLimit = config.Capacity.IdnacAlarmCurrentLimitA * (1 - config.Spare.SpareFractionDefault);
            var spareULLimit = config.Capacity.IdnacStandbyUnitLoadLimit * (1 - config.Spare.SpareFractionDefault);
            
            var currentUtilization = panel.CurrentA / (idnacsNeeded * config.Capacity.IdnacAlarmCurrentLimitA);
            var ulUtilization = panel.UnitLoads / (idnacsNeeded * config.Capacity.IdnacStandbyUnitLoadLimit);
            
            panel.SparePct = (1 - Math.Max(currentUtilization, ulUtilization)) * 100;
            
            // Determine limiting factor
            if (panel.CurrentA > spareCurrentLimit)
            {
                panel.Limiter = "Current";
                panel.HasViolation = true;
            }
            else if (panel.UnitLoads > spareULLimit)
            {
                panel.Limiter = "UL";
                panel.HasViolation = true;
            }
            else
            {
                panel.Limiter = "";
                panel.HasViolation = false;
            }
        }

        private void RecomputeBranchMetrics(BranchNodeVM branch)
        {
            var config = ConfigurationService.Current;
            
            branch.CurrentA = branch.Devices.Sum(d => d.CurrentA);
            branch.UnitLoads = branch.Devices.Sum(d => d.UnitLoads);
            
            // Apply spare capacity limits
            var spareCurrentLimit = config.Capacity.IdnacAlarmCurrentLimitA * (1 - config.Spare.SpareFractionDefault);
            var spareULLimit = config.Capacity.IdnacStandbyUnitLoadLimit * (1 - config.Spare.SpareFractionDefault);
            
            // Calculate spare percentage
            var currentUtilization = branch.CurrentA / config.Capacity.IdnacAlarmCurrentLimitA;
            var ulUtilization = (double)branch.UnitLoads / config.Capacity.IdnacStandbyUnitLoadLimit;
            
            branch.SparePct = (1 - Math.Max(currentUtilization, ulUtilization)) * 100;
            
            // Check for violations including addressing conflicts
            var hasAddressingViolation = CheckAddressingViolations(branch);
            
            if (branch.CurrentA > spareCurrentLimit)
            {
                branch.Limiter = "Current";
                branch.HasViolation = true;
            }
            else if (branch.UnitLoads > spareULLimit)
            {
                branch.Limiter = "UL";
                branch.HasViolation = true;
            }
            else if (branch.Devices.Count > 127) // Max devices per IDNAC
            {
                branch.Limiter = "Devices";
                branch.HasViolation = true;
            }
            else if (hasAddressingViolation)
            {
                branch.Limiter = "Addressing";
                branch.HasViolation = true;
            }
            else
            {
                branch.Limiter = "";
                branch.HasViolation = false;
            }
            
            // Simplified voltage drop calculation (would need actual wire routing)
            branch.VoltageDropPct = Math.Min(branch.Devices.Count * 0.1, 10.0); // Placeholder
        }

        private void ApplyFiltering()
        {
            // In a real implementation, this would apply CollectionViewSource filtering
            // For now, just trigger property change to refresh display
            OnPropertyChanged(nameof(Panels));
        }

        private void UpdateSummaryStatistics()
        {
            TotalDevices = Panels.SelectMany(p => p.Branches).SelectMany(b => b.Devices).Count();
            TotalViolations = Panels.Count(p => p.HasViolation) + 
                             Panels.SelectMany(p => p.Branches).Count(b => b.HasViolation);
            
            if (SelectedBranch != null)
            {
                SelectedBranchLoadA = SelectedBranch.CurrentA;
                SelectedBranchLoadUL = SelectedBranch.UnitLoads;
            }
        }

        private void ApplyChangeSet(ChangeSet changeSet)
        {
            // Apply the changes from the changeset to update the tree structure
            // This would involve moving nodes, updating assignments, etc.
            // Implementation depends on the specific change type
        }

        private bool CheckAddressingViolations(BranchNodeVM branch)
        {
            try
            {
                var addressingService = new DeviceAddressingService();
                // Note: AssignmentStore not available - using empty assignments for now
                var assignments = new List<DeviceAssignment>();
                
                // Get assignments for this branch
                var branchAssignments = assignments
                    .Where(a => a.BranchId == branch.Id && a.IsAssigned);
                
                if (!branchAssignments.Any())
                    return false;
                
                // Validate addressing for this branch
                var validation = addressingService.ValidateAddressing(branchAssignments);
                return !validation.IsValid;
            }
            catch
            {
                // If addressing service is not available or errors occur, don't flag violations
                return false;
            }
        }

        private void AddPanel()
        {
            var panelId = $"P{Panels.Count + 1:D2}";
            var changeSet = _assignmentService.AddPanel(panelId);
            
            if (changeSet.IsValid)
            {
                _undoStack.Push(changeSet);
                
                var newPanel = new PanelNodeVM
                {
                    Id = panelId,
                    Name = $"Panel {panelId}",
                    Level = "System"
                };
                
                Panels.Add(newPanel);
                RecomputeAndRefresh();
            }
        }

        private void AddBranch()
        {
            if (SelectedPanel == null) return;
            
            var branchId = $"{SelectedPanel.Id}-B{SelectedPanel.Branches.Count + 1:D2}";
            var changeSet = _assignmentService.AddBranch(SelectedPanel.Id, branchId);
            
            if (changeSet.IsValid)
            {
                _undoStack.Push(changeSet);
                
                var newBranch = new BranchNodeVM
                {
                    Id = branchId,
                    Name = $"Branch {branchId}",
                    RiserZone = "New"
                };
                
                SelectedPanel.Branches.Add(newBranch);
                RecomputeAndRefresh();
            }
        }

        private void AddDevice()
        {
            if (SelectedBranch == null) return;
            
            // This would typically open a device selection dialog
            // For now, create a placeholder device
            var deviceSnapshot = new DeviceSnapshot(
                9999, // Placeholder element ID
                "Unknown",
                "Generic Device",
                "Placeholder",
                1.0, // Watts
                0.1, // Amps
                1,   // Unit loads
                false, false, false, false
            );
            
            var changeSet = _assignmentService.InsertDevice(SelectedBranch.Id, deviceSnapshot, SelectedBranch.Devices.Count);
            
            if (changeSet.IsValid)
            {
                _undoStack.Push(changeSet);
                // Add device to UI
                RecomputeAndRefresh();
            }
        }

        private void SaveToRevit()
        {
            // This would use ExternalEventBridge to update Revit parameters
            // For now, just show a message
            System.Diagnostics.Debug.WriteLine("Save to Revit not yet implemented");
        }

        private void Undo()
        {
            if (_undoStack.CanUndo)
            {
                _undoStack.Undo();
                RecomputeAndRefresh();
            }
        }

        private void Redo()
        {
            if (_undoStack.CanRedo)
            {
                _undoStack.Redo();
                RecomputeAndRefresh();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Base class for all tree node view models
    /// </summary>
    public abstract class BaseNodeVM : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _level = string.Empty;
        private double _currentA;
        private int _unitLoads;
        private double _voltageDropPct;
        private double _sparePct;
        private string _limiter = string.Empty;
        private bool _hasViolation;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }

        public double CurrentA
        {
            get => _currentA;
            set => SetProperty(ref _currentA, value);
        }

        public int UnitLoads
        {
            get => _unitLoads;
            set => SetProperty(ref _unitLoads, value);
        }

        public double VoltageDropPct
        {
            get => _voltageDropPct;
            set => SetProperty(ref _voltageDropPct, value);
        }

        public double SparePct
        {
            get => _sparePct;
            set => SetProperty(ref _sparePct, value);
        }

        public string Limiter
        {
            get => _limiter;
            set => SetProperty(ref _limiter, value);
        }

        public bool HasViolation
        {
            get => _hasViolation;
            set => SetProperty(ref _hasViolation, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Panel node view model
    /// </summary>
    public class PanelNodeVM : BaseNodeVM
    {
        private int _cabinetBays;
        private int _blocksUsed;
        private int _blocksAvail;

        public ObservableCollection<BranchNodeVM> Branches { get; } = new ObservableCollection<BranchNodeVM>();

        public int CabinetBays
        {
            get => _cabinetBays;
            set => SetProperty(ref _cabinetBays, value);
        }

        public int BlocksUsed
        {
            get => _blocksUsed;
            set => SetProperty(ref _blocksUsed, value);
        }

        public int BlocksAvail
        {
            get => _blocksAvail;
            set => SetProperty(ref _blocksAvail, value);
        }
    }

    /// <summary>
    /// Branch node view model  
    /// </summary>
    public class BranchNodeVM : BaseNodeVM
    {
        private string _riserZone = string.Empty;
        private int _addressStart;
        private int _addressEnd;

        public string RiserZone
        {
            get => _riserZone;
            set => SetProperty(ref _riserZone, value);
        }

        public ObservableCollection<DeviceNodeVM> Devices { get; } = new ObservableCollection<DeviceNodeVM>();

        public int AddressStart
        {
            get => _addressStart;
            set => SetProperty(ref _addressStart, value);
        }

        public int AddressEnd
        {
            get => _addressEnd;
            set => SetProperty(ref _addressEnd, value);
        }
    }

    /// <summary>
    /// Device node view model
    /// </summary>
    public class DeviceNodeVM : BaseNodeVM
    {
        private int _elementIdInt;
        private string _family = string.Empty;
        private string _type = string.Empty;
        private int _address;
        private int _addressSlots = 1;
        private AddressLockState _lockState;

        public int ElementIdInt
        {
            get => _elementIdInt;
            set => SetProperty(ref _elementIdInt, value);
        }

        public string Family
        {
            get => _family;
            set => SetProperty(ref _family, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public int Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public int AddressSlots
        {
            get => _addressSlots;
            set => SetProperty(ref _addressSlots, value);
        }

        public AddressLockState LockState
        {
            get => _lockState;
            set => SetProperty(ref _lockState, value);
        }
    }

    /// <summary>
    /// Address lock state enumeration
    /// </summary>
    // Using AddressLockState from Models.DeviceModels

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }
}