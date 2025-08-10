using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models.Addressing;
using ParameterMappingValidation = Revit_FA_Tools.Services.ParameterMapping;

namespace Revit_FA_Tools.Services.Addressing
{
    /// <summary>
    /// Comprehensive validation engine with real-time feedback
    /// </summary>
    public class ValidationEngine
    {
        public Models.Addressing.ValidationResult ValidateAddressAssignment(int address, SmartDeviceNode device)
        {
            var result = new Models.Addressing.ValidationResult { IsValid = true };
            
            // Range validation
            if (address < 1 || address > (device.ParentCircuit?.MaxAddresses ?? 159))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Address {address} is outside valid range (1-{device.ParentCircuit?.MaxAddresses ?? 159})";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                result.DetailedExplanation = "Fire alarm device addresses must be within the panel's supported range.";
                return result;
            }
            
            // Duplicate address validation
            var existingDevice = device.ParentCircuit?.AddressPool?.GetAssignedDevice(address);
            if (existingDevice != null && existingDevice != device)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Address {address} is already assigned to '{existingDevice.DeviceName}'";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                result.DetailedExplanation = "Each device on a signaling line circuit must have a unique address.";
                result.SuggestedAlternatives = device.ParentCircuit.AddressPool.GetNearbyAvailableAddresses(address, 5);
                return result;
            }
            
            // Address lock validation
            if (existingDevice?.IsAddressLocked == true && existingDevice != device)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Address {address} is locked (device already installed in field)";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                result.DetailedExplanation = "This address is locked because the device has been physically installed.";
                return result;
            }
            
            // Capacity validation
            var circuit = device.ParentCircuit;
            if (circuit != null)
            {
                var utilization = circuit.UtilizationPercentage;
                
                if (utilization > 0.95) // 95% capacity
                {
                    result.Warnings.Add("Circuit is at 95%+ capacity - risk of exceeding limits");
                    result.Severity = Models.Addressing.ValidationSeverity.Critical;
                }
                else if (utilization > circuit.SafeCapacityThreshold)
                {
                    result.Warnings.Add($"Circuit utilization at {utilization:P1} - approaching safe threshold");
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
                }
                
                // Electrical validation
                var totalCurrent = circuit.TotalCurrent + device.CurrentDraw;
                if (totalCurrent > 3.0m) // 3A IDNAC limit
                {
                    result.Warnings.Add($"Circuit current {totalCurrent:F2}A exceeds 3A limit");
                    result.Severity = Models.Addressing.ValidationSeverity.Error;
                }
                else if (totalCurrent > 2.7m) // 90% of 3A
                {
                    result.Warnings.Add($"Circuit current {totalCurrent:F2}A approaching 3A limit");
                    if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                        result.Severity = Models.Addressing.ValidationSeverity.Warning;
                }
            }
            
            // Optimization suggestions
            var optimalAddress = device.PhysicalPosition;
            if (optimalAddress != address && 
                device.ParentCircuit?.AddressPool?.IsAddressAvailable(optimalAddress) == true)
            {
                result.Warnings.Add($"Consider address {optimalAddress} to match physical position {device.PhysicalPosition}");
                result.SuggestedAlternatives.Add(optimalAddress);
                if (result.Severity == Models.Addressing.ValidationSeverity.Info)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }
            
            return result;
        }

        public Models.Addressing.ValidationResult ValidateCircuit(AddressingCircuit circuit)
        {
            var result = new Models.Addressing.ValidationResult { IsValid = true };
            
            if (circuit == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Circuit is null";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                return result;
            }

            // Check for duplicate addresses
            var addressGroups = circuit.Devices
                .Where(d => d.AssignedAddress.HasValue)
                .GroupBy(d => d.AssignedAddress.Value)
                .Where(g => g.Count() > 1);

            foreach (var group in addressGroups)
            {
                result.IsValid = false;
                result.Warnings.Add($"Address {group.Key} assigned to multiple devices: {string.Join(", ", group.Select(d => d.DeviceName))}");
                result.Severity = Models.Addressing.ValidationSeverity.Error;
            }

            // Check circuit capacity
            var utilization = circuit.UtilizationPercentage;
            if (utilization > 0.95)
            {
                result.Warnings.Add($"Circuit at {utilization:P1} capacity - critical");
                result.Severity = Models.Addressing.ValidationSeverity.Critical;
            }
            else if (utilization > circuit.SafeCapacityThreshold)
            {
                result.Warnings.Add($"Circuit at {utilization:P1} capacity - approaching limit");
                if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }

            // Check electrical limits
            if (circuit.TotalCurrent > 3.0m)
            {
                result.IsValid = false;
                result.Warnings.Add($"Total circuit current {circuit.TotalCurrent:F2}A exceeds 3A limit");
                result.Severity = Models.Addressing.ValidationSeverity.Error;
            }

            return result;
        }

        public ParameterMappingValidation.ValidationResult ValidateMapping(Models.DeviceSnapshot device, object? deviceSpecification)
        {
            // Create ParameterMapping ValidationResult and convert from Addressing ValidationResult
            var addressingResult = ValidateMappingInternal(device, deviceSpecification);
            
            // Convert to ParameterMapping ValidationResult
            return new ParameterMappingValidation.ValidationResult
            {
                IsValid = addressingResult.IsValid,
                ErrorMessage = addressingResult.ErrorMessage ?? string.Empty,
                DetailedExplanation = addressingResult.DetailedExplanation ?? string.Empty,
                Warnings = addressingResult.Warnings?.ToList() ?? new List<string>(),
                Severity = ConvertSeverity(addressingResult.Severity),
                SuggestedAlternatives = addressingResult.SuggestedAlternatives?.ToList() ?? new List<int>()
            };
        }
        
        private Models.Addressing.ValidationResult ValidateMappingInternal(Models.DeviceSnapshot device, object? deviceSpecification)
        {
            var result = new Models.Addressing.ValidationResult 
            { 
                IsValid = true,
                ErrorMessage = string.Empty,
                DetailedExplanation = string.Empty
            };
            
            if (device == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Device snapshot is null";
                result.Severity = Models.Addressing.ValidationSeverity.Error;
                return result;
            }

            // Validate device family name exists
            if (string.IsNullOrEmpty(device.FamilyName))
            {
                result.Warnings.Add("Device family name is missing or empty");
                if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }

            // Validate device type exists
            if (string.IsNullOrEmpty(device.TypeName))
            {
                result.Warnings.Add("Device type name is missing or empty");
                if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }

            // Validate specification mapping if available
            if (deviceSpecification == null)
            {
                result.Warnings.Add("No device specification found in repository");
                if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }

            // Validate power parameters if present
            if (device.Amps <= 0 && device.Watts <= 0)
            {
                result.Warnings.Add("Device power parameters (current/wattage) are missing or invalid");
                if (result.Severity < Models.Addressing.ValidationSeverity.Warning)
                    result.Severity = Models.Addressing.ValidationSeverity.Warning;
            }

            return result;
        }
        
        private ParameterMappingValidation.ValidationSeverity ConvertSeverity(Models.Addressing.ValidationSeverity severity)
        {
            return severity switch
            {
                Models.Addressing.ValidationSeverity.Info => ParameterMappingValidation.ValidationSeverity.Info,
                Models.Addressing.ValidationSeverity.Warning => ParameterMappingValidation.ValidationSeverity.Warning,
                Models.Addressing.ValidationSeverity.Error => ParameterMappingValidation.ValidationSeverity.Error,
                Models.Addressing.ValidationSeverity.Critical => ParameterMappingValidation.ValidationSeverity.Critical,
                _ => ParameterMappingValidation.ValidationSeverity.Info
            };
        }
    }
}