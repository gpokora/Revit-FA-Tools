using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Core.Models.Addressing;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// Enhanced validation engine with advanced device specification validation
    /// Provides comprehensive validation beyond basic addressing requirements
    /// </summary>
    public class EnhancedValidationEngine
    {
        private readonly DeviceRepositoryService _repository;
        
        public EnhancedValidationEngine()
        {
            _repository = new DeviceRepositoryService();
        }
        
        /// <summary>
        /// Comprehensive device specification validation
        /// </summary>
        public EnhancedValidationResult ValidateDeviceSpecifications(SmartDeviceNode device)
        {
            var result = new EnhancedValidationResult
            {
                DeviceName = device.DeviceName,
                IsValid = true,
                Warnings = new List<string>(),
                Recommendations = new List<string>(),
                ComplianceIssues = new List<ComplianceIssue>()
            };
            
            try
            {
                // 1. Repository specification validation
                ValidateRepositorySpecifications(device, result);
                
                // 2. Electrical compatibility validation
                ValidateElectricalCompatibility(device, result);
                
                // 3. Fire alarm code compliance
                ValidateFireAlarmCompliance(device, result);
                
                // 4. Installation requirements validation
                ValidateInstallationRequirements(device, result);
                
                // 5. Performance optimization recommendations
                GenerateOptimizationRecommendations(device, result);
                
                // 6. Environmental compatibility
                ValidateEnvironmentalCompatibility(device, result);
                
                // Determine overall validation status
                result.IsValid = !result.ComplianceIssues.Any(ci => ci.Severity == ComplianceSeverity.Critical);
                result.ValidationScore = CalculateValidationScore(result);
                
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ComplianceIssues.Add(new ComplianceIssue
                {
                    IssueType = "VALIDATION_ERROR",
                    Severity = ComplianceSeverity.Critical,
                    Description = $"Validation engine error: {ex.Message}",
                    Recommendation = "Contact technical support"
                });
                return result;
            }
        }
        
        /// <summary>
        /// Validate circuit-level compliance and optimization
        /// </summary>
        public CircuitValidationResult ValidateCircuitConfiguration(AddressingCircuit circuit)
        {
            var result = new CircuitValidationResult
            {
                CircuitName = circuit.Name,
                IsValid = true,
                DeviceValidations = new List<EnhancedValidationResult>(),
                CircuitIssues = new List<ComplianceIssue>()
            };
            
            try
            {
                // Validate each device on the circuit
                foreach (var device in circuit.Devices)
                {
                    var deviceValidation = ValidateDeviceSpecifications(device);
                    result.DeviceValidations.Add(deviceValidation);
                }
                
                // Circuit-level validations
                ValidateCircuitElectricalLimits(circuit, result);
                ValidateCircuitCapacity(circuit, result);
                ValidateCircuitBalance(circuit, result);
                ValidateCircuitCompliance(circuit, result);
                
                result.IsValid = !result.CircuitIssues.Any(ci => ci.Severity == ComplianceSeverity.Critical) &&
                                result.DeviceValidations.All(dv => dv.IsValid);
                
                result.OverallScore = CalculateCircuitScore(result);
                
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "CIRCUIT_VALIDATION_ERROR",
                    Severity = ComplianceSeverity.Critical,
                    Description = $"Circuit validation error: {ex.Message}"
                });
                return result;
            }
        }
        
        private void ValidateRepositorySpecifications(SmartDeviceNode device, EnhancedValidationResult result)
        {
            if (device.SourceDevice?.HasRepositorySpecifications() == true)
            {
                var spec = device.SourceDevice.GetSpecification();
                
                if (spec != null)
                {
                    result.Recommendations.Add($"✓ Verified device specifications from {spec.Manufacturer} catalog");
                    
                    // Validate UL listing
                    if (!spec.IsULListed)
                    {
                        result.ComplianceIssues.Add(new ComplianceIssue
                        {
                            IssueType = "UL_LISTING",
                            Severity = ComplianceSeverity.High,
                            Description = "Device is not UL listed for fire alarm applications",
                            Recommendation = "Use UL listed device or verify AHJ acceptance"
                        });
                    }
                    
                    // Validate specifications consistency
                    if (Math.Abs(device.PowerConsumption - (decimal)spec.PowerConsumption) > 0.1m)
                    {
                        result.Warnings.Add($"Power consumption mismatch: Device shows {device.PowerConsumption}W, repository shows {spec.PowerConsumption}W");
                    }
                }
            }
            else
            {
                result.Warnings.Add("Device not found in repository - specifications may be estimated");
                result.Recommendations.Add("Verify device specifications with manufacturer documentation");
            }
        }
        
        private void ValidateElectricalCompatibility(SmartDeviceNode device, EnhancedValidationResult result)
        {
            // Current draw validation
            if (device.CurrentDraw > 0.5m) // 500mA threshold
            {
                result.Warnings.Add($"High current draw: {device.CurrentDraw}A - verify power supply capacity");
            }
            
            // Power consumption validation
            if (device.PowerConsumption > 10m) // 10W threshold for IDNAC devices
            {
                result.ComplianceIssues.Add(new ComplianceIssue
                {
                    IssueType = "POWER_CONSUMPTION",
                    Severity = ComplianceSeverity.Medium,
                    Description = $"High power consumption: {device.PowerConsumption}W",
                    Recommendation = "Consider using lower power device or verify amplifier capacity"
                });
            }
            
            // Unit load validation
            if (device.UnitLoads > 1)
            {
                result.Recommendations.Add($"Device uses {device.UnitLoads} unit loads - verify circuit capacity");
            }
        }
        
        private void ValidateFireAlarmCompliance(SmartDeviceNode device, EnhancedValidationResult result)
        {
            // NFPA 72 compliance checks
            if (device.SourceDevice?.HasStrobe == true)
            {
                var candelaRating = device.SourceDevice.GetCandelaRating();
                if (candelaRating < 15)
                {
                    result.ComplianceIssues.Add(new ComplianceIssue
                    {
                        IssueType = "NFPA72_CANDELA",
                        Severity = ComplianceSeverity.High,
                        Description = "Candela rating below NFPA 72 minimum (15cd)",
                        Recommendation = "Use device with minimum 15 candela rating"
                    });
                }
                
                if (candelaRating > 185)
                {
                    result.Recommendations.Add("High candela rating - verify space requirements and ADA compliance");
                }
            }
            
            // Device spacing compliance
            if (device.SourceDevice?.HasSpeaker == true)
            {
                result.Recommendations.Add("Verify speaker coverage and intelligibility requirements per NFPA 72");
            }
            
            // Environmental requirements
            var envRating = device.SourceDevice?.GetEnvironmentalRating();
            if (envRating == "OUTDOOR" || envRating == "WEATHERPROOF")
            {
                result.Recommendations.Add("Outdoor device requires appropriate conduit and weatherproofing installation");
            }
        }
        
        private void ValidateInstallationRequirements(SmartDeviceNode device, EnhancedValidationResult result)
        {
            var mountingType = device.SourceDevice?.GetMountingType();
            
            // Mounting validation
            if (mountingType == "CEILING" && device.Level?.ToUpper().Contains("BASEMENT") == true)
            {
                result.Warnings.Add("Ceiling mount device in basement - verify adequate clearance");
            }
            
            // T-Tap compatibility
            if (device.SourceDevice?.IsTTapCompatible() == true)
            {
                result.Recommendations.Add("Device supports T-Tap wiring - consider for installation efficiency");
            }
            
            // Environmental considerations
            if (device.Zone?.ToUpper().Contains("KITCHEN") == true || 
                device.Zone?.ToUpper().Contains("MECHANICAL") == true)
            {
                if (device.SourceDevice?.HasStrobe == true)
                {
                    result.Recommendations.Add("High ambient environment - verify strobe visibility and cleaning access");
                }
            }
        }
        
        private void GenerateOptimizationRecommendations(SmartDeviceNode device, EnhancedValidationResult result)
        {
            // Address optimization
            if (device.AssignedAddress.HasValue && device.PhysicalPosition > 0)
            {
                if (Math.Abs(device.AssignedAddress.Value - device.PhysicalPosition) > 10)
                {
                    result.Recommendations.Add($"Consider address {device.PhysicalPosition} to match physical position (currently {device.AssignedAddress})");
                }
            }
            
            // Power efficiency
            if (device.PowerConsumption > 0 && device.SourceDevice?.HasRepositorySpecifications() == true)
            {
                var spec = device.SourceDevice.GetSpecification();
                if (spec.PowerConsumption < (double)device.PowerConsumption * 0.8) // 20% power savings available
                {
                    result.Recommendations.Add("More efficient device model available in catalog");
                }
            }
            
            // Feature optimization
            if (device.SourceDevice?.HasStrobe == true && device.SourceDevice?.HasSpeaker == true)
            {
                result.Recommendations.Add("Combination device provides space and installation efficiency");
            }
        }
        
        private void ValidateEnvironmentalCompatibility(SmartDeviceNode device, EnhancedValidationResult result)
        {
            var envRating = device.SourceDevice?.GetEnvironmentalRating() ?? "INDOOR";
            var deviceLevel = device.Level?.ToUpper() ?? "";
            
            // Environmental matching
            if ((deviceLevel.Contains("PARKING") || deviceLevel.Contains("GARAGE")) && envRating == "INDOOR")
            {
                result.ComplianceIssues.Add(new ComplianceIssue
                {
                    IssueType = "ENVIRONMENTAL_RATING",
                    Severity = ComplianceSeverity.Medium,
                    Description = "Indoor rated device in parking/garage environment",
                    Recommendation = "Consider weatherproof or sealed device for harsh environment"
                });
            }
            
            if (deviceLevel.Contains("EXTERIOR") && envRating != "OUTDOOR")
            {
                result.ComplianceIssues.Add(new ComplianceIssue
                {
                    IssueType = "ENVIRONMENTAL_RATING",
                    Severity = ComplianceSeverity.High,
                    Description = "Non-outdoor rated device in exterior location",
                    Recommendation = "Use outdoor/weatherproof rated device"
                });
            }
        }
        
        private void ValidateCircuitElectricalLimits(AddressingCircuit circuit, CircuitValidationResult result)
        {
            var totalCurrent = circuit.TotalCurrent;
            var currentLimit = 3.0m; // IDNAC 3A limit
            
            if (totalCurrent > currentLimit)
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "CIRCUIT_CURRENT_LIMIT",
                    Severity = ComplianceSeverity.Critical,
                    Description = $"Circuit current {totalCurrent:F2}A exceeds {currentLimit}A limit",
                    Recommendation = "Reduce device loading or split circuit"
                });
            }
            else if (totalCurrent > currentLimit * 0.9m) // 90% warning
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "CIRCUIT_CURRENT_WARNING",
                    Severity = ComplianceSeverity.Medium,
                    Description = $"Circuit current {totalCurrent:F2}A approaching {currentLimit}A limit",
                    Recommendation = "Consider load balancing for future expansion"
                });
            }
        }
        
        private void ValidateCircuitCapacity(AddressingCircuit circuit, CircuitValidationResult result)
        {
            var utilization = circuit.UtilizationPercentage;
            
            if (utilization > 0.95) // 95% capacity
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "CIRCUIT_CAPACITY",
                    Severity = ComplianceSeverity.High,
                    Description = $"Circuit at {utilization:P1} capacity - no room for expansion",
                    Recommendation = "Add additional circuit for future devices"
                });
            }
            else if (utilization > circuit.SafeCapacityThreshold)
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "CIRCUIT_CAPACITY_WARNING",
                    Severity = ComplianceSeverity.Medium,
                    Description = $"Circuit approaching capacity limit at {utilization:P1}",
                    Recommendation = "Plan for additional circuit capacity"
                });
            }
        }
        
        private void ValidateCircuitBalance(AddressingCircuit circuit, CircuitValidationResult result)
        {
            var devices = circuit.Devices.ToList();
            if (devices.Count < 3) return; // Not enough devices to analyze balance
            
            // Check for address gaps
            var addressedDevices = devices.Where(d => d.AssignedAddress.HasValue).OrderBy(d => d.AssignedAddress).ToList();
            if (addressedDevices.Any())
            {
                var maxGap = 0;
                for (int i = 1; i < addressedDevices.Count; i++)
                {
                    var gap = addressedDevices[i].AssignedAddress.Value - addressedDevices[i - 1].AssignedAddress.Value - 1;
                    maxGap = Math.Max(maxGap, gap);
                }
                
                if (maxGap > 10)
                {
                    result.CircuitIssues.Add(new ComplianceIssue
                    {
                        IssueType = "ADDRESS_GAPS",
                        Severity = ComplianceSeverity.Low,
                        Description = $"Large address gaps detected (max {maxGap})",
                        Recommendation = "Consider sequential addressing for easier maintenance"
                    });
                }
            }
        }
        
        private void ValidateCircuitCompliance(AddressingCircuit circuit, CircuitValidationResult result)
        {
            // Isolator requirements
            var deviceCount = circuit.Devices.Count;
            var isolatorCount = circuit.Devices.Count(d => d.SourceDevice?.IsIsolator == true);
            
            if (deviceCount > 20 && isolatorCount == 0)
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "ISOLATOR_REQUIREMENT",
                    Severity = ComplianceSeverity.Medium,
                    Description = "Large circuit without isolators - consider fault isolation",
                    Recommendation = "Add isolator modules per NFPA 72 requirements"
                });
            }
            
            // End-of-line requirements
            var hasEOL = circuit.Devices.Any(d => d.Connections?.Any(c => c.Type == ConnectionType.EndOfLine) == true);
            if (!hasEOL && deviceCount > 0)
            {
                result.CircuitIssues.Add(new ComplianceIssue
                {
                    IssueType = "EOL_REQUIREMENT",
                    Severity = ComplianceSeverity.High,
                    Description = "Circuit missing end-of-line device or resistor",
                    Recommendation = "Install EOL resistor or end-of-line device per panel requirements"
                });
            }
        }
        
        private double CalculateValidationScore(EnhancedValidationResult result)
        {
            double score = 100.0;
            
            // Deduct points for compliance issues
            foreach (var issue in result.ComplianceIssues)
            {
                switch (issue.Severity)
                {
                    case ComplianceSeverity.Critical:
                        score -= 25;
                        break;
                    case ComplianceSeverity.High:
                        score -= 15;
                        break;
                    case ComplianceSeverity.Medium:
                        score -= 10;
                        break;
                    case ComplianceSeverity.Low:
                        score -= 5;
                        break;
                }
            }
            
            // Deduct minor points for warnings
            score -= result.Warnings.Count * 2;
            
            return Math.Max(0, score);
        }
        
        private double CalculateCircuitScore(CircuitValidationResult result)
        {
            var deviceScores = result.DeviceValidations.Select(dv => dv.ValidationScore);
            var avgDeviceScore = deviceScores.Any() ? deviceScores.Average() : 100;
            
            var circuitScore = CalculateValidationScore(new EnhancedValidationResult
            {
                ComplianceIssues = result.CircuitIssues
            });
            
            return (avgDeviceScore + circuitScore) / 2;
        }
    }
    
    public class EnhancedValidationResult
    {
        public string DeviceName { get; set; }
        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<ComplianceIssue> ComplianceIssues { get; set; } = new List<ComplianceIssue>();
        public double ValidationScore { get; set; } = 100.0;
    }
    
    public class CircuitValidationResult
    {
        public string CircuitName { get; set; }
        public bool IsValid { get; set; }
        public List<EnhancedValidationResult> DeviceValidations { get; set; } = new List<EnhancedValidationResult>();
        public List<ComplianceIssue> CircuitIssues { get; set; } = new List<ComplianceIssue>();
        public double OverallScore { get; set; } = 100.0;
    }
    
    public class ComplianceIssue
    {
        public string IssueType { get; set; }
        public ComplianceSeverity Severity { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public string CodeReference { get; set; }
    }
    
    public enum ComplianceSeverity
    {
        Low,      // Minor optimization opportunity
        Medium,   // Should be addressed
        High,     // Important compliance issue
        Critical  // Must be fixed before installation
    }
}