using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Core.Services.Interfaces
{
    /// <summary>
    /// Unified parameter mapping service interface
    /// </summary>
    public interface IParameterMappingService
    {
        /// <summary>
        /// Maps parameters for a device
        /// </summary>
        Task<ParameterMappingResult> MapParametersAsync(DeviceSnapshot device, MappingOptions options = null);

        /// <summary>
        /// Maps parameters for multiple devices
        /// </summary>
        Task<BatchMappingResult> MapParametersBatchAsync(IEnumerable<DeviceSnapshot> devices, MappingOptions options = null);

        /// <summary>
        /// Enhances a device with additional parameters
        /// </summary>
        Task<DeviceSnapshot> EnhanceDeviceAsync(DeviceSnapshot device);

        /// <summary>
        /// Gets device specifications from repository
        /// </summary>
        Task<DeviceSpecification> GetDeviceSpecificationAsync(string deviceType, string model = null);

        /// <summary>
        /// Extracts parameters from a source object
        /// </summary>
        Task<Dictionary<string, object>> ExtractParametersAsync(object source, ExtractionOptions options = null);

        /// <summary>
        /// Validates parameter mappings
        /// </summary>
        Task<ValidationResult> ValidateMappingsAsync(Dictionary<string, object> mappings);

        /// <summary>
        /// Gets mapping configuration
        /// </summary>
        MappingConfiguration GetConfiguration();

        /// <summary>
        /// Updates mapping configuration
        /// </summary>
        void UpdateConfiguration(MappingConfiguration configuration);

        /// <summary>
        /// Clears all cached mappings
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// Parameter mapping result
    /// </summary>
    public class ParameterMappingResult
    {
        public bool Success { get; set; }
        public DeviceSnapshot MappedDevice { get; set; }
        public Dictionary<string, object> MappedParameters { get; set; } = new Dictionary<string, object>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Batch mapping result
    /// </summary>
    public class BatchMappingResult
    {
        public bool Success { get; set; }
        public int TotalDevices { get; set; }
        public int SuccessfulMappings { get; set; }
        public int FailedMappings { get; set; }
        public List<ParameterMappingResult> Results { get; set; } = new List<ParameterMappingResult>();
        public TimeSpan TotalProcessingTime { get; set; }
    }

    /// <summary>
    /// Mapping options
    /// </summary>
    public class MappingOptions
    {
        public bool UseCache { get; set; } = true;
        public bool EnhanceWithRepository { get; set; } = true;
        public bool ValidateResults { get; set; } = true;
        public bool IncludeElectricalParameters { get; set; } = true;
        public bool IncludeLocationParameters { get; set; } = true;
        public MappingStrategy Strategy { get; set; } = MappingStrategy.Default;
    }

    /// <summary>
    /// Extraction options
    /// </summary>
    public class ExtractionOptions
    {
        public bool IncludePrivateProperties { get; set; } = false;
        public bool IncludeCalculatedProperties { get; set; } = true;
        public List<string> PropertiesToInclude { get; set; }
        public List<string> PropertiesToExclude { get; set; }
    }

    /// <summary>
    /// Mapping strategy
    /// </summary>
    public enum MappingStrategy
    {
        Default,
        Performance,
        Comprehensive,
        Minimal
    }

    /// <summary>
    /// Device specification
    /// </summary>
    public class DeviceSpecification
    {
        public string DeviceType { get; set; }
        public string Model { get; set; }
        public string Manufacturer { get; set; }
        public Dictionary<string, object> ElectricalSpecs { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> PhysicalSpecs { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> FunctionalSpecs { get; set; } = new Dictionary<string, object>();
        public List<string> Certifications { get; set; } = new List<string>();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Mapping configuration
    /// </summary>
    public class MappingConfiguration
    {
        public Dictionary<string, string> ParameterMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> DefaultValues { get; set; } = new Dictionary<string, object>();
        public List<MappingRule> Rules { get; set; } = new List<MappingRule>();
        public bool EnableAutoMapping { get; set; } = true;
        public int CacheExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// Mapping rule
    /// </summary>
    public class MappingRule
    {
        public string RuleId { get; set; }
        public string SourceParameter { get; set; }
        public string TargetParameter { get; set; }
        public Func<object, object> TransformFunction { get; set; }
        public bool IsRequired { get; set; }
    }
}