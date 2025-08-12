using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Core.Services.Interfaces
{
    /// <summary>
    /// Unified validation service interface
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates a device snapshot
        /// </summary>
        Task<ValidationResult> ValidateDeviceAsync(DeviceSnapshot device);

        /// <summary>
        /// Validates a collection of devices
        /// </summary>
        Task<ValidationResult> ValidateDevicesAsync(IEnumerable<DeviceSnapshot> devices);

        /// <summary>
        /// Validates circuit configuration
        /// </summary>
        Task<ValidationResult> ValidateCircuitAsync(object circuit);

        /// <summary>
        /// Validates panel configuration
        /// </summary>
        Task<ValidationResult> ValidatePanelAsync(object panel);

        /// <summary>
        /// Validates the entire system
        /// </summary>
        Task<ValidationResult> ValidateSystemAsync();

        /// <summary>
        /// Performs pre-analysis validation
        /// </summary>
        Task<PreAnalysisResult> PerformPreAnalysisAsync();

        /// <summary>
        /// Validates parameter mappings
        /// </summary>
        Task<ValidationResult> ValidateParameterMappingsAsync(Dictionary<string, object> mappings);

        /// <summary>
        /// Validates electrical parameters
        /// </summary>
        Task<ValidationResult> ValidateElectricalParametersAsync(ElectricalParameters parameters);

        /// <summary>
        /// Gets validation rules for a specific context
        /// </summary>
        IEnumerable<IValidationRule> GetValidationRules(ValidationContext context);

        /// <summary>
        /// Registers a custom validation rule
        /// </summary>
        void RegisterValidationRule(IValidationRule rule);
    }

    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationMessage> Messages { get; set; } = new List<ValidationMessage>();
        public ValidationSeverity HighestSeverity { get; set; } = ValidationSeverity.None;
        public DateTime ValidationTime { get; set; } = DateTime.Now;
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        public static ValidationResult Failure(string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            return new ValidationResult
            {
                IsValid = false,
                Messages = new List<ValidationMessage> 
                { 
                    new ValidationMessage 
                    { 
                        Message = message, 
                        Severity = severity 
                    } 
                },
                HighestSeverity = severity
            };
        }
    }

    /// <summary>
    /// Validation message
    /// </summary>
    public class ValidationMessage
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string PropertyName { get; set; }
        public object PropertyValue { get; set; }
        public string EntityId { get; set; }
        public string EntityType { get; set; }
    }

    /// <summary>
    /// Pre-analysis validation result
    /// </summary>
    public class PreAnalysisResult : ValidationResult
    {
        public bool CanProceedWithAnalysis { get; set; }
        public List<string> RequiredActions { get; set; } = new List<string>();
        public AnalysisReadinessStatus ReadinessStatus { get; set; }
    }

    /// <summary>
    /// Analysis readiness status
    /// </summary>
    public enum AnalysisReadinessStatus
    {
        Ready,
        WarningsPresent,
        RequiresUserAction,
        CannotProceed
    }

    /// <summary>
    /// Validation severity
    /// </summary>
    public enum ValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    /// <summary>
    /// Validation context
    /// </summary>
    public enum ValidationContext
    {
        Device,
        Circuit,
        Panel,
        System,
        Electrical,
        Parameters,
        PreAnalysis
    }

    /// <summary>
    /// Validation rule interface
    /// </summary>
    public interface IValidationRule
    {
        string RuleId { get; }
        string Description { get; }
        ValidationContext Context { get; }
        ValidationSeverity Severity { get; }
        Task<ValidationResult> ValidateAsync(object target);
    }

    /// <summary>
    /// Electrical parameters for validation
    /// </summary>
    public class ElectricalParameters
    {
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Power { get; set; }
        public double CableLength { get; set; }
        public string CableType { get; set; }
        public double AmbientTemperature { get; set; }
    }
}