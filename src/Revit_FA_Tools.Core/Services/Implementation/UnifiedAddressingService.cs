using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.UnitOfWork;
using FireAlarmConfiguration = Revit_FA_Tools.FireAlarmConfiguration;
using AddressLockState = Revit_FA_Tools.Models.AddressLockState;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using ValidationMessage = Revit_FA_Tools.Core.Services.Interfaces.ValidationMessage;
using AddressingValidationResult = Revit_FA_Tools.Core.Services.Interfaces.AddressingValidationResult;
using ValidationIssue = Revit_FA_Tools.Core.Services.Interfaces.ValidationIssue;
using InterfaceValidationSeverity = Revit_FA_Tools.Core.Services.Interfaces.ValidationSeverity;
using AddressingValidationSeverity = Revit_FA_Tools.Core.Models.Addressing.ValidationSeverity;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Unified addressing service that consolidates all addressing functionality
    /// </summary>
    public class UnifiedAddressingService : IAddressingService
    {
        private readonly IValidationService _validationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly FireAlarmConfiguration _configuration;

        public UnifiedAddressingService(
            IValidationService validationService, 
            IUnitOfWork unitOfWork,
            FireAlarmConfiguration configuration)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<AddressingResult> AssignAddressesAsync(IEnumerable<SmartDeviceNode> devices, AddressingOptions options)
        {
            if (devices == null)
                throw new ArgumentNullException(nameof(devices));

            options ??= new AddressingOptions();
            var deviceList = devices.ToList();

            var result = new AddressingResult
            {
                Success = true,
                DevicesAddressed = 0,
                DevicesSkipped = 0
            };

            try
            {
                _unitOfWork.BeginTransaction();

                foreach (var device in deviceList)
                {
                    // Skip if device already has address and preserve existing is set
                    if (!string.IsNullOrEmpty(device.Address) && options.RespectLocks)
                    {
                        if (device.LockState == AddressLockState.Locked)
                        {
                            result.DevicesSkipped++;
                            result.Warnings.Add($"Device {device.ElementId} skipped - address is locked");
                            continue;
                        }

                        if (!options.OverwriteExisting)
                        {
                            result.DevicesSkipped++;
                            result.Warnings.Add($"Device {device.ElementId} skipped - already has address");
                            continue;
                        }
                    }

                    // Find available address
                    var availableAddress = FindNextAvailableAddress(device.Circuit, options.StartAddress);
                    if (availableAddress == -1)
                    {
                        result.Success = false;
                        result.Errors.Add($"No available addresses for device {device.ElementId} in circuit {device.Circuit?.CircuitNumber}");
                        continue;
                    }

                    // Validate electrical parameters if requested
                    if (options.ValidateElectrical)
                    {
                        var validation = await ValidateElectricalCapacity(device, availableAddress);
                        if (!validation.IsValid)
                        {
                            result.Success = false;
                            result.Errors.AddRange(validation.Messages.Select(m => m.Message));
                            continue;
                        }
                    }

                    // Assign the address
                    var assignmentSuccess = AssignAddress(device, availableAddress);
                    if (assignmentSuccess)
                    {
                        result.DevicesAddressed++;
                        _unitOfWork.RegisterModified(device);
                    }
                    else
                    {
                        result.Success = false;
                        result.Errors.Add($"Failed to assign address {availableAddress} to device {device.ElementId}");
                    }
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                result.Success = false;
                result.Errors.Add($"Assignment failed: {ex.Message}");
            }

            return result;
        }

        public async Task<AddressingValidationResult> ValidateAddressingAsync(AddressingCircuit circuit)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            var result = new AddressingValidationResult { IsValid = true };

            // Validate circuit capacity
            if (circuit.DeviceCount > (_configuration.CircuitConfiguration?.MaxDevicesPerCircuit ?? 25))
            {
                result.IsValid = false;
                result.Issues.Add(new Revit_FA_Tools.Core.Services.Interfaces.ValidationIssue
                {
                    Code = "ADDR_001",
                    Message = $"Circuit {circuit.CircuitNumber} exceeds maximum device count ({circuit.DeviceCount} > {_configuration.CircuitConfiguration?.MaxDevicesPerCircuit ?? 25})",
                    Severity = InterfaceValidationSeverity.Error,
                    CircuitId = circuit.CircuitNumber
                });
            }

            // Validate address ranges
            var usedAddresses = circuit.Devices
                .Where(d => !string.IsNullOrEmpty(d.Address) && int.TryParse(d.Address, out _))
                .Select(d => int.Parse(d.Address))
                .ToList();

            var invalidAddresses = usedAddresses.Where(a => a < 1 || a > 250).ToList();
            foreach (var invalidAddress in invalidAddresses)
            {
                result.IsValid = false;
                result.Issues.Add(new Revit_FA_Tools.Core.Services.Interfaces.ValidationIssue
                {
                    Code = "ADDR_002",
                    Message = $"Invalid address {invalidAddress} in circuit {circuit.CircuitNumber}",
                    Severity = InterfaceValidationSeverity.Error,
                    CircuitId = circuit.CircuitNumber
                });
            }

            // Check for duplicate addresses
            var duplicateAddresses = usedAddresses
                .GroupBy(a => a)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var duplicateAddress in duplicateAddresses)
            {
                result.IsValid = false;
                result.Issues.Add(new Revit_FA_Tools.Core.Services.Interfaces.ValidationIssue
                {
                    Code = "ADDR_003",
                    Message = $"Duplicate address {duplicateAddress} found in circuit {circuit.CircuitNumber}",
                    Severity = InterfaceValidationSeverity.Error,
                    CircuitId = circuit.CircuitNumber
                });
            }

            // Validate electrical load
            var totalCurrent = circuit.Devices.Sum(d => d.CurrentDraw);
            if (totalCurrent > (decimal)(_configuration.ElectricalConfiguration?.MaxCircuitCurrent ?? 7.0))
            {
                result.IsValid = false;
                result.Issues.Add(new Revit_FA_Tools.Core.Services.Interfaces.ValidationIssue
                {
                    Code = "ADDR_004",
                    Message = $"Circuit {circuit.CircuitNumber} current draw ({totalCurrent:F2}A) exceeds maximum ({_configuration.ElectricalConfiguration?.MaxCircuitCurrent ?? 7.0}A)",
                    Severity = InterfaceValidationSeverity.Error,
                    CircuitId = circuit.CircuitNumber
                });
            }

            // Set highest severity
            if (result.Issues.Any())
            {
                result.HighestSeverity = result.Issues.Max(i => i.Severity);
            }

            return await Task.FromResult(result);
        }

        public IEnumerable<int> GetAvailableAddresses(AddressingCircuit circuit)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            var usedAddresses = circuit.Devices
                .Where(d => !string.IsNullOrEmpty(d.Address) && int.TryParse(d.Address, out _))
                .Select(d => int.Parse(d.Address))
                .ToHashSet();

            var maxAddress = _configuration.CircuitConfiguration?.MaxAddressPerCircuit ?? 250;
            
            for (int address = 1; address <= maxAddress; address++)
            {
                if (!usedAddresses.Contains(address))
                {
                    yield return address;
                }
            }
        }

        public void ReleaseAddress(AddressingCircuit circuit, int address)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            var device = circuit.Devices.FirstOrDefault(d => d.Address == address.ToString());
            if (device != null)
            {
                device.Address = "";
                device.LockState = AddressLockState.Unlocked;
                _unitOfWork.RegisterModified(device);
                circuit.UpdateUtilization();
            }
        }

        public bool ReserveAddress(AddressingCircuit circuit, int address)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            if (address < 1 || address > (_configuration.CircuitConfiguration?.MaxAddressPerCircuit ?? 250))
                return false;

            // Check if address is already in use
            var existingDevice = circuit.Devices.FirstOrDefault(d => d.Address == address.ToString());
            return existingDevice == null;
        }

        public async Task<AddressingResult> AutoAssignAsync(AddressingCircuit circuit, AutoAssignOptions options)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            options ??= new AutoAssignOptions();

            var devicesToAssign = circuit.Devices
                .Where(d => string.IsNullOrEmpty(d.Address) || options.OverwriteExisting)
                .ToList();

            // Apply grouping and optimization if requested
            if (options.OptimizeByLocation)
            {
                devicesToAssign = devicesToAssign
                    .OrderBy(d => d.Level)
                    .ThenBy(d => d.Room)
                    .ThenBy(d => d.X)
                    .ThenBy(d => d.Y)
                    .ToList();
            }

            if (options.GroupByDeviceType)
            {
                devicesToAssign = devicesToAssign
                    .OrderBy(d => d.DeviceType)
                    .ThenBy(d => d.DeviceFunction)
                    .ToList();
            }

            var assignmentOptions = new AddressingOptions
            {
                RespectLocks = options.RespectLocks,
                OverwriteExisting = options.OverwriteExisting,
                ValidateElectrical = options.ValidateElectrical,
                StartAddress = options.StartAddress
            };

            return await AssignAddressesAsync(devicesToAssign, assignmentOptions);
        }

        public void ClearAddresses(AddressingCircuit circuit)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            foreach (var device in circuit.Devices)
            {
                if (device.LockState != AddressLockState.Locked)
                {
                    device.Address = "";
                    device.LockState = AddressLockState.Unlocked;
                    _unitOfWork.RegisterModified(device);
                }
            }

            circuit.UpdateUtilization();
        }

        public AddressAllocationStatus GetAllocationStatus(AddressingCircuit circuit)
        {
            if (circuit == null)
                throw new ArgumentNullException(nameof(circuit));

            var maxAddresses = _configuration.CircuitConfiguration?.MaxAddressPerCircuit ?? 250;
            var allocatedAddresses = circuit.Devices
                .Where(d => !string.IsNullOrEmpty(d.Address) && int.TryParse(d.Address, out _))
                .Select(d => int.Parse(d.Address))
                .OrderBy(a => a)
                .ToList();

            var availableAddresses = GetAvailableAddresses(circuit).Take(50).ToList(); // Limit for performance

            return new AddressAllocationStatus
            {
                TotalAddresses = maxAddresses,
                UsedAddresses = allocatedAddresses.Count,
                AvailableAddresses = maxAddresses - allocatedAddresses.Count,
                UtilizationPercentage = (double)allocatedAddresses.Count / maxAddresses * 100,
                AllocatedAddresses = allocatedAddresses,
                AvailableAddressList = availableAddresses
            };
        }

        #region Private Methods

        private int FindNextAvailableAddress(AddressingCircuit circuit, int startAddress = 1)
        {
            if (circuit == null)
                return -1;

            var availableAddresses = GetAvailableAddresses(circuit);
            return availableAddresses.FirstOrDefault(a => a >= startAddress);
        }

        private bool AssignAddress(SmartDeviceNode device, int address)
        {
            if (device == null || address < 1)
                return false;

            try
            {
                device.Address = address.ToString();
                device.LockState = AddressLockState.Unlocked; // Allow further modifications
                
                // Update circuit utilization
                device.Circuit?.UpdateUtilization();
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<ValidationResult> ValidateElectricalCapacity(SmartDeviceNode device, int address)
        {
            // Validate that adding this device won't exceed circuit capacity
            var result = new ValidationResult { IsValid = true };

            if (device.Circuit != null)
            {
                var currentLoad = device.Circuit.Devices.Sum(d => d.CurrentDraw);
                var newLoad = currentLoad + device.CurrentDraw;

                if (newLoad > (decimal)(_configuration.ElectricalConfiguration?.MaxCircuitCurrent ?? 7.0))
                {
                    result.IsValid = false;
                    result.Messages.Add(new ValidationMessage
                    {
                        Code = "ELEC_001",
                        Message = $"Adding device would exceed circuit current limit ({newLoad:F2}A > {_configuration.ElectricalConfiguration?.MaxCircuitCurrent ?? 7.0}A)",
                        Severity = InterfaceValidationSeverity.Error,
                        EntityId = device.ElementId,
                        EntityType = "Device"
                    });
                }

                // Check device count
                var deviceCount = device.Circuit.Devices.Count(d => !string.IsNullOrEmpty(d.Address));
                if (deviceCount >= (_configuration.CircuitConfiguration?.MaxDevicesPerCircuit ?? 25))
                {
                    result.IsValid = false;
                    result.Messages.Add(new ValidationMessage
                    {
                        Code = "ELEC_002",
                        Message = $"Circuit already at maximum device count ({deviceCount}/{_configuration.CircuitConfiguration?.MaxDevicesPerCircuit ?? 25})",
                        Severity = InterfaceValidationSeverity.Error,
                        EntityId = device.ElementId,
                        EntityType = "Device"
                    });
                }
            }

            return await Task.FromResult(result);
        }

        #endregion
    }
}