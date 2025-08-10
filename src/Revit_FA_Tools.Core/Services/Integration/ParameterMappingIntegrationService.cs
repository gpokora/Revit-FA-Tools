using System;
using System.Collections.Generic;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Models.Addressing;
using Revit_FA_Tools.Services.ParameterMapping;

namespace Revit_FA_Tools.Services.Integration
{
    /// <summary>
    /// Integration service connecting parameter mapping with addressing system
    /// </summary>
    public class ParameterMappingIntegrationService
    {
        private readonly ParameterMappingEngine _parameterMapping;
        
        public ParameterMappingIntegrationService()
        {
            _parameterMapping = new ParameterMappingEngine();
        }
        
        /// <summary>
        /// Analyze device with both parameter mapping and addressing capabilities
        /// </summary>
        public ComprehensiveDeviceResult ProcessDeviceComprehensively(DeviceSnapshot sourceDevice)
        {
            try
            {
                // 1. Parameter mapping for accurate specifications
                var parameterResult = _parameterMapping.AnalyzeDevice(sourceDevice);
                
                // 2. Create addressing node with enhanced device info
                var addressingNode = new SmartDeviceNode
                {
                    SourceDevice = parameterResult.EnhancedSnapshot ?? sourceDevice,
                    DeviceName = sourceDevice.FamilyName,
                    DeviceType = parameterResult.DeviceClassification?.Category ?? sourceDevice.GetDeviceCategory(),
                    
                    // Use specifications from parameter mapping if available
                    PhysicalPosition = 1, // Default position
                };
                
                // 3. Apply enhanced electrical properties
                if (parameterResult.DeviceSpecification != null)
                {
                    var spec = parameterResult.DeviceSpecification;
                    // The enhanced snapshot already has these values applied
                }
                
                return new ComprehensiveDeviceResult
                {
                    ParameterMapping = parameterResult,
                    AddressingNode = addressingNode,
                    ElectricalSpecifications = parameterResult.DeviceSpecification,
                    ValidationResults = parameterResult.ValidationResult,
                    ProcessingTime = parameterResult.ProcessingTime,
                    Success = parameterResult.Success
                };
            }
            catch (Exception ex)
            {
                return new ComprehensiveDeviceResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = TimeSpan.Zero
                };
            }
        }
        
        /// <summary>
        /// Batch process multiple devices efficiently
        /// </summary>
        public List<ComprehensiveDeviceResult> ProcessDevicesBatch(List<DeviceSnapshot> devices)
        {
            var results = new List<ComprehensiveDeviceResult>();
            
            foreach (var device in devices)
            {
                results.Add(ProcessDeviceComprehensively(device));
            }
            
            return results;
        }
    }
    
    public class ComprehensiveDeviceResult
    {
        public ParameterMappingResult ParameterMapping { get; set; }
        public SmartDeviceNode AddressingNode { get; set; }
        public DeviceSpecification ElectricalSpecifications { get; set; }
        public ParameterMapping.ValidationResult ValidationResults { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}