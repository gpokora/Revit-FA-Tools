using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.UnitOfWork;
using Revit_FA_Tools.Models;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using DeviceSnapshot = Revit_FA_Tools.Models.DeviceSnapshot;
using AddressingValidationResult = Revit_FA_Tools.Core.Models.Addressing.ValidationResult;
using AddressingValidationSeverity = Revit_FA_Tools.Core.Models.Addressing.ValidationSeverity;
using InterfaceValidationSeverity = Revit_FA_Tools.Core.Services.Interfaces.ValidationSeverity;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Addressing panel service that encapsulates all addressing panel business logic
    /// </summary>
    public class AddressingPanelService : IAddressingPanelService
    {
        private readonly IAddressingService _addressingService;
        private readonly IValidationService _validationService;
        private readonly IParameterMappingService _parameterMappingService;
        private readonly IAssignmentService _assignmentService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly FireAlarmConfiguration _configuration;
        private readonly Dictionary<string, AddressingPanel> _panelCache;

        public AddressingPanelService(
            IAddressingService addressingService,
            IValidationService validationService,
            IParameterMappingService parameterMappingService,
            IAssignmentService assignmentService,
            IUnitOfWork unitOfWork,
            FireAlarmConfiguration configuration)
        {
            _addressingService = addressingService ?? throw new ArgumentNullException(nameof(addressingService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _parameterMappingService = parameterMappingService ?? throw new ArgumentNullException(nameof(parameterMappingService));
            _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _panelCache = new Dictionary<string, AddressingPanel>();
        }

        public async Task<AddressingPanelData> InitializePanelAsync(IEnumerable<DeviceSnapshot> devices)
        {
            if (devices == null)
                throw new ArgumentNullException(nameof(devices));

            try
            {
                _unitOfWork.BeginTransaction();

                var deviceList = devices.ToList();
                var panelData = new AddressingPanelData();

                // Enhance devices with parameter mapping
                var enhancedDevices = new List<DeviceSnapshot>();
                foreach (var device in deviceList)
                {
                    var enhanced = await _parameterMappingService.EnhanceDeviceAsync(device);
                    enhancedDevices.Add(enhanced);
                }

                // Group devices by panel/circuit
                var devicesByPanel = GroupDevicesByPanel(enhancedDevices);

                // Create addressing panels
                foreach (var panelGroup in devicesByPanel)
                {
                    var panel = await CreateAddressingPanel(panelGroup.Key, panelGroup.Value);
                    panelData.Panels.Add(panel);
                    _panelCache[panel.PanelId] = panel;
                }

                // Identify unassigned devices
                panelData.UnassignedDevices = enhancedDevices
                    .Where(d => string.IsNullOrEmpty(d.GetCircuitNumber()))
                    .Select(CreateSmartDeviceNode)
                    .ToList();

                // Calculate statistics
                panelData.TotalDevices = enhancedDevices.Count;
                panelData.AddressedDevices = enhancedDevices.Count(d => !string.IsNullOrEmpty(d.GetAddress()));
                panelData.UnaddressedDevices = panelData.TotalDevices - panelData.AddressedDevices;

                // Add metadata
                panelData.Metadata["InitializationTime"] = DateTime.Now;
                panelData.Metadata["DeviceEnhancement"] = "Applied parameter mapping";
                panelData.Metadata["PanelCount"] = panelData.Panels.Count;

                await _unitOfWork.CommitAsync();
                return panelData;
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }

        public async Task<AssignmentResult> AssignDeviceToCircuitAsync(string deviceId, string circuitId, AssignmentOptions options)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));
            if (string.IsNullOrEmpty(circuitId))
                throw new ArgumentException("Circuit ID cannot be empty", nameof(circuitId));

            options ??= new AssignmentOptions();

            try
            {
                _unitOfWork.BeginTransaction();

                // Find the device and circuit
                var device = await FindDeviceAsync(deviceId);
                var circuit = await FindCircuitAsync(circuitId);

                if (device == null)
                {
                    return new AssignmentResult
                    {
                        Success = false,
                        DeviceId = deviceId,
                        ErrorMessage = "Device not found"
                    };
                }

                if (circuit == null)
                {
                    return new AssignmentResult
                    {
                        Success = false,
                        DeviceId = deviceId,
                        CircuitId = circuitId,
                        ErrorMessage = "Circuit not found"
                    };
                }

                // Validate assignment
                if (options.ValidateElectrical)
                {
                    var validation = await ValidateDeviceToCircuitAssignment(device, circuit);
                    if (!validation.IsValid)
                    {
                        return new AssignmentResult
                        {
                            Success = false,
                            DeviceId = deviceId,
                            CircuitId = circuitId,
                            ErrorMessage = string.Join("; ", validation.Messages.Select(m => m.Message)),
                            Warnings = validation.Messages.Where(m => m.Severity == InterfaceValidationSeverity.Warning).Select(m => m.Message).ToList()
                        };
                    }
                }

                // Assign address if requested
                string assignedAddress = null;
                if (options.AutoAssignAddress)
                {
                    var availableAddress = _addressingService.GetAvailableAddresses(circuit).FirstOrDefault();
                    if (availableAddress > 0)
                    {
                        if (_addressingService.ReserveAddress(circuit, availableAddress))
                        {
                            assignedAddress = availableAddress.ToString();
                            device.Address = assignedAddress;
                        }
                    }
                }

                // Update device assignment
                device.Circuit = circuit;
                circuit.Devices.Add(device);
                circuit.UpdateUtilization();

                _unitOfWork.RegisterModified(device);
                _unitOfWork.RegisterModified(circuit);

                await _unitOfWork.CommitAsync();

                return new AssignmentResult
                {
                    Success = true,
                    DeviceId = deviceId,
                    CircuitId = circuitId,
                    AssignedAddress = assignedAddress
                };
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                return new AssignmentResult
                {
                    Success = false,
                    DeviceId = deviceId,
                    CircuitId = circuitId,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> RemoveDeviceFromCircuitAsync(string deviceId, string circuitId)
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(circuitId))
                return false;

            try
            {
                _unitOfWork.BeginTransaction();

                var device = await FindDeviceAsync(deviceId);
                var circuit = await FindCircuitAsync(circuitId);

                if (device?.Circuit?.CircuitNumber == circuitId)
                {
                    // Release the address
                    if (int.TryParse(device.Address, out int address))
                    {
                        _addressingService.ReleaseAddress(circuit, address);
                    }

                    // Remove from circuit
                    circuit.Devices.Remove(device);
                    device.Circuit = null;
                    device.Address = "";
                    circuit.UpdateUtilization();

                    _unitOfWork.RegisterModified(device);
                    _unitOfWork.RegisterModified(circuit);

                    await _unitOfWork.CommitAsync();
                    return true;
                }

                return false;
            }
            catch
            {
                _unitOfWork.Rollback();
                return false;
            }
        }

        public async Task<UpdateResult> UpdateDeviceAddressAsync(string deviceId, string newAddress, bool validateFirst = true)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

            try
            {
                var device = await FindDeviceAsync(deviceId);
                if (device == null)
                {
                    return new UpdateResult
                    {
                        Success = false,
                        ErrorMessage = "Device not found"
                    };
                }

                var oldAddress = device.Address;

                // Validate new address if requested
                if (validateFirst && device.Circuit != null)
                {
                    if (int.TryParse(newAddress, out int addressValue))
                    {
                        if (!_addressingService.ReserveAddress(device.Circuit, addressValue))
                        {
                            return new UpdateResult
                            {
                                Success = false,
                                OldValue = oldAddress,
                                NewValue = newAddress,
                                ErrorMessage = "Address is already in use"
                            };
                        }
                    }

                    var validation = await _validationService.ValidateDeviceAsync(device.SourceDevice ?? new DeviceSnapshot(
                        ElementId: int.Parse(device.ElementId),
                        LevelName: device.Level,
                        FamilyName: device.FamilyName,
                        TypeName: device.DeviceType,
                        Watts: 0,
                        Amps: (double)device.CurrentDraw,
                        UnitLoads: 1,
                        HasStrobe: device.IsNotificationDevice,
                        HasSpeaker: device.IsNotificationDevice,
                        IsIsolator: false,
                        IsRepeater: false));
                    if (!validation.IsValid)
                    {
                        return new UpdateResult
                        {
                            Success = false,
                            OldValue = oldAddress,
                            NewValue = newAddress,
                            ValidationResult = validation,
                            ErrorMessage = "Device validation failed"
                        };
                    }
                }

                // Update the address
                _unitOfWork.BeginTransaction();

                // Release old address if it exists
                if (!string.IsNullOrEmpty(oldAddress) && int.TryParse(oldAddress, out int oldAddressValue))
                {
                    _addressingService.ReleaseAddress(device.Circuit, oldAddressValue);
                }

                device.Address = newAddress;
                _unitOfWork.RegisterModified(device);

                await _unitOfWork.CommitAsync();

                return new UpdateResult
                {
                    Success = true,
                    OldValue = oldAddress,
                    NewValue = newAddress
                };
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                return new UpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<AutoAssignmentResult> AutoAssignAllAsync(AutoAssignmentOptions options)
        {
            options ??= new AutoAssignmentOptions();

            try
            {
                _unitOfWork.BeginTransaction();

                var result = new AutoAssignmentResult
                {
                    Success = true
                };

                // Get all unaddressed devices across all panels
                var unaddressedDevices = new List<SmartDeviceNode>();
                foreach (var panel in _panelCache.Values)
                {
                    foreach (var circuit in panel.Circuits)
                    {
                        var unaddressed = circuit.Devices.Where(d => string.IsNullOrEmpty(d.Address)).ToList();
                        unaddressedDevices.AddRange(unaddressed);
                    }
                }

                result.DevicesProcessed = unaddressedDevices.Count;

                // Apply addressing strategy
                var orderedDevices = ApplyAddressingStrategy(unaddressedDevices, options);

                // Assign addresses
                foreach (var device in orderedDevices)
                {
                    if (device.Circuit != null)
                    {
                        var addressingOptions = new AddressingOptions
                        {
                            RespectLocks = options.RespectLocks,
                            ValidateElectrical = options.ValidateElectrical,
                            StartAddress = options.StartAddress
                        };

                        var assignmentResult = await _addressingService.AssignAddressesAsync(new[] { device }, addressingOptions);

                        if (assignmentResult.Success)
                        {
                            result.DevicesAssigned++;
                        }
                        else
                        {
                            result.DevicesFailed++;
                        }

                        result.Results.Add(new AssignmentResult
                        {
                            Success = assignmentResult.Success,
                            DeviceId = device.ElementId,
                            CircuitId = device.Circuit.CircuitNumber,
                            ErrorMessage = assignmentResult.Errors.FirstOrDefault()
                        });
                    }
                    else
                    {
                        result.DevicesSkipped++;
                    }
                }

                result.Success = result.DevicesFailed == 0;
                await _unitOfWork.CommitAsync();

                return result;
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                return new AutoAssignmentResult
                {
                    Success = false,
                    Results = new List<AssignmentResult>
                    {
                        new AssignmentResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        }
                    }
                };
            }
        }

        public async Task<PanelValidationResult> ValidatePanelAsync()
        {
            var result = new PanelValidationResult { IsValid = true };

            try
            {
                // Validate all panels and circuits
                foreach (var panel in _panelCache.Values)
                {
                    foreach (var circuit in panel.Circuits)
                    {
                        var circuitValidation = await _addressingService.ValidateAddressingAsync(circuit);
                        if (!circuitValidation.IsValid)
                        {
                            result.IsValid = false;
                            foreach (var issue in circuitValidation.Issues)
                            {
                                result.Messages.Add(new ValidationMessage
                                {
                                    Code = issue.Code,
                                    Message = issue.Message,
                                    Severity = issue.Severity,
                                    EntityId = issue.CircuitId
                                });

                                switch (issue.Severity)
                                {
                                    case InterfaceValidationSeverity.Error:
                                    case InterfaceValidationSeverity.Critical:
                                        result.ErrorCount++;
                                        break;
                                    case InterfaceValidationSeverity.Warning:
                                        result.WarningCount++;
                                        break;
                                    case InterfaceValidationSeverity.Info:
                                        result.InfoCount++;
                                        break;
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new PanelValidationResult
                {
                    IsValid = false,
                    ErrorCount = 1,
                    Messages = new List<ValidationMessage>
                    {
                        new ValidationMessage
                        {
                            Code = "SYS_001",
                            Message = $"Panel validation failed: {ex.Message}",
                            Severity = InterfaceValidationSeverity.Error
                        }
                    }
                };
            }
        }

        public async Task<CircuitUtilization> GetCircuitUtilizationAsync(string circuitId)
        {
            if (string.IsNullOrEmpty(circuitId))
                throw new ArgumentException("Circuit ID cannot be empty", nameof(circuitId));

            var circuit = await FindCircuitAsync(circuitId);
            if (circuit == null)
            {
                return new CircuitUtilization
                {
                    CircuitId = circuitId
                };
            }

            var maxDevices = _configuration.CircuitConfiguration.MaxDevicesPerCircuit;
            var maxCurrent = (decimal)_configuration.ElectricalConfiguration.MaxCircuitCurrent;
            var maxAddresses = _configuration.CircuitConfiguration.MaxAddressPerCircuit;

            var currentDraw = circuit.Devices.Sum(d => d.CurrentDraw);
            var usedAddresses = circuit.Devices.Count(d => !string.IsNullOrEmpty(d.Address));

            return new CircuitUtilization
            {
                CircuitId = circuitId,
                DeviceCount = circuit.Devices.Count,
                MaxDevices = maxDevices,
                DeviceUtilization = maxDevices > 0 ? (double)circuit.Devices.Count / maxDevices : 0,
                CurrentDraw = (double)currentDraw,
                MaxCurrent = (double)maxCurrent,
                CurrentUtilization = maxCurrent > 0 ? (double)currentDraw / (double)maxCurrent : 0,
                UsedAddresses = usedAddresses,
                AvailableAddresses = maxAddresses - usedAddresses,
                AddressUtilization = maxAddresses > 0 ? (double)usedAddresses / maxAddresses : 0
            };
        }

        public async Task<BalancingResult> BalanceCircuitsAsync(BalancingOptions options)
        {
            options ??= new BalancingOptions();

            try
            {
                _unitOfWork.BeginTransaction();

                var result = new BalancingResult { Success = true };
                var moves = new List<DeviceMove>();

                // Calculate current imbalance
                result.ImbalanceBefore = CalculateSystemImbalance();

                // Get circuits that need balancing
                var overloadedCircuits = _panelCache.Values
                    .SelectMany(p => p.Circuits)
                    .Where(c => c.DeviceUtilization > options.TargetUtilization)
                    .ToList();

                var underloadedCircuits = _panelCache.Values
                    .SelectMany(p => p.Circuits)
                    .Where(c => c.DeviceUtilization < options.TargetUtilization * 0.5)
                    .ToList();

                // Balance circuits by moving devices
                foreach (var overloadedCircuit in overloadedCircuits)
                {
                    var devicesToMove = SelectDevicesForMove(overloadedCircuit, options);
                    
                    foreach (var device in devicesToMove)
                    {
                        var targetCircuit = FindBestTargetCircuit(device, underloadedCircuits, options);
                        if (targetCircuit != null)
                        {
                            // Move device
                            await RemoveDeviceFromCircuitAsync(device.ElementId, overloadedCircuit.CircuitNumber);
                            var assignResult = await AssignDeviceToCircuitAsync(device.ElementId, targetCircuit.CircuitNumber, new AssignmentOptions());

                            if (assignResult.Success)
                            {
                                moves.Add(new DeviceMove
                                {
                                    DeviceId = device.ElementId,
                                    FromCircuit = overloadedCircuit.CircuitNumber,
                                    ToCircuit = targetCircuit.CircuitNumber,
                                    Reason = "Load balancing"
                                });
                                result.DevicesMoved++;
                            }
                        }
                    }
                }

                result.Moves = moves;
                result.ImbalanceAfter = CalculateSystemImbalance();
                result.Success = result.ImbalanceAfter < result.ImbalanceBefore;

                await _unitOfWork.CommitAsync();
                return result;
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                return new BalancingResult
                {
                    Success = false
                };
            }
        }

        public async Task<byte[]> ExportAddressingDataAsync(ExportFormat format)
        {
            try
            {
                var data = await CollectExportData();

                return format switch
                {
                    ExportFormat.JSON => ExportAsJson(data),
                    ExportFormat.CSV => ExportAsCsv(data),
                    ExportFormat.Excel => ExportAsExcel(data),
                    ExportFormat.XML => ExportAsXml(data),
                    _ => throw new ArgumentException($"Unsupported export format: {format}")
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Export failed: {ex.Message}", ex);
            }
        }

        public async Task<ImportResult> ImportAddressingDataAsync(byte[] data, ImportOptions options)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Import data cannot be empty", nameof(data));

            options ??= new ImportOptions();

            try
            {
                _unitOfWork.BeginTransaction();

                var result = new ImportResult { Success = true };

                // Detect format and parse data
                var importData = ParseImportData(data);
                result.RecordsProcessed = importData.Count;

                foreach (var record in importData)
                {
                    try
                    {
                        if (options.ValidateBeforeImport)
                        {
                            var validation = await ValidateImportRecord(record);
                            if (!validation.IsValid)
                            {
                                result.RecordsFailed++;
                                result.Errors.Add($"Validation failed for device {record.DeviceId}: {validation.Messages.FirstOrDefault()?.Message}");
                                continue;
                            }
                        }

                        await ProcessImportRecord(record, options);
                        result.RecordsImported++;
                    }
                    catch (Exception ex)
                    {
                        result.RecordsFailed++;
                        result.Errors.Add($"Failed to import device {record.DeviceId}: {ex.Message}");
                    }
                }

                result.Success = result.RecordsFailed == 0;
                
                if (result.Success)
                {
                    await _unitOfWork.CommitAsync();
                }
                else
                {
                    _unitOfWork.Rollback();
                }

                return result;
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                return new ImportResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<AddressingStatistics> GetStatisticsAsync()
        {
            var stats = new AddressingStatistics();

            try
            {
                stats.TotalPanels = _panelCache.Count;
                stats.TotalCircuits = _panelCache.Values.Sum(p => p.Circuits.Count);

                var allDevices = _panelCache.Values
                    .SelectMany(p => p.Circuits)
                    .SelectMany(c => c.Devices)
                    .ToList();

                stats.TotalDevices = allDevices.Count;
                stats.AddressedDevices = allDevices.Count(d => !string.IsNullOrEmpty(d.Address));
                stats.UnaddressedDevices = stats.TotalDevices - stats.AddressedDevices;

                // Calculate averages
                if (stats.TotalCircuits > 0)
                {
                    var circuits = _panelCache.Values.SelectMany(p => p.Circuits).ToList();
                    stats.AverageCircuitUtilization = circuits.Average(c => c.DeviceUtilization);
                    stats.AverageCurrentDraw = circuits.Average(c => c.Devices.Sum(d => (double)d.CurrentDraw));
                }

                // Group by type
                stats.DevicesByType = allDevices
                    .GroupBy(d => d.DeviceType ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                // Group by floor
                stats.DevicesByFloor = allDevices
                    .GroupBy(d => d.Level ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                return await Task.FromResult(stats);
            }
            catch (Exception ex)
            {
                return new AddressingStatistics
                {
                    TotalDevices = -1 // Indicate error
                };
            }
        }

        public async Task<ApplyChangesResult> ApplyChangesAsync()
        {
            try
            {
                var result = new ApplyChangesResult { Success = true };
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Apply all pending changes through the unit of work
                var changesApplied = await _unitOfWork.SaveChangesAsync();
                result.ChangesApplied = changesApplied;

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                return new ApplyChangesResult
                {
                    Success = false,
                    ChangesFailed = 1,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task RevertChangesAsync()
        {
            try
            {
                _unitOfWork.Rollback();
                _panelCache.Clear();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to revert changes: {ex.Message}", ex);
            }
        }

        #region Private Methods

        private Dictionary<string, List<DeviceSnapshot>> GroupDevicesByPanel(List<DeviceSnapshot> devices)
        {
            return devices
                .Where(d => !string.IsNullOrEmpty(d.GetCircuitNumber()))
                .GroupBy(d => ExtractPanelId(d.GetCircuitNumber()))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private string ExtractPanelId(string circuitNumber)
        {
            // Extract panel ID from circuit number (e.g., "Panel1-Circuit1" -> "Panel1")
            var parts = circuitNumber?.Split('-');
            return parts?.Length > 0 ? parts[0] : "DefaultPanel";
        }

        private async Task<AddressingPanel> CreateAddressingPanel(string panelId, List<DeviceSnapshot> devices)
        {
            var panel = new AddressingPanel { PanelId = panelId };

            // Group devices by circuit
            var circuitGroups = devices.GroupBy(d => d.GetCircuitNumber());

            foreach (var circuitGroup in circuitGroups)
            {
                var circuit = new AddressingCircuit
                {
                    CircuitNumber = circuitGroup.Key,
                    MaxDevices = _configuration.CircuitConfiguration.MaxDevicesPerCircuit,
                    MaxCurrent = (decimal)_configuration.ElectricalConfiguration.MaxCircuitCurrent
                };

                foreach (var device in circuitGroup)
                {
                    var smartDevice = CreateSmartDeviceNode(device);
                    smartDevice.Circuit = circuit;
                    circuit.Devices.Add(smartDevice);
                }

                circuit.UpdateUtilization();
                panel.Circuits.Add(circuit);
            }

            return panel;
        }

        private SmartDeviceNode CreateSmartDeviceNode(DeviceSnapshot device)
        {
            return new SmartDeviceNode
            {
                ElementId = device.ElementId.ToString(),
                DeviceType = device.DeviceType,
                DeviceFunction = device.GetDeviceFunction(),
                FamilyName = device.FamilyName,
                Level = device.LevelName,
                Room = device.GetRoom(),
                X = device.X,
                Y = device.Y,
                Z = device.Z,
                Address = device.GetAddress(),
                CircuitNumber = device.GetCircuitNumber(),
                CurrentDraw = (decimal)device.GetCurrentDraw(),
                Candela = device.GetCandela(),
                IsNotificationDevice = device.GetIsNotificationDevice(),
                LockState = AddressLockState.Unlocked
            };
        }

        private async Task<SmartDeviceNode> FindDeviceAsync(string deviceId)
        {
            foreach (var panel in _panelCache.Values)
            {
                foreach (var circuit in panel.Circuits)
                {
                    var device = circuit.Devices.FirstOrDefault(d => d.ElementId == deviceId);
                    if (device != null)
                        return device;
                }
            }
            return null;
        }

        private async Task<AddressingCircuit> FindCircuitAsync(string circuitId)
        {
            foreach (var panel in _panelCache.Values)
            {
                var circuit = panel.Circuits.FirstOrDefault(c => c.CircuitNumber == circuitId);
                if (circuit != null)
                    return circuit;
            }
            return null;
        }

        private async Task<ValidationResult> ValidateDeviceToCircuitAssignment(SmartDeviceNode device, AddressingCircuit circuit)
        {
            var result = new ValidationResult { IsValid = true };

            // Check circuit capacity
            if (circuit.Devices.Count >= circuit.MaxDevices)
            {
                result.IsValid = false;
                result.Messages.Add(new ValidationMessage
                {
                    Code = "CAP_001",
                    Message = "Circuit is at maximum device capacity",
                    Severity = InterfaceValidationSeverity.Error
                });
            }

            // Check electrical capacity
            var totalCurrent = circuit.Devices.Sum(d => d.CurrentDraw) + device.CurrentDraw;
            if (totalCurrent > circuit.MaxCurrent)
            {
                result.IsValid = false;
                result.Messages.Add(new ValidationMessage
                {
                    Code = "CAP_002",
                    Message = $"Assignment would exceed circuit current capacity ({totalCurrent:F2}A > {circuit.MaxCurrent}A)",
                    Severity = InterfaceValidationSeverity.Error
                });
            }

            return result;
        }

        private List<SmartDeviceNode> ApplyAddressingStrategy(List<SmartDeviceNode> devices, AutoAssignmentOptions options)
        {
            return options.Strategy switch
            {
                AssignmentStrategy.Sequential => devices.OrderBy(d => d.ElementId).ToList(),
                AssignmentStrategy.ByFloor => devices.OrderBy(d => d.Level).ThenBy(d => d.ElementId).ToList(),
                AssignmentStrategy.ByZone => devices.OrderBy(d => d.Room).ThenBy(d => d.ElementId).ToList(),
                AssignmentStrategy.ByDeviceType => devices.OrderBy(d => d.DeviceType).ThenBy(d => d.ElementId).ToList(),
                AssignmentStrategy.Optimized => OptimizeDeviceOrder(devices, options),
                _ => devices
            };
        }

        private List<SmartDeviceNode> OptimizeDeviceOrder(List<SmartDeviceNode> devices, AutoAssignmentOptions options)
        {
            // Optimize by location proximity and electrical load distribution
            return devices.OrderBy(d => d.Level)
                         .ThenBy(d => d.X)
                         .ThenBy(d => d.Y)
                         .ThenBy(d => d.CurrentDraw)
                         .ToList();
        }

        private double CalculateSystemImbalance()
        {
            var circuits = _panelCache.Values.SelectMany(p => p.Circuits).ToList();
            if (!circuits.Any()) return 0;

            var utilizations = circuits.Select(c => c.DeviceUtilization).ToList();
            var average = utilizations.Average();
            var variance = utilizations.Select(u => Math.Pow(u - average, 2)).Average();
            return Math.Sqrt(variance); // Standard deviation as imbalance measure
        }

        private List<SmartDeviceNode> SelectDevicesForMove(AddressingCircuit overloadedCircuit, BalancingOptions options)
        {
            // Select devices that can be moved while maintaining location grouping if requested
            var candidates = overloadedCircuit.Devices.ToList();
            
            if (options.MaintainLocationGrouping)
            {
                // Prefer moving devices that don't break location groups
                candidates = candidates.OrderBy(d => GetLocationGroupSize(d, overloadedCircuit)).ToList();
            }

            // Select devices to move until we reach target utilization
            var devicesToMove = new List<SmartDeviceNode>();
            var currentUtilization = overloadedCircuit.DeviceUtilization;
            
            foreach (var device in candidates)
            {
                if (currentUtilization <= options.TargetUtilization)
                    break;

                devicesToMove.Add(device);
                currentUtilization = (overloadedCircuit.Devices.Count - devicesToMove.Count) / (double)overloadedCircuit.MaxDevices;
            }

            return devicesToMove;
        }

        private int GetLocationGroupSize(SmartDeviceNode device, AddressingCircuit circuit)
        {
            return circuit.Devices.Count(d => d.Level == device.Level && d.Room == device.Room);
        }

        private AddressingCircuit FindBestTargetCircuit(SmartDeviceNode device, List<AddressingCircuit> candidates, BalancingOptions options)
        {
            return candidates
                .Where(c => c.DeviceUtilization < options.TargetUtilization)
                .Where(c => c.Devices.Sum(d => d.CurrentDraw) + device.CurrentDraw <= c.MaxCurrent)
                .OrderBy(c => c.DeviceUtilization)
                .FirstOrDefault();
        }

        private async Task<Dictionary<string, object>> CollectExportData()
        {
            var data = new Dictionary<string, object>();

            data["Panels"] = _panelCache.Values.Select(p => new
            {
                p.PanelId,
                CircuitCount = p.Circuits.Count,
                TotalDevices = p.TotalDevices,
                Circuits = p.Circuits.Select(c => new
                {
                    c.CircuitNumber,
                    DeviceCount = c.Devices.Count,
                    c.DeviceUtilization,
                    Devices = c.Devices.Select(d => new
                    {
                        d.ElementId,
                        d.Address,
                        d.DeviceType,
                        d.Level,
                        d.Room,
                        d.CurrentDraw
                    })
                })
            }).ToList();

            data["ExportDate"] = DateTime.Now;
            data["Statistics"] = await GetStatisticsAsync();

            return data;
        }

        private byte[] ExportAsJson(Dictionary<string, object> data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private byte[] ExportAsCsv(Dictionary<string, object> data)
        {
            // Simple CSV export implementation
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Panel,Circuit,DeviceId,Address,DeviceType,Level,Room,CurrentDraw");

            foreach (var panel in _panelCache.Values)
            {
                foreach (var circuit in panel.Circuits)
                {
                    foreach (var device in circuit.Devices)
                    {
                        csv.AppendLine($"{panel.PanelId},{circuit.CircuitNumber},{device.ElementId},{device.Address},{device.DeviceType},{device.Level},{device.Room},{device.CurrentDraw}");
                    }
                }
            }

            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        }

        private byte[] ExportAsExcel(Dictionary<string, object> data)
        {
            // Placeholder - would require Excel library like EPPlus
            return ExportAsCsv(data); // Fallback to CSV for now
        }

        private byte[] ExportAsXml(Dictionary<string, object> data)
        {
            // Placeholder - would implement XML serialization
            return ExportAsJson(data); // Fallback to JSON for now
        }

        private List<ImportRecord> ParseImportData(byte[] data)
        {
            // Parse import data based on format detection
            var text = System.Text.Encoding.UTF8.GetString(data);
            var records = new List<ImportRecord>();

            // Simple CSV parsing for now
            var lines = text.Split('\n').Skip(1); // Skip header
            foreach (var line in lines)
            {
                var fields = line.Split(',');
                if (fields.Length >= 8)
                {
                    records.Add(new ImportRecord
                    {
                        Panel = fields[0],
                        Circuit = fields[1],
                        DeviceId = fields[2],
                        Address = fields[3],
                        DeviceType = fields[4],
                        Level = fields[5],
                        Room = fields[6],
                        CurrentDraw = double.TryParse(fields[7], out var current) ? current : 0
                    });
                }
            }

            return records;
        }

        private async Task<ValidationResult> ValidateImportRecord(ImportRecord record)
        {
            // Basic validation for import records
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrEmpty(record.DeviceId))
            {
                result.IsValid = false;
                result.Messages.Add(new ValidationMessage
                {
                    Message = "DeviceId is required",
                    Severity = InterfaceValidationSeverity.Error
                });
            }

            return result;
        }

        private async Task ProcessImportRecord(ImportRecord record, ImportOptions options)
        {
            var device = await FindDeviceAsync(record.DeviceId);
            if (device == null && options.CreateMissingCircuits)
            {
                // Create device if it doesn't exist
                // Implementation would depend on device creation logic
            }

            if (device != null)
            {
                if (options.OverwriteExisting || string.IsNullOrEmpty(device.Address))
                {
                    await UpdateDeviceAddressAsync(device.ElementId, record.Address, false);
                }
            }
        }

        #endregion

        #region Helper Classes

        private class ImportRecord
        {
            public string Panel { get; set; }
            public string Circuit { get; set; }
            public string DeviceId { get; set; }
            public string Address { get; set; }
            public string DeviceType { get; set; }
            public string Level { get; set; }
            public string Room { get; set; }
            public double CurrentDraw { get; set; }
        }

        #endregion
    }
}