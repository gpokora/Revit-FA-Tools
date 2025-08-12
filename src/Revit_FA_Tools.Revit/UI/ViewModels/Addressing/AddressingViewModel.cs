using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevExpress.Mvvm;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Services.Addressing;
using Revit_FA_Tools.Services.Integration;

namespace Revit_FA_Tools.ViewModels.Addressing
{
    public class AddressingViewModel : INotifyPropertyChanged
    {
        private readonly ValidationEngine _validationEngine;
        private readonly ParameterMappingIntegrationService _integrationService;
        private string _statusMessage;
        private string _treeSearchText;
        private string _devicePoolSearchText;
        private string _treeFilter = "All Devices";
        private int _selectedDeviceCount;
        private string _validationStatusText = "✅ All Valid";
        
        public AddressingViewModel()
        {
            _validationEngine = new ValidationEngine();
            _integrationService = new ParameterMappingIntegrationService();
            SystemTreeItems = new ObservableCollection<object>();
            AvailableDevices = new ObservableCollection<SmartDeviceNode>();
            
            InitializeCommands();
            LoadSampleDataWithParameterMapping(); // Enhanced with parameter mapping
        }
        
        // Collections
        public ObservableCollection<object> SystemTreeItems { get; set; }
        public ObservableCollection<SmartDeviceNode> AvailableDevices { get; set; }
        
        // Properties
        public string StatusMessage 
        { 
            get => _statusMessage; 
            set => SetProperty(ref _statusMessage, value); 
        }
        
        public string TreeSearchText 
        { 
            get => _treeSearchText; 
            set => SetProperty(ref _treeSearchText, value); 
        }
        
        public string DevicePoolSearchText 
        { 
            get => _devicePoolSearchText; 
            set => SetProperty(ref _devicePoolSearchText, value); 
        }
        
        public string TreeFilter 
        { 
            get => _treeFilter; 
            set => SetProperty(ref _treeFilter, value); 
        }
        
        public int SelectedDeviceCount 
        { 
            get => _selectedDeviceCount; 
            set => SetProperty(ref _selectedDeviceCount, value); 
        }
        
        // Status Properties
        public string AddressUtilizationText
        {
            get
            {
                var totalAddressed = SystemTreeItems.OfType<AddressingCircuit>().Sum(c => c.UsedCapacity);
                var totalCapacity = SystemTreeItems.OfType<AddressingCircuit>().Sum(c => c.MaxAddresses);
                return $"Addresses: {totalAddressed}/{totalCapacity} ({(totalCapacity > 0 ? (double)totalAddressed / totalCapacity * 100 : 0):F1}%)";
            }
        }
        
        public double AddressUtilizationPercentage
        {
            get
            {
                var totalAddressed = SystemTreeItems.OfType<AddressingCircuit>().Sum(c => c.UsedCapacity);
                var totalCapacity = SystemTreeItems.OfType<AddressingCircuit>().Sum(c => c.MaxAddresses);
                return totalCapacity > 0 ? (double)totalAddressed / totalCapacity * 100 : 0;
            }
        }
        
        public bool HasCapacityWarning => AddressUtilizationPercentage > 80;
        public string CapacityWarningText => HasCapacityWarning ? "Warning: Near capacity limit" : "";
        public string ValidationStatusText 
        { 
            get => _validationStatusText; 
            set => SetProperty(ref _validationStatusText, value); 
        }
        public string LastOperationText => "Ready";
        
        /// <summary>
        /// Get enhanced device information for display
        /// </summary>
        public string GetEnhancedDeviceInfo(SmartDeviceNode device)
        {
            if (device?.SourceDevice == null) return "No enhanced data available";
            
            try
            {
                var snapshot = device.SourceDevice;
                var info = new List<string>();
                
                // Basic device info
                info.Add($"Device: {snapshot.FamilyName}");
                info.Add($"Type: {snapshot.TypeName}");
                
                // Enhanced specifications from parameter mapping
                if (snapshot.HasRepositorySpecifications())
                {
                    info.Add($"SKU: {snapshot.GetSKU()}");
                    info.Add($"Manufacturer: {snapshot.GetManufacturer()}");
                }
                
                // Electrical specifications
                if (snapshot.Watts > 0)
                    info.Add($"Power: {snapshot.Watts}W");
                if (snapshot.Amps > 0)
                    info.Add($"Current: {snapshot.Amps:F3}A");
                
                // Device characteristics
                var characteristics = new List<string>();
                if (snapshot.HasStrobe) characteristics.Add($"Strobe ({snapshot.GetCandelaRating()}cd)");
                if (snapshot.HasSpeaker) characteristics.Add("Speaker");
                if (snapshot.IsIsolator) characteristics.Add("Isolator");
                if (snapshot.IsRepeater) characteristics.Add("Repeater");
                
                if (characteristics.Any())
                    info.Add($"Features: {string.Join(", ", characteristics)}");
                
                // Environmental specs
                if (snapshot.IsTTapCompatible())
                    info.Add("T-Tap Compatible");
                
                info.Add($"Mounting: {snapshot.GetMountingType()}");
                info.Add($"Environment: {snapshot.GetEnvironmentalRating()}");
                
                return string.Join(" | ", info);
            }
            catch (Exception ex)
            {
                return $"Error getting device info: {ex.Message}";
            }
        }
        
        // Commands
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }
        public ICommand AutoAssignCommand { get; private set; }
        public ICommand ClearAllCommand { get; private set; }
        public ICommand SaveStateCommand { get; private set; }
        public ICommand LoadStateCommand { get; private set; }
        public ICommand ClearTreeSearchCommand { get; private set; }
        public ICommand RunIntegrationTestsCommand { get; private set; }
        
        private void InitializeCommands()
        {
            UndoCommand = new DelegateCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new DelegateCommand(ExecuteRedo, CanExecuteRedo);
            AutoAssignCommand = new DelegateCommand(ExecuteAutoAssign);
            ClearAllCommand = new DelegateCommand(ExecuteClearAll);
            SaveStateCommand = new DelegateCommand(ExecuteSaveState);
            LoadStateCommand = new DelegateCommand(ExecuteLoadState);
            ClearTreeSearchCommand = new DelegateCommand(ExecuteClearTreeSearch);
            RunIntegrationTestsCommand = new DelegateCommand(ExecuteRunIntegrationTests);
        }
        
        /// <summary>
        /// Load sample data enhanced with parameter mapping integration
        /// </summary>
        private void LoadSampleDataWithParameterMapping()
        {
            // Create sample circuit with devices for testing parameter mapping integration
            var circuit1 = new AddressingCircuit 
            { 
                Name = "Circuit-A", 
                MaxAddresses = 159, 
                SafeCapacityThreshold = 0.8 
            };
            
            // Create sample DeviceSnapshots with realistic data
            var sampleDeviceData = new[]
            {
                new { FamilyName = "SpectrAlert Advance", TypeName = "MT-12127WF-3", Candela = 75, HasStrobe = true, HasSpeaker = true },
                new { FamilyName = "ECO1000 Smoke Detector", TypeName = "ECO1003", Candela = 0, HasStrobe = false, HasSpeaker = false },
                new { FamilyName = "Addressable Manual Pull Station", TypeName = "M901E", Candela = 0, HasStrobe = false, HasSpeaker = false },
            };
            
            var devices = new List<SmartDeviceNode>();
            
            foreach (var (deviceData, index) in sampleDeviceData.Select((d, i) => (d, i)))
            {
                try
                {
                    // Create DeviceSnapshot with parameters
                    var deviceSnapshot = DeviceSnapshotExtensions.CreateWithParameters(
                        elementId: 1000 + index,
                        familyName: deviceData.FamilyName,
                        typeName: deviceData.TypeName,
                        levelName: "Level 1",
                        parameters: deviceData.Candela > 0 ? 
                            new Dictionary<string, object> { ["CANDELA"] = deviceData.Candela } : 
                            new Dictionary<string, object>()
                    );
                    
                    // Process with parameter mapping integration
                    var result = _integrationService.ProcessDeviceComprehensively(deviceSnapshot);
                    
                    // Create SmartDeviceNode with enhanced specifications
                    var smartDevice = result.Success ? result.AddressingNode : new SmartDeviceNode
                    {
                        SourceDevice = deviceSnapshot,
                        DeviceName = deviceData.FamilyName,
                        DeviceType = GetDeviceTypeFromSnapshot(deviceSnapshot),
                        PhysicalPosition = index + 1,
                        ParentCircuit = circuit1
                    };
                    
                    // Ensure circuit reference and position are set
                    smartDevice.ParentCircuit = circuit1;
                    smartDevice.PhysicalPosition = index + 1;
                    
                    devices.Add(smartDevice);
                    circuit1.Devices.Add(smartDevice);
                    
                    System.Diagnostics.Debug.WriteLine($"Parameter Mapping Integration: Created {smartDevice.DeviceName} with enhanced specs");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating enhanced device {deviceData.FamilyName}: {ex.Message}");
                    
                    // Fallback to basic device creation
                    var basicDevice = new SmartDeviceNode 
                    { 
                        DeviceName = deviceData.FamilyName, 
                        DeviceType = deviceData.HasStrobe && deviceData.HasSpeaker ? "Horn Strobe" : 
                                   deviceData.HasStrobe ? "Strobe" : 
                                   deviceData.FamilyName.Contains("Smoke") ? "Smoke Detector" : "Device",
                        PhysicalPosition = index + 1, 
                        ParentCircuit = circuit1 
                    };
                    devices.Add(basicDevice);
                    circuit1.Devices.Add(basicDevice);
                }
            }
            
            // Assign some sample addresses to demonstrate functionality
            try
            {
                if (devices.Count > 0) circuit1.AddressPool.AssignAddress(1, devices[0]);
                if (devices.Count > 1) circuit1.AddressPool.AssignAddress(2, devices[1]);
                // Leave third device unaddressed to show mixed state
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting up sample addresses: {ex.Message}";
            }
            
            SystemTreeItems.Add(circuit1);
            
            // Add available devices with parameter mapping
            var availableDeviceData = new[]
            {
                new { FamilyName = "SpectrAlert Advance", TypeName = "SPSCWL", HasSpeaker = (bool?)true, HasStrobe = (bool?)null, HasHeat = (bool?)null, IsIsolator = (bool?)null },
                new { FamilyName = "SpectrAlert Advance", TypeName = "P2WL", HasSpeaker = (bool?)null, HasStrobe = (bool?)true, HasHeat = (bool?)null, IsIsolator = (bool?)null },
                new { FamilyName = "ECO1000 Heat Detector", TypeName = "ECO1005T", HasSpeaker = (bool?)null, HasStrobe = (bool?)null, HasHeat = (bool?)true, IsIsolator = (bool?)null },
                new { FamilyName = "IDNAC Isolator Module", TypeName = "ISO-6", HasSpeaker = (bool?)null, HasStrobe = (bool?)null, HasHeat = (bool?)null, IsIsolator = (bool?)true },
            };
            
            foreach (var (deviceData, index) in availableDeviceData.Select((d, i) => (d, i)))
            {
                try
                {
                    var deviceSnapshot = DeviceSnapshotExtensions.CreateWithParameters(
                        elementId: 2000 + index,
                        familyName: deviceData.FamilyName,
                        typeName: deviceData.TypeName,
                        levelName: "Available"
                    );
                    
                    var result = _integrationService.ProcessDeviceComprehensively(deviceSnapshot);
                    var availableDevice = result.Success ? result.AddressingNode : new SmartDeviceNode
                    {
                        SourceDevice = deviceSnapshot,
                        DeviceName = deviceData.FamilyName,
                        DeviceType = GetDeviceTypeFromSnapshot(deviceSnapshot)
                    };
                    
                    AvailableDevices.Add(availableDevice);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating available device {deviceData.FamilyName}: {ex.Message}");
                    
                    // Fallback to basic device
                    AvailableDevices.Add(new SmartDeviceNode 
                    { 
                        DeviceName = deviceData.FamilyName, 
                        DeviceType = deviceData.FamilyName.Contains("Speaker") ? "Speaker" :
                                   deviceData.FamilyName.Contains("Strobe") ? "Strobe" :
                                   deviceData.FamilyName.Contains("Heat") ? "Heat Detector" :
                                   deviceData.FamilyName.Contains("Isolator") ? "Isolator" : "Device"
                    });
                }
            }
            
            StatusMessage = "Enhanced sample data loaded with parameter mapping - Integration testing ready";
        }
        
        /// <summary>
        /// Determine device type from DeviceSnapshot
        /// </summary>
        private string GetDeviceTypeFromSnapshot(DeviceSnapshot snapshot)
        {
            if (snapshot.IsIsolator) return "Isolator";
            if (snapshot.IsRepeater) return "Repeater";
            if (snapshot.HasStrobe && snapshot.HasSpeaker) return "Horn Strobe";
            if (snapshot.HasStrobe) return "Strobe";
            if (snapshot.HasSpeaker) return "Speaker";
            if (snapshot.FamilyName.ToUpper().Contains("SMOKE")) return "Smoke Detector";
            if (snapshot.FamilyName.ToUpper().Contains("HEAT")) return "Heat Detector";
            if (snapshot.FamilyName.ToUpper().Contains("PULL")) return "Pull Station";
            return "Device";
        }
        
        private void LoadSampleData()
        {
            // Keep original method for backward compatibility
            LoadSampleDataWithParameterMapping();
        }
        
        // Command Implementations
        private void ExecuteUndo()
        {
            StatusMessage = "Undo functionality - Available in Pro version";
        }
        
        private bool CanExecuteUndo() => false; // Disabled for basic implementation
        
        private void ExecuteRedo()
        {
            StatusMessage = "Redo functionality - Available in Pro version";
        }
        
        private bool CanExecuteRedo() => false; // Disabled for basic implementation
        
        private void ExecuteAutoAssign()
        {
            try
            {
                int devicesAssigned = 0;
                foreach (var circuit in SystemTreeItems.OfType<AddressingCircuit>())
                {
                    circuit.AutoAssignSequential();
                    devicesAssigned += circuit.UsedCapacity;
                }
                
                UpdateUtilizationProperties();
                StatusMessage = $"Auto-assignment complete: {devicesAssigned} devices addressed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Auto-assignment error: {ex.Message}";
            }
        }
        
        private void ExecuteClearAll()
        {
            try
            {
                int devicesCleared = 0;
                foreach (var circuit in SystemTreeItems.OfType<AddressingCircuit>())
                {
                    foreach (var device in circuit.Devices.ToList())
                    {
                        if (device.AssignedAddress.HasValue)
                        {
                            device.ReturnAddressToPool();
                            devicesCleared++;
                        }
                    }
                }
                
                UpdateUtilizationProperties();
                StatusMessage = $"Clear complete: {devicesCleared} addresses returned to pool";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clear addresses error: {ex.Message}";
            }
        }
        
        private void ExecuteSaveState()
        {
            try
            {
                // Placeholder for save functionality
                StatusMessage = "Save state functionality - Available in Pro version";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save error: {ex.Message}";
            }
        }
        
        private void ExecuteLoadState()
        {
            try
            {
                // Placeholder for load functionality  
                StatusMessage = "Load state functionality - Available in Pro version";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load error: {ex.Message}";
            }
        }
        
        private void ExecuteClearTreeSearch()
        {
            TreeSearchText = "";
        }
        
        /// <summary>
        /// Execute comprehensive integration tests
        /// </summary>
        private void ExecuteRunIntegrationTests()
        {
            try
            {
                StatusMessage = "Running integration tests...";
                
                var testService = new IntegrationTestService();
                var results = testService.RunIntegrationTests();
                
                if (results.OverallSuccess)
                {
                    StatusMessage = $"✅ Integration tests PASSED ({results.TotalDuration.TotalMilliseconds:F0}ms) - {results.TestResults.Count(t => t.Success)}/{results.TestResults.Count} tests successful";
                }
                else
                {
                    var failedTests = results.TestResults.Where(t => !t.Success).Select(t => t.TestName);
                    StatusMessage = $"❌ Integration tests FAILED - Issues: {string.Join(", ", failedTests)}";
                }
                
                // Update validation status to show test results
                UpdateValidationStatus();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Integration test error: {ex.Message}";
            }
        }
        
        // CRITICAL: Address assignment method with validation
        public bool TryAssignAddress(SmartDeviceNode device, int address)
        {
            try
            {
                // First validate the assignment
                var validationResult = _validationEngine.ValidateAddressAssignment(address, device);
                
                if (!validationResult.IsValid)
                {
                    StatusMessage = validationResult.ErrorMessage;
                    UpdateValidationStatus();
                    return false;
                }
                
                if (device.ParentCircuit?.AddressPool?.IsAddressAvailable(address) == true)
                {
                    var oldAddress = device.AssignedAddress;
                    if (device.ParentCircuit.AddressPool.AssignAddress(address, device))
                    {
                        StatusMessage = oldAddress.HasValue 
                            ? $"Address changed from {oldAddress} to {address}"
                            : $"Address {address} assigned to {device.DeviceName}";
                        
                        // Show validation warnings if any
                        if (validationResult.Warnings.Any())
                        {
                            StatusMessage += $" - {string.Join(", ", validationResult.Warnings)}";
                        }
                        
                        UpdateUtilizationProperties();
                        UpdateValidationStatus();
                        return true;
                    }
                }
                else
                {
                    StatusMessage = $"Address {address} is not available";
                }
                
                return false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error assigning address: {ex.Message}";
                return false;
            }
        }

        // Update utilization properties
        private void UpdateUtilizationProperties()
        {
            OnPropertyChanged(nameof(AddressUtilizationText));
            OnPropertyChanged(nameof(AddressUtilizationPercentage));
            OnPropertyChanged(nameof(HasCapacityWarning));
            OnPropertyChanged(nameof(CapacityWarningText));
        }
        
        // Update validation status
        private void UpdateValidationStatus()
        {
            try
            {
                var circuits = SystemTreeItems.OfType<AddressingCircuit>().ToList();
                var hasErrors = false;
                var warningCount = 0;
                
                foreach (var circuit in circuits)
                {
                    var validation = _validationEngine.ValidateCircuit(circuit);
                    if (!validation.IsValid)
                    {
                        hasErrors = true;
                    }
                    warningCount += validation.Warnings.Count;
                }
                
                if (hasErrors)
                {
                    ValidationStatusText = "❌ Validation Errors";
                }
                else if (warningCount > 0)
                {
                    ValidationStatusText = $"⚠️ {warningCount} Warning(s)";
                }
                else
                {
                    ValidationStatusText = "✅ All Valid";
                }
            }
            catch (Exception)
            {
                ValidationStatusText = "❓ Validation Error";
            }
        }
        
        // INotifyPropertyChanged implementation
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
    }
}