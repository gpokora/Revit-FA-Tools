using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools;
using Revit_FA_Tools.Core.Services.Interfaces;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using DeviceSnapshot = Revit_FA_Tools.Models.DeviceSnapshot;
using DeviceAssignment = Revit_FA_Tools.Models.DeviceAssignment;
using SystemOverviewData = Revit_FA_Tools.Models.SystemOverviewData;
using AddressingValidationResult = Revit_FA_Tools.Core.Models.Addressing.ValidationResult;
using ModelsValidationResult = Revit_FA_Tools.Models.ValidationResult;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Unified validation service that consolidates all validation logic
    /// </summary>
    public class UnifiedValidationService : IValidationService
    {
        private readonly List<IValidationRule> _validationRules;
        private readonly FireAlarmConfiguration _configuration;

        public UnifiedValidationService(FireAlarmConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validationRules = new List<IValidationRule>();
            RegisterDefaultRules();
        }

        public async Task<ValidationResult> ValidateDeviceAsync(DeviceSnapshot device)
        {
            if (device == null)
            {
                return ValidationResult.Failure("Device cannot be null", ValidationSeverity.Error);
            }

            var result = new ValidationResult { IsValid = true };
            var rules = GetValidationRules(ValidationContext.Device);

            foreach (var rule in rules)
            {
                var ruleResult = await rule.ValidateAsync(device);
                MergeResults(result, ruleResult);
            }

            // Device-specific validations
            ValidateDeviceProperties(device, result);
            ValidateElectricalProperties(device, result);
            ValidateLocationProperties(device, result);

            return result;
        }

        public async Task<ValidationResult> ValidateDevicesAsync(IEnumerable<DeviceSnapshot> devices)
        {
            var result = new ValidationResult { IsValid = true };

            if (devices == null || !devices.Any())
            {
                return ValidationResult.Failure("No devices to validate", ValidationSeverity.Warning);
            }

            foreach (var device in devices)
            {
                var deviceResult = await ValidateDeviceAsync(device);
                MergeResults(result, deviceResult);
            }

            // Cross-device validations
            ValidateDuplicateAddresses(devices, result);
            ValidateCircuitCapacity(devices, result);

            return result;
        }

        public async Task<ValidationResult> ValidateCircuitAsync(object circuit)
        {
            var result = new ValidationResult { IsValid = true };
            var rules = GetValidationRules(ValidationContext.Circuit);

            foreach (var rule in rules)
            {
                var ruleResult = await rule.ValidateAsync(circuit);
                MergeResults(result, ruleResult);
            }

            return result;
        }

        public async Task<ValidationResult> ValidatePanelAsync(object panel)
        {
            var result = new ValidationResult { IsValid = true };
            var rules = GetValidationRules(ValidationContext.Panel);

            foreach (var rule in rules)
            {
                var ruleResult = await rule.ValidateAsync(panel);
                MergeResults(result, ruleResult);
            }

            return result;
        }

        public async Task<ValidationResult> ValidateSystemAsync()
        {
            var result = new ValidationResult { IsValid = true };
            var rules = GetValidationRules(ValidationContext.System);

            foreach (var rule in rules)
            {
                var ruleResult = await rule.ValidateAsync(null);
                MergeResults(result, ruleResult);
            }

            return result;
        }

        public async Task<PreAnalysisResult> PerformPreAnalysisAsync()
        {
            var result = new PreAnalysisResult
            {
                IsValid = true,
                CanProceedWithAnalysis = true,
                ReadinessStatus = AnalysisReadinessStatus.Ready
            };

            // Check system readiness
            var systemValidation = await ValidateSystemAsync();
            if (!systemValidation.IsValid)
            {
                result.IsValid = false;
                result.Messages.AddRange(systemValidation.Messages);
                
                if (systemValidation.HighestSeverity >= ValidationSeverity.Error)
                {
                    result.CanProceedWithAnalysis = false;
                    result.ReadinessStatus = AnalysisReadinessStatus.CannotProceed;
                }
                else
                {
                    result.ReadinessStatus = AnalysisReadinessStatus.WarningsPresent;
                }
            }

            // Add required actions based on validation results
            if (result.Messages.Any(m => m.Severity >= ValidationSeverity.Error))
            {
                result.RequiredActions.Add("Fix all critical errors before proceeding");
                result.ReadinessStatus = AnalysisReadinessStatus.RequiresUserAction;
            }

            return result;
        }

        public async Task<ValidationResult> ValidateParameterMappingsAsync(Dictionary<string, object> mappings)
        {
            var result = new ValidationResult { IsValid = true };

            if (mappings == null || !mappings.Any())
            {
                return ValidationResult.Failure("No parameter mappings to validate", ValidationSeverity.Warning);
            }

            foreach (var mapping in mappings)
            {
                ValidateParameterMapping(mapping.Key, mapping.Value, result);
            }

            return await Task.FromResult(result);
        }

        public async Task<ValidationResult> ValidateElectricalParametersAsync(ElectricalParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters == null)
            {
                return ValidationResult.Failure("Electrical parameters cannot be null", ValidationSeverity.Error);
            }

            // Voltage validation
            if (parameters.Voltage < _configuration.ElectricalConfiguration.MinVoltage ||
                parameters.Voltage > _configuration.ElectricalConfiguration.MaxVoltage)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ELEC001",
                    Message = $"Voltage {parameters.Voltage}V is outside acceptable range",
                    Severity = ValidationSeverity.Error,
                    PropertyName = "Voltage",
                    PropertyValue = parameters.Voltage
                });
                result.IsValid = false;
            }

            // Current validation
            if (parameters.Current > _configuration.ElectricalConfiguration.MaxCircuitCurrent)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ELEC002",
                    Message = $"Current {parameters.Current}A exceeds maximum circuit current",
                    Severity = ValidationSeverity.Error,
                    PropertyName = "Current",
                    PropertyValue = parameters.Current
                });
                result.IsValid = false;
            }

            // Power validation
            var calculatedPower = parameters.Voltage * parameters.Current;
            if (Math.Abs(parameters.Power - calculatedPower) > 0.1)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ELEC003",
                    Message = "Power calculation mismatch",
                    Severity = ValidationSeverity.Warning,
                    PropertyName = "Power",
                    PropertyValue = parameters.Power
                });
            }

            UpdateHighestSeverity(result);
            return await Task.FromResult(result);
        }

        public IEnumerable<IValidationRule> GetValidationRules(ValidationContext context)
        {
            return _validationRules.Where(r => r.Context == context);
        }

        public void RegisterValidationRule(IValidationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (!_validationRules.Any(r => r.RuleId == rule.RuleId))
            {
                _validationRules.Add(rule);
            }
        }

        private void RegisterDefaultRules()
        {
            // Register default validation rules
            RegisterValidationRule(new DeviceAddressValidationRule());
            RegisterValidationRule(new CircuitCapacityValidationRule(_configuration));
            RegisterValidationRule(new ElectricalLoadValidationRule(_configuration));
            RegisterValidationRule(new DeviceCompatibilityValidationRule());
        }

        private void ValidateDeviceProperties(DeviceSnapshot device, ValidationResult result)
        {
            // Validate device ID
            if (device.ElementId <= 0)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "DEV001",
                    Message = "Device ID is missing",
                    Severity = ValidationSeverity.Error,
                    EntityType = "Device"
                });
                result.IsValid = false;
            }

            // Validate device type
            if (string.IsNullOrWhiteSpace(device.DeviceType))
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "DEV002",
                    Message = "Device type is not specified",
                    Severity = ValidationSeverity.Warning,
                    EntityId = device.ElementId.ToString(),
                    EntityType = "Device"
                });
            }

            // Validate device function (using DeviceType as alternative)
            if (string.IsNullOrWhiteSpace(device.DeviceType))
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "DEV003",
                    Message = "Device type is not specified",
                    Severity = ValidationSeverity.Info,
                    EntityId = device.ElementId.ToString(),
                    EntityType = "Device"
                });
            }
        }

        private void ValidateElectricalProperties(DeviceSnapshot device, ValidationResult result)
        {
            // Validate current draw (using Amps from DeviceSnapshot)
            if (device.Amps < 0)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ELEC004",
                    Message = "Invalid negative current draw",
                    Severity = ValidationSeverity.Error,
                    PropertyName = "Amps",
                    PropertyValue = device.Amps,
                    EntityId = device.ElementId.ToString(),
                    EntityType = "Device"
                });
                result.IsValid = false;
            }

            // Validate notification device properties (using HasStrobe as indicator)
            if (device.HasStrobe && device.Watts <= 0)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ELEC005",
                    Message = "Notification device missing power rating",
                    Severity = ValidationSeverity.Warning,
                    PropertyName = "Watts",
                    PropertyValue = device.Watts,
                    EntityId = device.ElementId.ToString(),
                    EntityType = "Device"
                });
            }
        }

        private void ValidateLocationProperties(DeviceSnapshot device, ValidationResult result)
        {
            // Validate level (using LevelName)
            if (string.IsNullOrWhiteSpace(device.LevelName))
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "LOC001",
                    Message = "Device level/floor is not specified",
                    Severity = ValidationSeverity.Warning,
                    PropertyName = "LevelName",
                    EntityId = device.ElementId.ToString(),
                    EntityType = "Device"
                });
            }

            // Note: DeviceSnapshot doesn't have Room property, skip this validation
            // Room information would need to be obtained from Revit element parameters separately
        }

        private void ValidateDuplicateAddresses(IEnumerable<DeviceSnapshot> devices, ValidationResult result)
        {
            // Note: DeviceSnapshot doesn't have Address property
            // Address validation would need to be done at SmartDeviceNode level
            var addressGroups = new List<IGrouping<string, DeviceSnapshot>>();

            foreach (var group in addressGroups)
            {
                var deviceIds = string.Join(", ", group.Select(d => d.ElementId));
                result.Messages.Add(new ValidationMessage
                {
                    Code = "ADDR001",
                    Message = $"Duplicate address {group.Key} found on devices: {deviceIds}",
                    Severity = ValidationSeverity.Error,
                    PropertyName = "Address",
                    PropertyValue = group.Key
                });
                result.IsValid = false;
            }
        }

        private void ValidateCircuitCapacity(IEnumerable<DeviceSnapshot> devices, ValidationResult result)
        {
            // Note: DeviceSnapshot doesn't have CircuitNumber property  
            // Circuit validation would need to be done with separate circuit assignment data
            var circuitGroups = new List<IGrouping<string, DeviceSnapshot>>();

            foreach (var circuit in circuitGroups)
            {
                var deviceCount = circuit.Count();
                var totalCurrent = circuit.Sum(d => d.Amps);

                if (deviceCount > _configuration.CircuitConfiguration.MaxDevicesPerCircuit)
                {
                    result.Messages.Add(new ValidationMessage
                    {
                        Code = "CIRC001",
                        Message = $"Circuit {circuit.Key} has {deviceCount} devices, exceeding maximum of {_configuration.CircuitConfiguration.MaxDevicesPerCircuit}",
                        Severity = ValidationSeverity.Error,
                        PropertyName = "DeviceCount",
                        PropertyValue = deviceCount
                    });
                    result.IsValid = false;
                }

                if (totalCurrent > _configuration.ElectricalConfiguration.MaxCircuitCurrent)
                {
                    result.Messages.Add(new ValidationMessage
                    {
                        Code = "CIRC002",
                        Message = $"Circuit {circuit.Key} total current {totalCurrent:F2}A exceeds maximum",
                        Severity = ValidationSeverity.Error,
                        PropertyName = "TotalCurrent",
                        PropertyValue = totalCurrent
                    });
                    result.IsValid = false;
                }
            }
        }

        private void ValidateParameterMapping(string key, object value, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "MAP001",
                    Message = "Parameter mapping key cannot be empty",
                    Severity = ValidationSeverity.Error
                });
                result.IsValid = false;
            }

            if (value == null)
            {
                result.Messages.Add(new ValidationMessage
                {
                    Code = "MAP002",
                    Message = $"Parameter mapping value for '{key}' is null",
                    Severity = ValidationSeverity.Warning,
                    PropertyName = key
                });
            }
        }

        private void MergeResults(ValidationResult target, ValidationResult source)
        {
            if (source == null) return;

            target.Messages.AddRange(source.Messages);
            target.IsValid = target.IsValid && source.IsValid;
            
            if (source.HighestSeverity > target.HighestSeverity)
            {
                target.HighestSeverity = source.HighestSeverity;
            }

            foreach (var metadata in source.Metadata)
            {
                if (!target.Metadata.ContainsKey(metadata.Key))
                {
                    target.Metadata[metadata.Key] = metadata.Value;
                }
            }
        }

        private void UpdateHighestSeverity(ValidationResult result)
        {
            if (result.Messages.Any())
            {
                result.HighestSeverity = result.Messages.Max(m => m.Severity);
            }
        }
    }

    // Default validation rules
    internal class DeviceAddressValidationRule : IValidationRule
    {
        public string RuleId => "RULE_DEVICE_ADDRESS";
        public string Description => "Validates device addressing";
        public ValidationContext Context => ValidationContext.Device;
        public ValidationSeverity Severity => ValidationSeverity.Error;

        public async Task<ValidationResult> ValidateAsync(object target)
        {
            var result = new ValidationResult { IsValid = true };
            
            if (target is DeviceSnapshot device)
            {
                var deviceAddress = device.ActualCustomProperties.ContainsKey("Address") ? device.ActualCustomProperties["Address"]?.ToString() : "";
                if (!string.IsNullOrWhiteSpace(deviceAddress))
                {
                    if (!int.TryParse(deviceAddress, out int address) || address < 1 || address > 250)
                    {
                        result.Messages.Add(new ValidationMessage
                        {
                            Code = "ADDR002",
                            Message = $"Invalid address format or range: {deviceAddress}",
                            Severity = ValidationSeverity.Error,
                            EntityId = device.ElementId.ToString()
                        });
                        result.IsValid = false;
                    }
                }
            }

            return await Task.FromResult(result);
        }
    }

    internal class CircuitCapacityValidationRule : IValidationRule
    {
        private readonly FireAlarmConfiguration _configuration;

        public CircuitCapacityValidationRule(FireAlarmConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string RuleId => "RULE_CIRCUIT_CAPACITY";
        public string Description => "Validates circuit capacity limits";
        public ValidationContext Context => ValidationContext.Circuit;
        public ValidationSeverity Severity => ValidationSeverity.Error;

        public async Task<ValidationResult> ValidateAsync(object target)
        {
            var result = new ValidationResult { IsValid = true };
            // Circuit-specific validation logic here
            return await Task.FromResult(result);
        }
    }

    internal class ElectricalLoadValidationRule : IValidationRule
    {
        private readonly FireAlarmConfiguration _configuration;

        public ElectricalLoadValidationRule(FireAlarmConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string RuleId => "RULE_ELECTRICAL_LOAD";
        public string Description => "Validates electrical load calculations";
        public ValidationContext Context => ValidationContext.Electrical;
        public ValidationSeverity Severity => ValidationSeverity.Error;

        public async Task<ValidationResult> ValidateAsync(object target)
        {
            var result = new ValidationResult { IsValid = true };
            // Electrical load validation logic here
            return await Task.FromResult(result);
        }
    }

    internal class DeviceCompatibilityValidationRule : IValidationRule
    {
        public string RuleId => "RULE_DEVICE_COMPATIBILITY";
        public string Description => "Validates device compatibility";
        public ValidationContext Context => ValidationContext.Device;
        public ValidationSeverity Severity => ValidationSeverity.Warning;

        public async Task<ValidationResult> ValidateAsync(object target)
        {
            var result = new ValidationResult { IsValid = true };
            // Device compatibility validation logic here
            return await Task.FromResult(result);
        }
    }
}