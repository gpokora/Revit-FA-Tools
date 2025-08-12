using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Revit_FA_Tools;
using Revit_FA_Tools.Core.Services.Interfaces;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using DeviceSnapshot = Revit_FA_Tools.Models.DeviceSnapshot;
using DeviceAssignment = Revit_FA_Tools.Models.DeviceAssignment;
using ModelsValidationResult = Revit_FA_Tools.Models.ValidationResult;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Unified parameter mapping service that consolidates all parameter mapping functionality
    /// </summary>
    public class UnifiedParameterMappingService : IParameterMappingService
    {
        private readonly IValidationService _validationService;
        private readonly FireAlarmConfiguration _configuration;
        
        // Unified caching system
        private readonly ConcurrentDictionary<string, ParameterMappingResult> _mappingCache;
        private readonly ConcurrentDictionary<string, DeviceSpecification> _specificationCache;
        private readonly ConcurrentDictionary<string, Dictionary<string, double>> _learnedPatterns;
        
        // Device repository
        private readonly Dictionary<string, DeviceSpecification> _deviceRepository;
        private readonly Dictionary<string, List<DeviceSpecification>> _categoryIndex;
        private readonly Dictionary<string, DeviceSpecification> _skuIndex;
        
        // Performance monitoring
        private readonly Stopwatch _performanceTimer;
        private readonly Dictionary<string, long> _performanceMetrics;
        
        // Regex patterns for parameter extraction
        private static readonly Regex CurrentDrawPattern = new Regex(@"(\d+(?:\.\d+)?)\s*(?:mA|milliamps?|ma)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VoltageDraw = new Regex(@"(\d+(?:\.\d+)?)\s*(?:V|volts?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CandelaPattern = new Regex(@"(\d+(?:\.\d+)?)\s*(?:cd|candela)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ModelNumberPattern = new Regex(@"[A-Z]{1,4}\d{3,6}[A-Z]{0,3}", RegexOptions.Compiled);

        public UnifiedParameterMappingService(IValidationService validationService, FireAlarmConfiguration configuration)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _mappingCache = new ConcurrentDictionary<string, ParameterMappingResult>();
            _specificationCache = new ConcurrentDictionary<string, DeviceSpecification>();
            _learnedPatterns = new ConcurrentDictionary<string, Dictionary<string, double>>();
            
            _deviceRepository = new Dictionary<string, DeviceSpecification>();
            _categoryIndex = new Dictionary<string, List<DeviceSpecification>>();
            _skuIndex = new Dictionary<string, DeviceSpecification>();
            
            _performanceTimer = new Stopwatch();
            _performanceMetrics = new Dictionary<string, long>();
            
            InitializeDeviceRepository();
        }

        public async Task<ParameterMappingResult> MapParametersAsync(DeviceSnapshot device, MappingOptions options = null)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            options ??= new MappingOptions();
            _performanceTimer.Restart();

            try
            {
                // Check cache first if enabled
                var cacheKey = GenerateCacheKey(device, options);
                if (options.UseCache && _mappingCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    return cachedResult;
                }

                // Create result object
                var result = new ParameterMappingResult
                {
                    Success = true,
                    MappedParameters = new Dictionary<string, object>()
                };

                // Step 1: Extract parameters from the device
                var extractionOptions = new ExtractionOptions
                {
                    IncludeCalculatedProperties = options?.IncludeElectricalParameters ?? true,
                    IncludePrivateProperties = false
                };
                var extractedParams = await ExtractParametersAsync(device, extractionOptions);
                foreach (var param in extractedParams)
                {
                    result.MappedParameters[param.Key] = param.Value;
                }

                // Step 2: Find device specification if repository enhancement is enabled
                DeviceSpecification specification = null;
                if (options.EnhanceWithRepository)
                {
                    specification = await GetDeviceSpecificationAsync(device.DeviceType, device.FamilyName);
                    if (specification != null)
                    {
                        // Merge specification parameters
                        foreach (var spec in specification.ElectricalSpecs)
                        {
                            if (!result.MappedParameters.ContainsKey(spec.Key))
                            {
                                result.MappedParameters[spec.Key] = spec.Value;
                            }
                        }
                    }
                }

                // Step 3: Apply intelligent inference and learning
                await ApplyIntelligentInference(device, result, specification);

                // Step 4: Validate results if enabled
                if (options.ValidateResults)
                {
                    var validation = await ValidateMappingsAsync(result.MappedParameters);
                    if (!validation.IsValid)
                    {
                        result.Success = false;
                        result.Errors.AddRange(validation.Messages.Where(m => m.Severity >= ValidationSeverity.Error).Select(m => m.Message));
                        result.Warnings.AddRange(validation.Messages.Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.Message));
                    }
                }

                // Step 5: Create enhanced device snapshot
                result.MappedDevice = CreateEnhancedSnapshot(device, result.MappedParameters, specification);

                _performanceTimer.Stop();
                result.ProcessingTime = _performanceTimer.Elapsed;

                // Cache result if enabled
                if (options.UseCache && result.Success)
                {
                    _mappingCache.TryAdd(cacheKey, result);
                }

                // Update performance metrics
                UpdatePerformanceMetrics("MapParametersAsync", _performanceTimer.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _performanceTimer.Stop();
                return new ParameterMappingResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                    ProcessingTime = _performanceTimer.Elapsed
                };
            }
        }

        public async Task<BatchMappingResult> MapParametersBatchAsync(IEnumerable<DeviceSnapshot> devices, MappingOptions options = null)
        {
            if (devices == null)
                throw new ArgumentNullException(nameof(devices));

            options ??= new MappingOptions();
            var deviceList = devices.ToList();
            var overallTimer = Stopwatch.StartNew();

            var result = new BatchMappingResult
            {
                TotalDevices = deviceList.Count,
                Results = new List<ParameterMappingResult>()
            };

            // Process devices in parallel for better performance
            var tasks = deviceList.Select(async device =>
            {
                try
                {
                    var mappingResult = await MapParametersAsync(device, options);
                    if (mappingResult.Success)
                    {
                        result.SuccessfulMappings++;
                    }
                    else
                    {
                        result.FailedMappings++;
                    }
                    return mappingResult;
                }
                catch (Exception ex)
                {
                    result.FailedMappings++;
                    return new ParameterMappingResult
                    {
                        Success = false,
                        Errors = new List<string> { ex.Message }
                    };
                }
            });

            result.Results.AddRange(await Task.WhenAll(tasks));

            overallTimer.Stop();
            result.TotalProcessingTime = overallTimer.Elapsed;
            result.Success = result.FailedMappings == 0;

            UpdatePerformanceMetrics("MapParametersBatchAsync", overallTimer.ElapsedMilliseconds);
            return result;
        }

        public async Task<DeviceSnapshot> EnhanceDeviceAsync(DeviceSnapshot device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            var mappingResult = await MapParametersAsync(device, new MappingOptions
            {
                UseCache = true,
                EnhanceWithRepository = true,
                ValidateResults = true,
                IncludeElectricalParameters = true,
                IncludeLocationParameters = true,
                Strategy = MappingStrategy.Comprehensive
            });

            return mappingResult.Success ? mappingResult.MappedDevice : device;
        }

        public async Task<DeviceSpecification> GetDeviceSpecificationAsync(string deviceType, string model = null)
        {
            if (string.IsNullOrWhiteSpace(deviceType))
                return null;

            var cacheKey = $"{deviceType}:{model ?? ""}";
            if (_specificationCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            // Try direct lookup first
            if (!string.IsNullOrWhiteSpace(model) && _skuIndex.TryGetValue(model, out var specification))
            {
                _specificationCache.TryAdd(cacheKey, specification);
                return specification;
            }

            // Try category-based lookup
            if (_categoryIndex.TryGetValue(deviceType, out var categoryDevices))
            {
                // Find best match based on model similarity
                specification = await FindBestDeviceMatch(deviceType, model, categoryDevices);
                if (specification != null)
                {
                    _specificationCache.TryAdd(cacheKey, specification);
                    return specification;
                }
            }

            // Create fallback specification
            specification = CreateFallbackSpecification(deviceType, model);
            _specificationCache.TryAdd(cacheKey, specification);

            return await Task.FromResult(specification);
        }

        public async Task<Dictionary<string, object>> ExtractParametersAsync(object source, ExtractionOptions options = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            options ??= new ExtractionOptions();
            var parameters = new Dictionary<string, object>();

            if (source is DeviceSnapshot device)
            {
                // Extract from device properties
                ExtractDeviceProperties(device, parameters, options);
                
                // Extract electrical parameters
                if (options.IncludeCalculatedProperties)
                {
                    ExtractElectricalParameters(device, parameters);
                }
            }
            else
            {
                // Extract from generic object
                ExtractFromGenericObject(source, parameters, options);
            }

            return await Task.FromResult(parameters);
        }

        public async Task<ValidationResult> ValidateMappingsAsync(Dictionary<string, object> mappings)
        {
            return await _validationService.ValidateParameterMappingsAsync(mappings);
        }

        public MappingConfiguration GetConfiguration()
        {
            return new MappingConfiguration
            {
                EnableAutoMapping = _configuration.ParameterMappingConfiguration?.EnableAutoMapping ?? true,
                CacheExpirationMinutes = _configuration.ParameterMappingConfiguration?.CacheExpirationMinutes ?? 60,
                ParameterMappings = _configuration.ParameterMappingConfiguration?.ParameterMappings?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, string>(),
                DefaultValues = _configuration.ParameterMappingConfiguration?.DefaultValues?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object>()
            };
        }

        public void UpdateConfiguration(MappingConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Ensure parameter mapping configuration exists
            if (_configuration.ParameterMappingConfiguration == null)
            {
                _configuration.ParameterMappingConfiguration = new ParameterMappingConfiguration();
            }

            _configuration.ParameterMappingConfiguration.EnableAutoMapping = configuration.EnableAutoMapping;
            _configuration.ParameterMappingConfiguration.CacheExpirationMinutes = configuration.CacheExpirationMinutes;
            
            // Clear cache when configuration changes
            ClearCache();
        }

        public void ClearCache()
        {
            _mappingCache.Clear();
            _specificationCache.Clear();
            _performanceMetrics.Clear();
        }

        #region Private Methods

        private void InitializeDeviceRepository()
        {
            try
            {
                // Load from embedded resource first, then fallback to file system
                var catalogJson = LoadDeviceCatalog();
                if (!string.IsNullOrEmpty(catalogJson))
                {
                    var catalog = JsonConvert.DeserializeObject<Dictionary<string, DeviceSpecification>>(catalogJson);
                    
                    foreach (var kvp in catalog)
                    {
                        var spec = kvp.Value;
                        _deviceRepository[kvp.Key] = spec;
                        
                        // Build indexes
                        if (!string.IsNullOrEmpty(spec.DeviceType))
                        {
                            if (!_categoryIndex.ContainsKey(spec.DeviceType))
                            {
                                _categoryIndex[spec.DeviceType] = new List<DeviceSpecification>();
                            }
                            _categoryIndex[spec.DeviceType].Add(spec);
                        }
                        
                        if (!string.IsNullOrEmpty(spec.Model))
                        {
                            _skuIndex[spec.Model] = spec;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with empty repository
                Console.WriteLine($"Warning: Failed to load device catalog: {ex.Message}");
            }
        }

        private string LoadDeviceCatalog()
        {
            // Try to load from embedded resource
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith("AutoCallDeviceCatalog.json"));
                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                // Fall through to file system approach
            }

            // Try to load from file system
            var catalogPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DeviceCatalogs", "AutoCallDeviceCatalog.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitFATools", "AutoCallDeviceCatalog.json")
            };

            foreach (var path in catalogPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllText(path);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return null;
        }

        private async Task ApplyIntelligentInference(DeviceSnapshot device, ParameterMappingResult result, DeviceSpecification specification)
        {
            // Apply learned patterns
            var deviceKey = $"{device.DeviceType}:{device.FamilyName}";
            if (_learnedPatterns.TryGetValue(deviceKey, out var patterns))
            {
                foreach (var pattern in patterns)
                {
                    if (!result.MappedParameters.ContainsKey(pattern.Key) && pattern.Value > 0.7) // Confidence threshold
                    {
                        result.MappedParameters[pattern.Key] = InferParameterValue(device, pattern.Key, pattern.Value);
                    }
                }
            }

            // Apply intelligent defaults based on device type
            ApplyIntelligentDefaults(device, result);

            await Task.CompletedTask;
        }

        private void ApplyIntelligentDefaults(DeviceSnapshot device, ParameterMappingResult result)
        {
            // Infer device function if not present
            var deviceFunction = device.ActualCustomProperties.ContainsKey("DeviceFunction") ? device.ActualCustomProperties["DeviceFunction"]?.ToString() : "";
            if (string.IsNullOrEmpty(deviceFunction))
            {
                var inferredFunction = InferDeviceFunction(device);
                if (!string.IsNullOrEmpty(inferredFunction))
                {
                    result.MappedParameters["DeviceFunction"] = inferredFunction;
                }
            }

            // Infer notification device flag
            if (!result.MappedParameters.ContainsKey("IsNotificationDevice"))
            {
                result.MappedParameters["IsNotificationDevice"] = IsNotificationDevice(device);
            }

            // Infer electrical parameters based on device type
            var isNotificationDevice = device.HasStrobe || device.HasSpeaker || 
                (device.ActualCustomProperties.ContainsKey("IsNotificationDevice") && Convert.ToBoolean(device.ActualCustomProperties["IsNotificationDevice"]));
            if (isNotificationDevice && !result.MappedParameters.ContainsKey("Candela"))
            {
                result.MappedParameters["Candela"] = InferCandelaRating(device);
            }
        }

        private async Task<DeviceSpecification> FindBestDeviceMatch(string deviceType, string model, List<DeviceSpecification> candidates)
        {
            if (candidates == null || !candidates.Any())
                return null;

            if (string.IsNullOrEmpty(model))
            {
                return candidates.FirstOrDefault();
            }

            // Calculate similarity scores
            var scoredCandidates = candidates.Select(c => new
            {
                Specification = c,
                Score = CalculateSimilarity(model, c.Model)
            }).OrderByDescending(x => x.Score);

            var bestMatch = scoredCandidates.FirstOrDefault();
            return bestMatch?.Score > 0.5 ? bestMatch.Specification : null;
        }

        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0;

            // Simple Levenshtein distance-based similarity
            var distance = LevenshteinDistance(source.ToUpperInvariant(), target.ToUpperInvariant());
            var maxLength = Math.Max(source.Length, target.Length);
            return 1.0 - (double)distance / maxLength;
        }

        private int LevenshteinDistance(string source, string target)
        {
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; matrix[i, 0] = i++) { }
            for (int j = 0; j <= target.Length; matrix[0, j] = j++) { }

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        private DeviceSpecification CreateFallbackSpecification(string deviceType, string model)
        {
            return new DeviceSpecification
            {
                DeviceType = deviceType,
                Model = model ?? "Unknown",
                Manufacturer = "Generic",
                ElectricalSpecs = new Dictionary<string, object>
                {
                    ["Voltage"] = 24.0,
                    ["CurrentDraw"] = 1.0,
                    ["PowerConsumption"] = 24.0
                },
                LastUpdated = DateTime.Now
            };
        }

        private void ExtractDeviceProperties(DeviceSnapshot device, Dictionary<string, object> parameters, ExtractionOptions options)
        {
            // Extract basic properties
            parameters["ElementId"] = device.ElementId;
            parameters["DeviceType"] = device.DeviceType;
            parameters["FamilyName"] = device.FamilyName;
            parameters["Level"] = device.Level;
            parameters["Room"] = device.ActualCustomProperties.ContainsKey("Room") ? device.ActualCustomProperties["Room"]?.ToString() : "";

            // Extract electrical properties
            var currentDraw = device.Amps;
            if (currentDraw > 0)
                parameters["CurrentDraw"] = currentDraw;
            
            var candela = device.ActualCustomProperties.ContainsKey("Candela") ? Convert.ToDouble(device.ActualCustomProperties["Candela"]) : 0.0;
            if (candela > 0)
                parameters["Candela"] = candela;

            // Extract location properties if requested
            if (options.IncludeCalculatedProperties)
            {
                parameters["X"] = device.X;
                parameters["Y"] = device.Y;
                parameters["Z"] = device.Z;
            }
        }

        private void ExtractElectricalParameters(DeviceSnapshot device, Dictionary<string, object> parameters)
        {
            // Try to extract electrical parameters from text fields
            ExtractFromText(device.DeviceType, parameters);
            ExtractFromText(device.FamilyName, parameters);
            ExtractFromText(device.TypeName, parameters); // TypeComments doesn't exist, using TypeName
        }

        private void ExtractFromText(string text, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Extract current draw
            var currentMatch = CurrentDrawPattern.Match(text);
            if (currentMatch.Success && double.TryParse(currentMatch.Groups[1].Value, out var current))
            {
                if (!parameters.ContainsKey("CurrentDraw"))
                    parameters["CurrentDraw"] = current;
            }

            // Extract voltage
            var voltageMatch = VoltageDraw.Match(text);
            if (voltageMatch.Success && double.TryParse(voltageMatch.Groups[1].Value, out var voltage))
            {
                if (!parameters.ContainsKey("Voltage"))
                    parameters["Voltage"] = voltage;
            }

            // Extract candela
            var candelaMatch = CandelaPattern.Match(text);
            if (candelaMatch.Success && double.TryParse(candelaMatch.Groups[1].Value, out var candela))
            {
                if (!parameters.ContainsKey("Candela"))
                    parameters["Candela"] = candela;
            }

            // Extract model number
            var modelMatch = ModelNumberPattern.Match(text);
            if (modelMatch.Success)
            {
                if (!parameters.ContainsKey("ModelNumber"))
                    parameters["ModelNumber"] = modelMatch.Value;
            }
        }

        private void ExtractFromGenericObject(object source, Dictionary<string, object> parameters, ExtractionOptions options)
        {
            var properties = source.GetType().GetProperties();
            
            foreach (var prop in properties)
            {
                if (options.PropertiesToExclude?.Contains(prop.Name) == true)
                    continue;

                if (options.PropertiesToInclude?.Any() == true && !options.PropertiesToInclude.Contains(prop.Name))
                    continue;

                try
                {
                    var value = prop.GetValue(source);
                    if (value != null)
                    {
                        parameters[prop.Name] = value;
                    }
                }
                catch
                {
                    // Skip properties that can't be accessed
                }
            }
        }

        private DeviceSnapshot CreateEnhancedSnapshot(DeviceSnapshot originalDevice, Dictionary<string, object> mappedParameters, DeviceSpecification specification)
        {
            // Create enhanced device snapshot with mapped parameters
            var enhancedProperties = new Dictionary<string, object>(originalDevice.ActualCustomProperties);
            
            // Add or update mapped parameters
            enhancedProperties["DeviceFunction"] = GetParameterValue<string>(mappedParameters, "DeviceFunction", 
                originalDevice.ActualCustomProperties.ContainsKey("DeviceFunction") ? originalDevice.ActualCustomProperties["DeviceFunction"]?.ToString() : "");
            enhancedProperties["CurrentDraw"] = GetParameterValue<double>(mappedParameters, "CurrentDraw", originalDevice.Amps);
            enhancedProperties["Candela"] = GetParameterValue<double>(mappedParameters, "Candela", 
                originalDevice.ActualCustomProperties.ContainsKey("Candela") ? Convert.ToDouble(originalDevice.ActualCustomProperties["Candela"]) : 0.0);
            enhancedProperties["IsNotificationDevice"] = GetParameterValue<bool>(mappedParameters, "IsNotificationDevice", 
                originalDevice.HasStrobe || originalDevice.HasSpeaker);
            
            return originalDevice with
            {
                CustomProperties = enhancedProperties
            };
        }

        private T GetParameterValue<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        private string GenerateCacheKey(DeviceSnapshot device, MappingOptions options)
        {
            return $"{device.ElementId}:{device.DeviceType}:{options.Strategy}:{options.EnhanceWithRepository}";
        }

        private object InferParameterValue(DeviceSnapshot device, string parameterName, double confidence)
        {
            // Implement parameter inference logic based on learned patterns
            return null;
        }

        private string InferDeviceFunction(DeviceSnapshot device)
        {
            var deviceType = device.DeviceType?.ToLowerInvariant() ?? "";
            var familyName = device.FamilyName?.ToLowerInvariant() ?? "";

            if (deviceType.Contains("smoke") || familyName.Contains("smoke"))
                return "Smoke Detection";
            if (deviceType.Contains("heat") || familyName.Contains("heat"))
                return "Heat Detection";
            if (deviceType.Contains("pull") || familyName.Contains("pull"))
                return "Manual Pull Station";
            if (deviceType.Contains("horn") || familyName.Contains("horn"))
                return "Audible Notification";
            if (deviceType.Contains("strobe") || familyName.Contains("strobe"))
                return "Visual Notification";

            return null;
        }

        private bool IsNotificationDevice(DeviceSnapshot device)
        {
            var deviceType = device.DeviceType?.ToLowerInvariant() ?? "";
            var familyName = device.FamilyName?.ToLowerInvariant() ?? "";

            return deviceType.Contains("horn") || deviceType.Contains("strobe") || deviceType.Contains("speaker") ||
                   familyName.Contains("horn") || familyName.Contains("strobe") || familyName.Contains("speaker");
        }

        private double InferCandelaRating(DeviceSnapshot device)
        {
            // Default candela ratings based on device type and location
            var isNotificationDev = device.HasStrobe || device.HasSpeaker || 
                (device.ActualCustomProperties.ContainsKey("IsNotificationDevice") && Convert.ToBoolean(device.ActualCustomProperties["IsNotificationDevice"]));
            if (isNotificationDev)
            {
                // Basic inference - could be enhanced with more sophisticated logic
                return 15.0; // Common default candela rating
            }
            return 0.0;
        }

        private void UpdatePerformanceMetrics(string operation, long elapsedMilliseconds)
        {
            _performanceMetrics[operation] = elapsedMilliseconds;
        }

        #endregion
    }
}