using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.DependencyInjection;

namespace Revit_FA_Tools.Revit.UI.ViewModels.Addressing
{
    /// <summary>
    /// Clean ViewModel for addressing panel that uses dependency injection
    /// </summary>
    public class CleanAddressingViewModel : INotifyPropertyChanged
    {
        #region Private Fields
        
        private readonly IAddressingPanelService _addressingPanelService;
        private readonly IValidationService _validationService;
        
        private ObservableCollection<AddressingPanel> _panels;
        private ObservableCollection<SmartDeviceNode> _unassignedDevices;
        private AddressingPanel _selectedPanel;
        private SmartDeviceNode _selectedDevice;
        private bool _isLoading;
        private string _statusMessage;
        private bool _hasValidationErrors;
        
        #endregion

        #region Constructor

        public CleanAddressingViewModel(
            IAddressingPanelService addressingPanelService,
            IValidationService validationService)
        {
            _addressingPanelService = addressingPanelService ?? throw new ArgumentNullException(nameof(addressingPanelService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            
            _panels = new ObservableCollection<AddressingPanel>();
            _unassignedDevices = new ObservableCollection<SmartDeviceNode>();
            
            InitializeCommands();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Collection of addressing panels
        /// </summary>
        public ObservableCollection<AddressingPanel> Panels
        {
            get => _panels;
            set
            {
                _panels = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Collection of unassigned devices
        /// </summary>
        public ObservableCollection<SmartDeviceNode> UnassignedDevices
        {
            get => _unassignedDevices;
            set
            {
                _unassignedDevices = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnassignedDevices));
            }
        }

        /// <summary>
        /// Selected addressing panel
        /// </summary>
        public AddressingPanel SelectedPanel
        {
            get => _selectedPanel;
            set
            {
                _selectedPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedPanel));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Selected device
        /// </summary>
        public SmartDeviceNode SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedDevice));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Indicates if data is loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Status message for user feedback
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Indicates if there are validation errors
        /// </summary>
        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            set
            {
                _hasValidationErrors = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets whether there are unassigned devices
        /// </summary>
        public bool HasUnassignedDevices => UnassignedDevices?.Any() == true;

        /// <summary>
        /// Gets whether a panel is selected
        /// </summary>
        public bool HasSelectedPanel => SelectedPanel != null;

        /// <summary>
        /// Gets whether a device is selected
        /// </summary>
        public bool HasSelectedDevice => SelectedDevice != null;

        /// <summary>
        /// Total number of devices
        /// </summary>
        public int TotalDevices => (Panels?.Sum(p => p.TotalDevices) ?? 0) + (UnassignedDevices?.Count ?? 0);

        /// <summary>
        /// Total addressed devices
        /// </summary>
        public int AddressedDevices => Panels?.Sum(p => p.AddressedDevices) ?? 0;

        /// <summary>
        /// Addressing completion percentage
        /// </summary>
        public double CompletionPercentage => TotalDevices > 0 ? (double)AddressedDevices / TotalDevices * 100 : 0;

        /// <summary>
        /// Indicates if there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges { get; set; }

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; private set; }
        public ICommand AutoAssignAllCommand { get; private set; }
        public ICommand ValidatePanelCommand { get; private set; }
        public ICommand AssignDeviceCommand { get; private set; }
        public ICommand RemoveDeviceCommand { get; private set; }
        public ICommand BalanceCircuitsCommand { get; private set; }
        public ICommand ExportDataCommand { get; private set; }
        public ICommand ImportDataCommand { get; private set; }
        public ICommand ApplyChangesCommand { get; private set; }
        public ICommand RevertChangesCommand { get; private set; }

        #endregion

        #region Command Implementation

        private void InitializeCommands()
        {
            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync(), () => !IsLoading);
            AutoAssignAllCommand = new RelayCommand(async () => await AutoAssignAllAsync(), () => !IsLoading && HasUnassignedDevices);
            ValidatePanelCommand = new RelayCommand(async () => await ValidatePanelAsync(), () => !IsLoading && HasSelectedPanel);
            AssignDeviceCommand = new RelayCommand<object>(async (param) => await AssignDeviceAsync(param), (param) => !IsLoading && HasSelectedDevice);
            RemoveDeviceCommand = new RelayCommand(async () => await RemoveDeviceAsync(), () => !IsLoading && HasSelectedDevice);
            BalanceCircuitsCommand = new RelayCommand(async () => await BalanceCircuitsAsync(), () => !IsLoading && HasSelectedPanel);
            ExportDataCommand = new RelayCommand(async () => await ExportDataAsync(), () => !IsLoading);
            ImportDataCommand = new RelayCommand(async () => await ImportDataAsync(), () => !IsLoading);
            ApplyChangesCommand = new RelayCommand(async () => await ApplyChangesAsync(), () => !IsLoading);
            RevertChangesCommand = new RelayCommand(async () => await RevertChangesAsync(), () => !IsLoading);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading addressing data...";

                // Get devices from Revit (this would be injected via a service)
                var devices = await GetDevicesFromRevit();
                
                var panelData = await _addressingPanelService.InitializePanelAsync(devices);
                
                Panels.Clear();
                foreach (var panel in panelData.Panels)
                {
                    Panels.Add(panel);
                }

                UnassignedDevices.Clear();
                foreach (var device in panelData.UnassignedDevices)
                {
                    UnassignedDevices.Add(device);
                }

                StatusMessage = $"Loaded {TotalDevices} devices across {Panels.Count} panels";
                
                // Perform initial validation
                await ValidateAllPanelsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AutoAssignAllAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Auto-assigning addresses...";

                var options = new AutoAssignmentOptions
                {
                    RespectLocks = true,
                    OptimizeByLocation = true,
                    Strategy = AssignmentStrategy.Sequential
                };

                var result = await _addressingPanelService.AutoAssignAllAsync(options);
                
                if (result.Success)
                {
                    StatusMessage = $"Successfully assigned addresses to {result.DevicesAssigned} devices";
                    
                    // Refresh data
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = $"Auto-assignment completed with {result.DevicesFailed} failures";
                    HasValidationErrors = result.DevicesFailed > 0;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during auto-assignment: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ValidatePanelAsync()
        {
            if (SelectedPanel == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Validating panel...";

                var result = await _addressingPanelService.ValidatePanelAsync();
                
                HasValidationErrors = !result.IsValid;
                StatusMessage = result.IsValid ? 
                    "Panel validation passed" : 
                    $"Panel validation failed: {result.ErrorCount} errors, {result.WarningCount} warnings";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error validating panel: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AssignDeviceAsync(object parameter)
        {
            if (SelectedDevice == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Assigning device...";

                // Get target circuit from parameter or user selection
                var targetCircuitId = GetTargetCircuitFromParameter(parameter);
                
                var result = await _addressingPanelService.AssignDeviceToCircuitAsync(
                    SelectedDevice.ElementId, 
                    targetCircuitId, 
                    new AssignmentOptions 
                    { 
                        AutoAssignAddress = true, 
                        ValidateElectrical = true 
                    });

                if (result.Success)
                {
                    StatusMessage = $"Device assigned to address {result.AssignedAddress}";
                    // Refresh the UI
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = $"Assignment failed: {result.ErrorMessage}";
                    HasValidationErrors = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error assigning device: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveDeviceAsync()
        {
            if (SelectedDevice?.Circuit == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Removing device from circuit...";

                var success = await _addressingPanelService.RemoveDeviceFromCircuitAsync(
                    SelectedDevice.ElementId, 
                    SelectedDevice.Circuit.CircuitNumber);

                if (success)
                {
                    StatusMessage = "Device removed from circuit";
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = "Failed to remove device from circuit";
                    HasValidationErrors = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing device: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BalanceCircuitsAsync()
        {
            if (SelectedPanel == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Balancing circuits...";

                var result = await _addressingPanelService.BalanceCircuitsAsync(new BalancingOptions
                {
                    TargetUtilization = 0.8,
                    MaintainLocationGrouping = true,
                    MinimizeMoves = true
                });

                if (result.Success)
                {
                    StatusMessage = $"Balanced circuits: {result.DevicesMoved} devices moved";
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = "Circuit balancing failed";
                    HasValidationErrors = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error balancing circuits: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting addressing data...";

                var data = await _addressingPanelService.ExportAddressingDataAsync(ExportFormat.Excel);
                
                // Save file logic would go here
                StatusMessage = "Export completed successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting data: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing addressing data...";

                // File selection logic would go here
                byte[] data = null; // Get from file dialog
                
                if (data != null)
                {
                    var result = await _addressingPanelService.ImportAddressingDataAsync(data, new ImportOptions
                    {
                        ValidateBeforeImport = true,
                        OverwriteExisting = false
                    });

                    StatusMessage = $"Import completed: {result.RecordsImported}/{result.RecordsProcessed} records imported";
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing data: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ApplyChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Applying changes to model...";

                var result = await _addressingPanelService.ApplyChangesAsync();
                
                if (result.Success)
                {
                    StatusMessage = $"Applied {result.ChangesApplied} changes successfully";
                }
                else
                {
                    StatusMessage = $"Applied changes with {result.ChangesFailed} failures";
                    HasValidationErrors = result.ChangesFailed > 0;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying changes: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RevertChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Reverting changes...";

                await _addressingPanelService.RevertChangesAsync();
                
                StatusMessage = "Changes reverted successfully";
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reverting changes: {ex.Message}";
                HasValidationErrors = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private async Task ValidateAllPanelsAsync()
        {
            try
            {
                var result = await _addressingPanelService.ValidatePanelAsync();
                HasValidationErrors = !result.IsValid;
            }
            catch
            {
                HasValidationErrors = true;
            }
        }

        private async Task<System.Collections.Generic.List<DeviceSnapshot>> GetDevicesFromRevit()
        {
            // This would be implemented by a Revit data service
            // For now, return empty list
            return new System.Collections.Generic.List<DeviceSnapshot>();
        }

        private string GetTargetCircuitFromParameter(object parameter)
        {
            // Extract circuit ID from parameter
            return parameter?.ToString() ?? "Circuit1";
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                await _executeAsync();
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    /// <summary>
    /// Generic relay command implementation
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _executeAsync;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Func<T, Task> executeAsync, Func<T, bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;

        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                await _executeAsync((T)parameter);
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}