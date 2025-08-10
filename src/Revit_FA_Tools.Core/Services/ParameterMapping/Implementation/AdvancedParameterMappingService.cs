using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// Advanced parameter mapping service with machine learning-like pattern recognition,
    /// batch processing optimization, and intelligent device matching
    /// </summary>
    public class AdvancedParameterMappingService
    {
        private readonly ParameterMappingEngine _engine;
        private readonly DeviceRepositoryService _repository;
        private readonly ParameterExtractor _extractor;
        
        // Learning patterns for device recognition
        private readonly Dictionary<string, DevicePattern> _learnedPatterns;
        private readonly List<DeviceMappingHistory> _mappingHistory;
        
        public AdvancedParameterMappingService()
        {
            _engine = new ParameterMappingEngine();
            _repository = new DeviceRepositoryService();
            _extractor = new ParameterExtractor();
            _learnedPatterns = new Dictionary<string, DevicePattern>();
            _mappingHistory = new List<DeviceMappingHistory>();
        }
        
        /// <summary>
        /// Advanced batch processing with optimization and learning
        /// </summary>
        public async Task<BatchMappingResult> ProcessDevicesBatchAdvanced(List<DeviceSnapshot> devices, BatchProcessingOptions options = null)
        {
            options ??= new BatchProcessingOptions();
            
            var result = new BatchMappingResult
            {
                StartTime = DateTime.Now,
                ProcessedDevices = new List<ParameterMappingResult>(),
                Statistics = new BatchStatistics(),
                OptimizationApplied = new List<string>()
            };
            
            try
            {
                // 1. Pre-analysis and optimization
                var optimizedGroups = await OptimizeDeviceGrouping(devices);
                result.OptimizationApplied.Add($"Grouped {devices.Count} devices into {optimizedGroups.Count} optimized batches");
                
                // 2. Apply learned patterns
                await ApplyLearnedPatterns(optimizedGroups);
                result.OptimizationApplied.Add("Applied learned device recognition patterns");
                
                // 3. Parallel batch processing
                var processedGroups = new List<Task<List<ParameterMappingResult>>>();
                
                foreach (var group in optimizedGroups)
                {
                    if (options.UseParallelProcessing)
                    {
                        processedGroups.Add(ProcessDeviceGroupParallel(group));
                    }
                    else
                    {
                        var groupResult = await ProcessDeviceGroup(group);
                        result.ProcessedDevices.AddRange(groupResult);
                    }
                }
                
                if (options.UseParallelProcessing)
                {
                    var allResults = await Task.WhenAll(processedGroups);
                    foreach (var groupResults in allResults)
                    {
                        result.ProcessedDevices.AddRange(groupResults);
                    }
                }
                
                // 4. Post-processing analysis
                await AnalyzeAndLearnFromResults(result.ProcessedDevices);
                
                // 5. Generate statistics
                result.Statistics = GenerateBatchStatistics(result.ProcessedDevices, result.StartTime);
                result.EndTime = DateTime.Now;
                result.TotalDuration = result.EndTime - result.StartTime;
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;
                result.TotalDuration = result.EndTime - result.StartTime;
                return result;
            }
        }
        
        /// <summary>
        /// Intelligent device matching with confidence scoring
        /// </summary>
        public IntelligentMatchingResult FindBestDeviceMatch(DeviceSnapshot device, List<DeviceSpecification> candidateSpecs = null)
        {
            var result = new IntelligentMatchingResult
            {
                InputDevice = device,
                Matches = new List<DeviceMatch>(),
                AnalysisTime = DateTime.Now
            };
            
            try
            {
                // Get candidate specifications
                candidateSpecs ??= GetCandidateSpecifications(device);
                
                foreach (var spec in candidateSpecs)
                {
                    var match = new DeviceMatch
                    {
                        Specification = spec,
                        ConfidenceScore = 0.0,
                        MatchingCriteria = new List<string>(),
                        MatchingFactors = new Dictionary<string, double>()
                    };
                    
                    // 1. Name similarity matching
                    var nameSimilarity = CalculateNameSimilarity(device.FamilyName, spec.ProductName);
                    match.MatchingFactors["NAME_SIMILARITY"] = nameSimilarity;
                    match.ConfidenceScore += nameSimilarity * 0.3; // 30% weight
                    
                    // 2. Parameter matching
                    var parameterMatch = CalculateParameterMatch(device, spec);
                    match.MatchingFactors["PARAMETER_MATCH"] = parameterMatch;
                    match.ConfidenceScore += parameterMatch * 0.4; // 40% weight
                    
                    // 3. Device characteristics matching
                    var characteristicsMatch = CalculateCharacteristicsMatch(device, spec);
                    match.MatchingFactors["CHARACTERISTICS_MATCH"] = characteristicsMatch;
                    match.ConfidenceScore += characteristicsMatch * 0.2; // 20% weight
                    
                    // 4. Historical pattern matching
                    var patternMatch = CalculatePatternMatch(device, spec);
                    match.MatchingFactors["PATTERN_MATCH"] = patternMatch;
                    match.ConfidenceScore += patternMatch * 0.1; // 10% weight
                    
                    // Generate matching criteria description
                    GenerateMatchingCriteria(match);
                    
                    result.Matches.Add(match);
                }
                
                // Sort by confidence score
                result.Matches = result.Matches.OrderByDescending(m => m.ConfidenceScore).ToList();
                result.BestMatch = result.Matches.FirstOrDefault();
                result.Success = result.BestMatch != null;
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
        
        /// <summary>
        /// Smart parameter inference using machine learning-like patterns
        /// </summary>
        public SmartInferenceResult InferMissingParameters(DeviceSnapshot device)
        {
            var result = new SmartInferenceResult
            {
                OriginalDevice = device,
                InferredParameters = new Dictionary<string, object>(),
                InferenceConfidence = new Dictionary<string, double>(),
                InferenceMethods = new Dictionary<string, string>()
            };
            
            try
            {
                // 1. Pattern-based inference from family name
                InferFromFamilyNamePatterns(device, result);
                
                // 2. Statistical inference from similar devices
                InferFromSimilarDevices(device, result);
                
                // 3. Manufacturer-specific inference
                InferFromManufacturerPatterns(device, result);
                
                // 4. Electrical relationship inference
                InferFromElectricalRelationships(device, result);
                
                // 5. Environmental context inference
                InferFromEnvironmentalContext(device, result);
                
                result.Success = result.InferredParameters.Any();
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
        
        /// <summary>
        /// Performance-optimized device grouping
        /// </summary>
        private async Task<List<DeviceGroup>> OptimizeDeviceGrouping(List<DeviceSnapshot> devices)
        {
            return await Task.Run(() =>
            {
                var groups = new Dictionary<string, DeviceGroup>();
                
                foreach (var device in devices)
                {
                    // Group by family name and similar characteristics
                    var groupKey = GenerateGroupKey(device);
                    
                    if (!groups.ContainsKey(groupKey))
                    {
                        groups[groupKey] = new DeviceGroup
                        {
                            GroupKey = groupKey,
                            Devices = new List<DeviceSnapshot>(),
                            RepresentativeDevice = device,
                            EstimatedProcessingTime = TimeSpan.Zero
                        };
                    }
                    
                    groups[groupKey].Devices.Add(device);
                }
                
                // Estimate processing time for each group
                foreach (var group in groups.Values)
                {
                    group.EstimatedProcessingTime = EstimateGroupProcessingTime(group);
                }
                
                return groups.Values.OrderBy(g => g.EstimatedProcessingTime).ToList();
            });
        }
        
        /// <summary>
        /// Apply learned patterns from previous successful mappings
        /// </summary>
        private async Task ApplyLearnedPatterns(List<DeviceGroup> groups)
        {
            await Task.Run(() =>
            {
                foreach (var group in groups)
                {
                    var groupKey = group.GroupKey;
                    if (_learnedPatterns.TryGetValue(groupKey, out var pattern))
                    {
                        // Apply learned pattern to speed up processing
                        foreach (var device in group.Devices)
                        {
                            ApplyDevicePattern(device, pattern);
                        }
                        group.PatternApplied = true;
                    }
                }
            });
        }
        
        /// <summary>
        /// Process device group in parallel
        /// </summary>
        private async Task<List<ParameterMappingResult>> ProcessDeviceGroupParallel(DeviceGroup group)
        {
            var tasks = group.Devices.Select(device => Task.Run(() => _engine.AnalyzeDevice(device)));
            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }
        
        /// <summary>
        /// Process device group sequentially
        /// </summary>
        private async Task<List<ParameterMappingResult>> ProcessDeviceGroup(DeviceGroup group)
        {
            return await Task.Run(() => group.Devices.Select(device => _engine.AnalyzeDevice(device)).ToList());
        }
        
        /// <summary>
        /// Analyze results and learn patterns for future use
        /// </summary>
        private async Task AnalyzeAndLearnFromResults(List<ParameterMappingResult> results)
        {
            await Task.Run(() =>
            {
                var successfulMappings = results.Where(r => r.Success && r.DeviceSpecification != null);
                
                foreach (var mapping in successfulMappings)
                {
                    // Record successful mapping for learning
                    var history = new DeviceMappingHistory
                    {
                        OriginalDevice = mapping.OriginalSnapshot,
                        MappedSpecification = mapping.DeviceSpecification,
                        MappingTime = DateTime.Now,
                        ProcessingTime = mapping.ProcessingTime,
                        Success = true
                    };
                    
                    _mappingHistory.Add(history);
                    
                    // Update learned patterns
                    UpdateLearnedPatterns(mapping);
                }
                
                // Clean up old history (keep last 1000 entries)
                if (_mappingHistory.Count > 1000)
                {
                    _mappingHistory.RemoveRange(0, _mappingHistory.Count - 1000);
                }
            });
        }
        
        private List<DeviceSpecification> GetCandidateSpecifications(DeviceSnapshot device)
        {
            // Get specifications from repository
            var allSpecs = new List<DeviceSpecification>();
            
            // Try direct repository lookup first
            var directSpec = _repository.FindSpecification(device);
            if (directSpec != null)
            {
                allSpecs.Add(directSpec);
            }
            
            // Add similar devices from repository
            var deviceType = device.GetDeviceCategory();
            var categorySpecs = _repository.GetDevicesByCategory(deviceType);
            allSpecs.AddRange(categorySpecs);
            
            return allSpecs.Distinct().Take(10).ToList(); // Limit to top 10 candidates
        }
        
        private double CalculateNameSimilarity(string deviceName, string specName)
        {
            if (string.IsNullOrEmpty(deviceName) || string.IsNullOrEmpty(specName))
                return 0.0;
            
            // Simple Levenshtein-like similarity
            var deviceWords = deviceName.ToUpper().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var specWords = specName.ToUpper().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            var matches = deviceWords.Count(dw => specWords.Any(sw => sw.Contains(dw) || dw.Contains(sw)));
            return (double)matches / Math.Max(deviceWords.Length, specWords.Length);
        }
        
        private double CalculateParameterMatch(DeviceSnapshot device, DeviceSpecification spec)
        {
            double score = 0.0;
            int factors = 0;
            
            // Power consumption match
            if (device.Watts > 0 && spec.PowerConsumption > 0)
            {
                var powerDiff = Math.Abs(device.Watts - spec.PowerConsumption) / Math.Max(device.Watts, spec.PowerConsumption);
                score += Math.Max(0, 1 - powerDiff);
                factors++;
            }
            
            // Current draw match
            if (device.Amps > 0 && spec.CurrentDraw > 0)
            {
                var currentDiff = Math.Abs(device.Amps - spec.CurrentDraw) / Math.Max(device.Amps, spec.CurrentDraw);
                score += Math.Max(0, 1 - currentDiff);
                factors++;
            }
            
            // Candela rating match
            if (device.GetCandelaRating() > 0 && spec.TechnicalSpecs?.ContainsKey("CANDELA_RATING") == true)
            {
                if (int.TryParse(spec.TechnicalSpecs["CANDELA_RATING"].ToString(), out var specCandela))
                {
                    var candelaDiff = Math.Abs(device.GetCandelaRating() - specCandela) / (double)Math.Max(device.GetCandelaRating(), specCandela);
                    score += Math.Max(0, 1 - candelaDiff);
                    factors++;
                }
            }
            
            return factors > 0 ? score / factors : 0.0;
        }
        
        private double CalculateCharacteristicsMatch(DeviceSnapshot device, DeviceSpecification spec)
        {
            double score = 0.0;
            int factors = 0;
            
            // Device function matching
            if (spec.TechnicalSpecs != null)
            {
                if (device.HasStrobe && spec.TechnicalSpecs.ContainsKey("HAS_STROBE") && 
                    bool.TryParse(spec.TechnicalSpecs["HAS_STROBE"].ToString(), out var hasStrobe) && hasStrobe)
                {
                    score += 1; factors++;
                }
                
                if (device.HasSpeaker && spec.TechnicalSpecs.ContainsKey("HAS_SPEAKER") && 
                    bool.TryParse(spec.TechnicalSpecs["HAS_SPEAKER"].ToString(), out var hasSpeaker) && hasSpeaker)
                {
                    score += 1; factors++;
                }
            }
            
            // Environmental rating match
            var deviceEnv = device.GetEnvironmentalRating();
            if (deviceEnv == spec.EnvironmentalRating)
            {
                score += 1; factors++;
            }
            
            return factors > 0 ? score / factors : 0.0;
        }
        
        private double CalculatePatternMatch(DeviceSnapshot device, DeviceSpecification spec)
        {
            var groupKey = GenerateGroupKey(device);
            if (_learnedPatterns.TryGetValue(groupKey, out var pattern))
            {
                return pattern.SuccessfulMappings.ContainsKey(spec.SKU) ? 
                    pattern.SuccessfulMappings[spec.SKU] / (double)pattern.TotalMappings : 0.0;
            }
            return 0.0;
        }
        
        private void GenerateMatchingCriteria(DeviceMatch match)
        {
            foreach (var factor in match.MatchingFactors)
            {
                if (factor.Value > 0.7)
                {
                    match.MatchingCriteria.Add($"Strong {factor.Key.ToLower().Replace('_', ' ')} match ({factor.Value:P0})");
                }
                else if (factor.Value > 0.4)
                {
                    match.MatchingCriteria.Add($"Moderate {factor.Key.ToLower().Replace('_', ' ')} match ({factor.Value:P0})");
                }
            }
        }
        
        private void InferFromFamilyNamePatterns(DeviceSnapshot device, SmartInferenceResult result)
        {
            // Pattern-based inference from device name
            var familyName = device.FamilyName?.ToUpper() ?? "";
            
            if (familyName.Contains("SPECTRALERT"))
            {
                if (!device.CustomProperties?.ContainsKey("MANUFACTURER") == true)
                {
                    result.InferredParameters["MANUFACTURER"] = "System Sensor";
                    result.InferenceConfidence["MANUFACTURER"] = 0.95;
                    result.InferenceMethods["MANUFACTURER"] = "Family name pattern recognition";
                }
            }
            
            if (familyName.Contains("ECO1000") || familyName.Contains("ECO"))
            {
                result.InferredParameters["DEVICE_CATEGORY"] = "SMOKE_DETECTOR";
                result.InferenceConfidence["DEVICE_CATEGORY"] = 0.90;
                result.InferenceMethods["DEVICE_CATEGORY"] = "ECO series pattern";
            }
        }
        
        private void InferFromSimilarDevices(DeviceSnapshot device, SmartInferenceResult result)
        {
            // Statistical inference from similar devices in history
            var similarMappings = _mappingHistory
                .Where(h => h.Success && IsDeviceSimilar(device, h.OriginalDevice))
                .Take(10)
                .ToList();
            
            if (similarMappings.Any())
            {
                // Infer most common specifications
                var commonSpecs = similarMappings
                    .GroupBy(m => m.MappedSpecification.Manufacturer)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                
                if (commonSpecs != null && !result.InferredParameters.ContainsKey("MANUFACTURER"))
                {
                    result.InferredParameters["MANUFACTURER"] = commonSpecs.Key;
                    result.InferenceConfidence["MANUFACTURER"] = (double)commonSpecs.Count() / similarMappings.Count;
                    result.InferenceMethods["MANUFACTURER"] = "Statistical inference from similar devices";
                }
            }
        }
        
        private void InferFromManufacturerPatterns(DeviceSnapshot device, SmartInferenceResult result)
        {
            // Manufacturer-specific patterns
            var manufacturer = device.GetManufacturer();
            if (manufacturer == "System Sensor" || manufacturer.Contains("SYSTEM"))
            {
                if (device.HasStrobe && !result.InferredParameters.ContainsKey("T_TAP_COMPATIBLE"))
                {
                    result.InferredParameters["T_TAP_COMPATIBLE"] = true;
                    result.InferenceConfidence["T_TAP_COMPATIBLE"] = 0.80;
                    result.InferenceMethods["T_TAP_COMPATIBLE"] = "System Sensor T-Tap standard";
                }
            }
        }
        
        private void InferFromElectricalRelationships(DeviceSnapshot device, SmartInferenceResult result)
        {
            // Electrical parameter relationships
            if (device.Watts > 0 && device.Amps == 0)
            {
                var inferredAmps = device.Watts / 24.0; // Standard 24V calculation
                result.InferredParameters["CURRENT_DRAW"] = inferredAmps;
                result.InferenceConfidence["CURRENT_DRAW"] = 0.85;
                result.InferenceMethods["CURRENT_DRAW"] = "Electrical relationship (P=VI, 24V standard)";
            }
            
            if (device.HasStrobe && device.GetCandelaRating() == 0)
            {
                result.InferredParameters["CANDELA"] = 75; // Most common rating
                result.InferenceConfidence["CANDELA"] = 0.60;
                result.InferenceMethods["CANDELA"] = "Default strobe candela rating";
            }
        }
        
        private void InferFromEnvironmentalContext(DeviceSnapshot device, SmartInferenceResult result)
        {
            // Environmental context inference
            var level = device.LevelName?.ToUpper() ?? "";
            
            if (level.Contains("PARKING") || level.Contains("GARAGE"))
            {
                result.InferredParameters["ENVIRONMENTAL_RATING"] = "OUTDOOR";
                result.InferenceConfidence["ENVIRONMENTAL_RATING"] = 0.70;
                result.InferenceMethods["ENVIRONMENTAL_RATING"] = "Parking/garage environment inference";
            }
            
            if (level.Contains("BASEMENT") || level.Contains("MECHANICAL"))
            {
                result.InferredParameters["MOUNTING_TYPE"] = "WALL";
                result.InferenceConfidence["MOUNTING_TYPE"] = 0.75;
                result.InferenceMethods["MOUNTING_TYPE"] = "Basement/mechanical room typical mounting";
            }
        }
        
        private string GenerateGroupKey(DeviceSnapshot device)
        {
            // Create grouping key for similar devices
            var familyNormalized = device.FamilyName?.ToUpper().Replace(" ", "").Replace("-", "") ?? "UNKNOWN";
            var hasStrobe = device.HasStrobe ? "S" : "";
            var hasSpeaker = device.HasSpeaker ? "P" : "";
            var isIsolator = device.IsIsolator ? "I" : "";
            
            return $"{familyNormalized}_{hasStrobe}{hasSpeaker}{isIsolator}";
        }
        
        private TimeSpan EstimateGroupProcessingTime(DeviceGroup group)
        {
            // Estimate processing time based on group characteristics
            var baseTime = TimeSpan.FromMilliseconds(50); // Base processing time
            var deviceCount = group.Devices.Count;
            var complexityMultiplier = 1.0;
            
            // Adjust for complexity
            if (group.RepresentativeDevice.CustomProperties?.Count > 5)
                complexityMultiplier *= 1.2;
            
            if (group.PatternApplied)
                complexityMultiplier *= 0.7; // 30% faster with pattern
            
            return TimeSpan.FromMilliseconds(baseTime.TotalMilliseconds * deviceCount * complexityMultiplier);
        }
        
        private void ApplyDevicePattern(DeviceSnapshot device, DevicePattern pattern)
        {
            // Apply learned pattern to device
            if (pattern.CommonParameters.Any())
            {
                var customProperties = device.CustomProperties ?? new Dictionary<string, object>();
                var updatedProperties = new Dictionary<string, object>(customProperties);
                
                foreach (var param in pattern.CommonParameters)
                {
                    if (!updatedProperties.ContainsKey(param.Key))
                    {
                        updatedProperties[param.Key] = param.Value;
                    }
                }
                
                // Create new DeviceSnapshot with updated properties
                device = device with { CustomProperties = updatedProperties };
            }
        }
        
        private void UpdateLearnedPatterns(ParameterMappingResult mapping)
        {
            var groupKey = GenerateGroupKey(mapping.OriginalSnapshot);
            
            if (!_learnedPatterns.TryGetValue(groupKey, out var pattern))
            {
                pattern = new DevicePattern
                {
                    GroupKey = groupKey,
                    SuccessfulMappings = new Dictionary<string, int>(),
                    CommonParameters = new Dictionary<string, object>(),
                    TotalMappings = 0
                };
                _learnedPatterns[groupKey] = pattern;
            }
            
            pattern.TotalMappings++;
            
            var sku = mapping.DeviceSpecification?.SKU;
            if (!string.IsNullOrEmpty(sku))
            {
                pattern.SuccessfulMappings[sku] = pattern.SuccessfulMappings.GetValueOrDefault(sku, 0) + 1;
            }
            
            // Update common parameters
            if (mapping.ExtractedParameters?.Any() == true)
            {
                foreach (var param in mapping.ExtractedParameters)
                {
                    pattern.CommonParameters[param.Key] = param.Value;
                }
            }
        }
        
        private bool IsDeviceSimilar(DeviceSnapshot device1, DeviceSnapshot device2)
        {
            return GenerateGroupKey(device1) == GenerateGroupKey(device2);
        }
        
        private BatchStatistics GenerateBatchStatistics(List<ParameterMappingResult> results, DateTime startTime)
        {
            return new BatchStatistics
            {
                TotalDevices = results.Count,
                SuccessfulMappings = results.Count(r => r.Success),
                FailedMappings = results.Count(r => !r.Success),
                AverageProcessingTime = results.Any() ? TimeSpan.FromMilliseconds(results.Average(r => r.ProcessingTime.TotalMilliseconds)) : TimeSpan.Zero,
                MaxProcessingTime = results.Any() ? results.Max(r => r.ProcessingTime) : TimeSpan.Zero,
                MinProcessingTime = results.Any() ? results.Min(r => r.ProcessingTime) : TimeSpan.Zero,
                RepositoryHitRate = results.Count(r => r.DeviceSpecification != null) / (double)Math.Max(results.Count, 1),
                TotalProcessingTime = DateTime.Now - startTime
            };
        }
    }
    
    #region Supporting Classes
    
    public class BatchProcessingOptions
    {
        public bool UseParallelProcessing { get; set; } = true;
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public bool ApplyLearning { get; set; } = true;
        public bool OptimizeGrouping { get; set; } = true;
    }
    
    public class BatchMappingResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<ParameterMappingResult> ProcessedDevices { get; set; }
        public BatchStatistics Statistics { get; set; }
        public List<string> OptimizationApplied { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
    
    public class BatchStatistics
    {
        public int TotalDevices { get; set; }
        public int SuccessfulMappings { get; set; }
        public int FailedMappings { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public TimeSpan MaxProcessingTime { get; set; }
        public TimeSpan MinProcessingTime { get; set; }
        public double RepositoryHitRate { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
    }
    
    public class DeviceGroup
    {
        public string GroupKey { get; set; }
        public List<DeviceSnapshot> Devices { get; set; }
        public DeviceSnapshot RepresentativeDevice { get; set; }
        public TimeSpan EstimatedProcessingTime { get; set; }
        public bool PatternApplied { get; set; }
    }
    
    public class DevicePattern
    {
        public string GroupKey { get; set; }
        public Dictionary<string, int> SuccessfulMappings { get; set; }
        public Dictionary<string, object> CommonParameters { get; set; }
        public int TotalMappings { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
    
    public class DeviceMappingHistory
    {
        public DeviceSnapshot OriginalDevice { get; set; }
        public DeviceSpecification MappedSpecification { get; set; }
        public DateTime MappingTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
    }
    
    public class IntelligentMatchingResult
    {
        public DeviceSnapshot InputDevice { get; set; }
        public List<DeviceMatch> Matches { get; set; }
        public DeviceMatch BestMatch { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public DateTime AnalysisTime { get; set; }
    }
    
    public class DeviceMatch
    {
        public DeviceSpecification Specification { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> MatchingCriteria { get; set; }
        public Dictionary<string, double> MatchingFactors { get; set; }
    }
    
    public class SmartInferenceResult
    {
        public DeviceSnapshot OriginalDevice { get; set; }
        public Dictionary<string, object> InferredParameters { get; set; }
        public Dictionary<string, double> InferenceConfidence { get; set; }
        public Dictionary<string, string> InferenceMethods { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
    
    #endregion
}