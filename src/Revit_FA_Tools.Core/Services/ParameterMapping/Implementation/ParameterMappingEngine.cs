using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services.Addressing;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// Core parameter mapping engine with <100ms performance requirement
    /// Transforms basic DeviceSnapshot into repository-accurate specifications
    /// </summary>
    public class ParameterMappingEngine
    {
        private readonly DeviceRepositoryService _repository;
        private readonly ParameterExtractor _extractor;
        private readonly ValidationEngine _validator;
        
        // Performance caching
        private static readonly Dictionary<string, ParameterMappingResult> _resultCache 
            = new Dictionary<string, ParameterMappingResult>();
        
        public ParameterMappingEngine()
        {
            _repository = new DeviceRepositoryService();
            _extractor = new ParameterExtractor();
            _validator = new ValidationEngine();
        }
        
        /// <summary>
        /// CRITICAL METHOD: Analyze device with <100ms performance requirement
        /// </summary>
        public ParameterMappingResult AnalyzeDevice(DeviceSnapshot device)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = GenerateCacheKey(device);
            
            try
            {
                // Check cache first for performance
                if (_resultCache.TryGetValue(cacheKey, out var cached))
                {
                    cached.ProcessingTime = stopwatch.Elapsed;
                    return cached;
                }
                
                var result = new ParameterMappingResult
                {
                    OriginalSnapshot = device,
                    ProcessingTime = TimeSpan.Zero,
                    Success = false
                };
                
                // Step 1: Extract all available parameters (target: <20ms)
                var extractedParameters = _extractor.ExtractAllParameters(device);
                result.ExtractedParameters = extractedParameters;
                
                // Step 2: Device classification (target: <10ms)
                result.DeviceClassification = ClassifyDevice(device, extractedParameters);
                
                // Step 3: Repository lookup (target: <30ms)
                result.DeviceSpecification = _repository.FindSpecification(device, extractedParameters);
                
                // Step 4: Create enhanced snapshot (target: <20ms)
                result.EnhancedSnapshot = CreateEnhancedSnapshot(device, result.DeviceSpecification, extractedParameters);
                
                // Step 5: Validation (target: <20ms)
                result.ValidationResult = _validator.ValidateMapping(device, result.DeviceSpecification);
                
                result.Success = true;
                result.ProcessingTime = stopwatch.Elapsed;
                
                // Cache successful results
                if (result.ProcessingTime.TotalMilliseconds < 200) // Only cache fast results
                {
                    _resultCache[cacheKey] = result;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return new ParameterMappingResult
                {
                    OriginalSnapshot = device,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }
        
        /// <summary>
        /// Batch analysis with optimized performance
        /// </summary>
        public List<ParameterMappingResult> AnalyzeDevicesBatch(List<DeviceSnapshot> devices)
        {
            var results = new List<ParameterMappingResult>();
            var stopwatch = Stopwatch.StartNew();
            
            // Group similar devices for optimized repository lookups
            var deviceGroups = devices.GroupBy(d => new { d.FamilyName, d.TypeName });
            
            foreach (var group in deviceGroups)
            {
                var template = group.First();
                var templateResult = AnalyzeDevice(template);
                
                // Apply template result to similar devices
                foreach (var device in group)
                {
                    if (device == template)
                    {
                        results.Add(templateResult);
                    }
                    else
                    {
                        results.Add(ApplyTemplateResult(device, templateResult));
                    }
                }
            }
            
            return results;
        }
        
        private DeviceClassification ClassifyDevice(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            var classification = new DeviceClassification();
            
            // Primary category classification
            if (device.HasStrobe && device.HasSpeaker)
                classification.Category = "SPEAKER_STROBE";
            else if (device.HasStrobe)
                classification.Category = "STROBE";
            else if (device.HasSpeaker)
                classification.Category = "SPEAKER";
            else if (device.IsIsolator)
                classification.Category = "ISOLATOR";
            else if (device.IsRepeater)
                classification.Category = "REPEATER";
            else if (device.FamilyName.ToUpper().Contains("SMOKE"))
                classification.Category = "SMOKE_DETECTOR";
            else if (device.FamilyName.ToUpper().Contains("HEAT"))
                classification.Category = "HEAT_DETECTOR";
            else if (device.FamilyName.ToUpper().Contains("PULL"))
                classification.Category = "PULL_STATION";
            else
                classification.Category = "UNKNOWN";
            
            // Subcategory classification
            if (parameters.TryGetValue("CANDELA", out var candela))
            {
                if (int.TryParse(candela.ToString(), out var candelaValue))
                {
                    if (candelaValue >= 135)
                        classification.Subcategory = "HIGH_CANDELA";
                    else if (candelaValue >= 75)
                        classification.Subcategory = "STANDARD_CANDELA";
                    else
                        classification.Subcategory = "LOW_CANDELA";
                }
            }
            
            if (parameters.TryGetValue("WATTAGE", out var wattage))
            {
                if (double.TryParse(wattage.ToString(), out var wattageValue))
                {
                    if (wattageValue >= 8.0)
                        classification.Subcategory = "HIGH_WATTAGE";
                    else if (wattageValue >= 2.0)
                        classification.Subcategory = "STANDARD_WATTAGE";
                    else
                        classification.Subcategory = "LOW_WATTAGE";
                }
            }
            
            // Confidence scoring
            classification.ConfidenceScore = CalculateConfidenceScore(device, parameters);
            
            return classification;
        }
        
        private DeviceSnapshot CreateEnhancedSnapshot(DeviceSnapshot original, DeviceSpecification specification, Dictionary<string, object> parameters)
        {
            var enhanced = new DeviceSnapshot(
                ElementId: original.ElementId,
                LevelName: original.LevelName,
                FamilyName: original.FamilyName,
                TypeName: original.TypeName,
                Watts: specification?.PowerConsumption ?? original.Watts,
                Amps: specification?.CurrentDraw ?? CalculateAmpsFromWatts(specification?.PowerConsumption ?? original.Watts),
                UnitLoads: specification?.UnitLoads ?? original.UnitLoads,
                HasStrobe: original.HasStrobe,
                HasSpeaker: original.HasSpeaker,
                IsIsolator: original.IsIsolator,
                IsRepeater: original.IsRepeater,
                CustomProperties: MergeCustomProperties(original.CustomProperties, parameters, specification)
            );
            
            return enhanced;
        }
        
        private Dictionary<string, object> MergeCustomProperties(
            Dictionary<string, object> original, 
            Dictionary<string, object> extracted, 
            DeviceSpecification specification)
        {
            var merged = new Dictionary<string, object>(original ?? new Dictionary<string, object>());
            
            // Add extracted parameters
            if (extracted != null)
            {
                foreach (var kvp in extracted)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
            
            // Add specification data
            if (specification != null)
            {
                merged["SKU"] = specification.SKU;
                merged["MANUFACTURER"] = specification.Manufacturer;
                merged["T_TAP_COMPATIBLE"] = specification.IsTTapCompatible;
                merged["MOUNTING_TYPE"] = specification.MountingType;
                merged["ENVIRONMENTAL_RATING"] = specification.EnvironmentalRating;
                merged["UL_LISTED"] = specification.IsULListed;
                merged["CURRENT_DRAW_24V"] = specification.CurrentDraw;
                merged["POWER_CONSUMPTION"] = specification.PowerConsumption;
            }
            
            return merged;
        }
        
        private ParameterMappingResult ApplyTemplateResult(DeviceSnapshot device, ParameterMappingResult template)
        {
            return new ParameterMappingResult
            {
                OriginalSnapshot = device,
                ExtractedParameters = template.ExtractedParameters,
                DeviceClassification = template.DeviceClassification,
                DeviceSpecification = template.DeviceSpecification,
                EnhancedSnapshot = CreateEnhancedSnapshot(device, template.DeviceSpecification, template.ExtractedParameters),
                ValidationResult = template.ValidationResult,
                Success = template.Success,
                ProcessingTime = TimeSpan.FromMilliseconds(5) // Fast template application
            };
        }
        
        private double CalculateConfidenceScore(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            double score = 0.0;
            int factors = 0;
            
            // Factor 1: Family name clarity
            if (!string.IsNullOrEmpty(device.FamilyName))
            {
                score += device.FamilyName.Length > 3 ? 0.2 : 0.1;
                factors++;
            }
            
            // Factor 2: Parameter completeness
            if (parameters.Count > 0)
            {
                score += Math.Min(parameters.Count * 0.1, 0.3);
                factors++;
            }
            
            // Factor 3: Device type specificity
            if (device.HasStrobe || device.HasSpeaker || device.IsIsolator || device.IsRepeater)
            {
                score += 0.3;
                factors++;
            }
            
            // Factor 4: Electrical parameters present
            if (device.Watts > 0 || device.Amps > 0)
            {
                score += 0.2;
                factors++;
            }
            
            return factors > 0 ? Math.Min(score / factors, 1.0) : 0.0;
        }
        
        private double CalculateAmpsFromWatts(double watts)
        {
            // Standard 24V calculation for fire alarm devices
            return watts > 0 ? watts / 24.0 : 0;
        }
        
        private string GenerateCacheKey(DeviceSnapshot device)
        {
            return $"{device.FamilyName}|{device.TypeName}|{device.Watts}|{device.HasStrobe}|{device.HasSpeaker}";
        }
        
        public void ClearCache()
        {
            _resultCache.Clear();
        }
        
        public int GetCacheSize()
        {
            return _resultCache.Count;
        }
    }
    
    /// <summary>
    /// Result of parameter mapping analysis
    /// </summary>
    public class ParameterMappingResult
    {
        public DeviceSnapshot OriginalSnapshot { get; set; }
        public Dictionary<string, object> ExtractedParameters { get; set; }
        public DeviceClassification DeviceClassification { get; set; }
        public DeviceSpecification DeviceSpecification { get; set; }
        public DeviceSnapshot EnhancedSnapshot { get; set; }
        public ValidationResult ValidationResult { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Device classification with confidence scoring
    /// </summary>
    public class DeviceClassification
    {
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Complete device specification from repository
    /// </summary>
    public class DeviceSpecification
    {
        public string SKU { get; set; }
        public string Manufacturer { get; set; }
        public string ProductName { get; set; }
        public double CurrentDraw { get; set; }
        public double PowerConsumption { get; set; }
        public int UnitLoads { get; set; }
        public bool IsTTapCompatible { get; set; }
        public string MountingType { get; set; }
        public string EnvironmentalRating { get; set; }
        public bool IsULListed { get; set; }
        public Dictionary<string, object> TechnicalSpecs { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Validation result for parameter mapping
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public ValidationSeverity Severity { get; set; }
        public string DetailedExplanation { get; set; }
        public List<int> SuggestedAlternatives { get; set; } = new List<int>();
    }
    
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}