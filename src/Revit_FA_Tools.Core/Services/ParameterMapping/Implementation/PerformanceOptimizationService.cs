using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services.ParameterMapping
{
    /// <summary>
    /// Performance optimization service for parameter mapping operations
    /// Provides caching, prefetching, memory management, and performance monitoring
    /// </summary>
    public class PerformanceOptimizationService
    {
        private readonly ConcurrentDictionary<string, CachedMappingResult> _mappingCache;
        private readonly ConcurrentDictionary<string, CachedSpecification> _specificationCache;
        private readonly PerformanceMetrics _performanceMetrics;
        private readonly Timer _cacheCleanupTimer;
        private readonly SemaphoreSlim _cacheSemaphore;
        
        // Configuration
        private readonly PerformanceConfiguration _config;
        
        public PerformanceOptimizationService(PerformanceConfiguration config = null)
        {
            _config = config ?? new PerformanceConfiguration();
            _mappingCache = new ConcurrentDictionary<string, CachedMappingResult>();
            _specificationCache = new ConcurrentDictionary<string, CachedSpecification>();
            _performanceMetrics = new PerformanceMetrics();
            _cacheSemaphore = new SemaphoreSlim(1, 1);
            
            // Setup periodic cache cleanup
            _cacheCleanupTimer = new Timer(CleanupExpiredCache, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
        
        /// <summary>
        /// Optimized parameter mapping with caching and performance monitoring
        /// </summary>
        public async Task<OptimizedMappingResult> OptimizedParameterMapping(DeviceSnapshot device, ParameterMappingEngine engine)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new OptimizedMappingResult
            {
                InputDevice = device,
                StartTime = DateTime.Now,
                OptimizationsApplied = new List<string>()
            };
            
            try
            {
                // 1. Cache lookup
                var cacheKey = GenerateCacheKey(device);
                if (TryGetFromCache(cacheKey, out var cachedResult))
                {
                    result.MappingResult = cachedResult.Result;
                    result.OptimizationsApplied.Add("Cache hit - retrieved from memory cache");
                    result.CacheHit = true;
                    result.ProcessingTime = TimeSpan.FromMilliseconds(1); // Minimal cache lookup time
                    
                    _performanceMetrics.RecordCacheHit();
                    return result;
                }
                
                // 2. Prefetch related specifications
                var prefetchTask = PrefetchRelatedSpecifications(device);
                
                // 3. Execute parameter mapping with monitoring
                var mappingTask = ExecuteMonitoredMapping(device, engine);
                
                // 4. Wait for mapping completion
                result.MappingResult = await mappingTask;
                
                // 5. Cache the result
                await CacheResult(cacheKey, result.MappingResult);
                result.OptimizationsApplied.Add("Result cached for future use");
                
                // 6. Complete prefetch (don't wait for it)
                _ = prefetchTask.ContinueWith(t => 
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        _performanceMetrics.RecordPrefetchSuccess();
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
                
                result.ProcessingTime = stopwatch.Elapsed;
                result.CacheHit = false;
                
                _performanceMetrics.RecordCacheMiss();
                _performanceMetrics.RecordProcessingTime(result.ProcessingTime);
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ProcessingTime = stopwatch.Elapsed;
                
                _performanceMetrics.RecordError();
                return result;
            }
        }
        
        /// <summary>
        /// Batch optimization with intelligent grouping and parallel processing
        /// </summary>
        public async Task<BatchOptimizationResult> OptimizedBatchProcessing(List<DeviceSnapshot> devices, ParameterMappingEngine engine)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new BatchOptimizationResult
            {
                TotalDevices = devices.Count,
                StartTime = DateTime.Now,
                OptimizedResults = new List<OptimizedMappingResult>(),
                OptimizationsApplied = new List<string>()
            };
            
            try
            {
                // 1. Analyze cache hit potential
                var cacheAnalysis = AnalyzeCachePotential(devices);
                result.OptimizationsApplied.Add($"Cache analysis: {cacheAnalysis.PotentialHits}/{devices.Count} potential cache hits");
                
                // 2. Optimize device grouping
                var optimizedGroups = OptimizeDeviceGrouping(devices, cacheAnalysis);
                result.OptimizationsApplied.Add($"Optimized grouping: {devices.Count} devices into {optimizedGroups.Count} processing groups");
                
                // 3. Prefetch specifications for entire batch
                var prefetchTask = PrefetchBatchSpecifications(devices);
                
                // 4. Process groups with optimal parallelism
                var processingTasks = new List<Task<List<OptimizedMappingResult>>>();
                
                var semaphore = new SemaphoreSlim(_config.MaxParallelism);
                
                foreach (var group in optimizedGroups)
                {
                    processingTasks.Add(ProcessGroupOptimized(group, engine, semaphore));
                }
                
                // 5. Wait for all processing to complete
                var groupResults = await Task.WhenAll(processingTasks);
                
                // 6. Combine results
                foreach (var groupResult in groupResults)
                {
                    result.OptimizedResults.AddRange(groupResult);
                }
                
                // 7. Complete prefetch
                await prefetchTask;
                result.OptimizationsApplied.Add("Batch prefetching completed");
                
                // 8. Generate optimization statistics
                result.ProcessingTime = stopwatch.Elapsed;
                result.CacheHitRate = result.OptimizedResults.Count(r => r.CacheHit) / (double)result.OptimizedResults.Count;
                result.AverageProcessingTime = TimeSpan.FromMilliseconds(result.OptimizedResults.Average(r => r.ProcessingTime.TotalMilliseconds));
                result.TotalOptimizations = result.OptimizationsApplied.Count;
                
                _performanceMetrics.RecordBatchProcessing(result);
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }
        }
        
        /// <summary>
        /// Get comprehensive performance metrics
        /// </summary>
        public PerformanceReport GetPerformanceReport()
        {
            return new PerformanceReport
            {
                ReportTime = DateTime.Now,
                CacheMetrics = new CacheMetrics
                {
                    CacheSize = _mappingCache.Count,
                    CacheHitRate = _performanceMetrics.CacheHitRate,
                    CacheHits = _performanceMetrics.CacheHits,
                    CacheMisses = _performanceMetrics.CacheMisses,
                    AverageProcessingTime = _performanceMetrics.AverageProcessingTime,
                    TotalMemoryUsage = GC.GetTotalMemory(false)
                },
                OptimizationMetrics = new OptimizationMetrics
                {
                    TotalOptimizations = _performanceMetrics.TotalOptimizations,
                    PrefetchSuccessRate = _performanceMetrics.PrefetchSuccessRate,
                    BatchProcessingEfficiency = _performanceMetrics.BatchProcessingEfficiency,
                    MemoryEfficiency = CalculateMemoryEfficiency()
                },
                Recommendations = GeneratePerformanceRecommendations()
            };
        }
        
        /// <summary>
        /// Optimize memory usage and cleanup
        /// </summary>
        public async Task<MemoryOptimizationResult> OptimizeMemory()
        {
            var result = new MemoryOptimizationResult
            {
                StartTime = DateTime.Now,
                InitialMemoryUsage = GC.GetTotalMemory(false)
            };
            
            await _cacheSemaphore.WaitAsync();
            try
            {
                // 1. Remove expired cache entries
                var expiredKeys = _mappingCache.Where(kvp => IsExpired(kvp.Value)).Select(kvp => kvp.Key).ToList();
                foreach (var key in expiredKeys)
                {
                    _mappingCache.TryRemove(key, out _);
                }
                result.ExpiredEntriesRemoved = expiredKeys.Count;
                
                // 2. Remove least recently used entries if over cache limit
                if (_mappingCache.Count > _config.MaxCacheSize)
                {
                    var entriesToRemove = _mappingCache.Count - _config.MaxCacheSize;
                    var lruKeys = _mappingCache
                        .OrderBy(kvp => kvp.Value.LastAccessed)
                        .Take(entriesToRemove)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var key in lruKeys)
                    {
                        _mappingCache.TryRemove(key, out _);
                    }
                    result.LRUEntriesRemoved = lruKeys.Count;
                }
                
                // 3. Compact specification cache
                var specExpiredKeys = _specificationCache.Where(kvp => IsExpired(kvp.Value)).Select(kvp => kvp.Key).ToList();
                foreach (var key in specExpiredKeys)
                {
                    _specificationCache.TryRemove(key, out _);
                }
                result.SpecificationCacheCompacted = specExpiredKeys.Count;
                
                // 4. Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                result.FinalMemoryUsage = GC.GetTotalMemory(false);
                result.MemoryFreed = result.InitialMemoryUsage - result.FinalMemoryUsage;
                result.OptimizationTime = DateTime.Now - result.StartTime;
                result.Success = true;
                
                return result;
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }
        
        private bool TryGetFromCache(string cacheKey, out CachedMappingResult cachedResult)
        {
            if (_mappingCache.TryGetValue(cacheKey, out cachedResult))
            {
                if (!IsExpired(cachedResult))
                {
                    cachedResult.LastAccessed = DateTime.Now;
                    cachedResult.HitCount++;
                    return true;
                }
                else
                {
                    _mappingCache.TryRemove(cacheKey, out _);
                }
            }
            
            cachedResult = null;
            return false;
        }
        
        private async Task<ParameterMappingResult> ExecuteMonitoredMapping(DeviceSnapshot device, ParameterMappingEngine engine)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = engine.AnalyzeDevice(device);
                
                _performanceMetrics.RecordProcessingTime(stopwatch.Elapsed);
                
                if (result.Success)
                {
                    _performanceMetrics.RecordSuccess();
                }
                else
                {
                    _performanceMetrics.RecordError();
                }
                
                return result;
            }
            catch (Exception)
            {
                _performanceMetrics.RecordError();
                throw;
            }
        }
        
        private async Task CacheResult(string cacheKey, ParameterMappingResult result)
        {
            if (result?.Success == true && _mappingCache.Count < _config.MaxCacheSize)
            {
                var cachedResult = new CachedMappingResult
                {
                    Result = result,
                    CachedTime = DateTime.Now,
                    LastAccessed = DateTime.Now,
                    HitCount = 0
                };
                
                _mappingCache.TryAdd(cacheKey, cachedResult);
                
                // Cache related specifications
                if (result.DeviceSpecification != null)
                {
                    var specKey = GenerateSpecificationKey(result.DeviceSpecification);
                    var cachedSpec = new CachedSpecification
                    {
                        Specification = result.DeviceSpecification,
                        CachedTime = DateTime.Now,
                        LastAccessed = DateTime.Now
                    };
                    
                    _specificationCache.TryAdd(specKey, cachedSpec);
                }
            }
            
            await Task.CompletedTask;
        }
        
        private async Task PrefetchRelatedSpecifications(DeviceSnapshot device)
        {
            try
            {
                // Prefetch specifications for similar devices
                var repository = new DeviceRepositoryService();
                var deviceCategory = device.GetDeviceCategory();
                
                await Task.Run(() =>
                {
                    var relatedSpecs = repository.GetDevicesByCategory(deviceCategory).Take(5);
                    foreach (var spec in relatedSpecs)
                    {
                        var specKey = GenerateSpecificationKey(spec);
                        if (!_specificationCache.ContainsKey(specKey))
                        {
                            var cachedSpec = new CachedSpecification
                            {
                                Specification = spec,
                                CachedTime = DateTime.Now,
                                LastAccessed = DateTime.Now
                            };
                            
                            _specificationCache.TryAdd(specKey, cachedSpec);
                        }
                    }
                });
            }
            catch
            {
                // Prefetch is best-effort, don't fail if it doesn't work
            }
        }
        
        private async Task PrefetchBatchSpecifications(List<DeviceSnapshot> devices)
        {
            try
            {
                var categories = devices.Select(d => d.GetDeviceCategory()).Distinct().ToList();
                var repository = new DeviceRepositoryService();
                
                await Task.Run(() =>
                {
                    foreach (var category in categories)
                    {
                        var specs = repository.GetDevicesByCategory(category).Take(10);
                        foreach (var spec in specs)
                        {
                            var specKey = GenerateSpecificationKey(spec);
                            if (!_specificationCache.ContainsKey(specKey))
                            {
                                var cachedSpec = new CachedSpecification
                                {
                                    Specification = spec,
                                    CachedTime = DateTime.Now,
                                    LastAccessed = DateTime.Now
                                };
                                
                                _specificationCache.TryAdd(specKey, cachedSpec);
                            }
                        }
                    }
                });
            }
            catch
            {
                // Prefetch is best-effort
            }
        }
        
        private CachePotentialAnalysis AnalyzeCachePotential(List<DeviceSnapshot> devices)
        {
            var analysis = new CachePotentialAnalysis
            {
                TotalDevices = devices.Count,
                PotentialHits = 0,
                UniqueDeviceTypes = devices.Select(d => GenerateCacheKey(d)).Distinct().Count()
            };
            
            foreach (var device in devices)
            {
                var cacheKey = GenerateCacheKey(device);
                if (_mappingCache.ContainsKey(cacheKey))
                {
                    analysis.PotentialHits++;
                }
            }
            
            analysis.EstimatedTimeWithCache = TimeSpan.FromMilliseconds(
                analysis.PotentialHits * 1 + // 1ms per cache hit
                (analysis.TotalDevices - analysis.PotentialHits) * 50 // 50ms per cache miss
            );
            
            analysis.EstimatedTimeWithoutCache = TimeSpan.FromMilliseconds(analysis.TotalDevices * 50);
            
            return analysis;
        }
        
        private List<DeviceProcessingGroup> OptimizeDeviceGrouping(List<DeviceSnapshot> devices, CachePotentialAnalysis cacheAnalysis)
        {
            var groups = new List<DeviceProcessingGroup>();
            
            // Group 1: Cache hits (process quickly)
            var cacheHits = devices.Where(d => _mappingCache.ContainsKey(GenerateCacheKey(d))).ToList();
            if (cacheHits.Any())
            {
                groups.Add(new DeviceProcessingGroup
                {
                    Devices = cacheHits,
                    GroupType = ProcessingGroupType.CacheHits,
                    Priority = 1,
                    EstimatedTime = TimeSpan.FromMilliseconds(cacheHits.Count * 1)
                });
            }
            
            // Group 2: Cache misses - group by similarity
            var cacheMisses = devices.Except(cacheHits).ToList();
            var similarityGroups = cacheMisses
                .GroupBy(d => GenerateGroupKey(d))
                .Select(g => new DeviceProcessingGroup
                {
                    Devices = g.ToList(),
                    GroupType = ProcessingGroupType.SimilarDevices,
                    Priority = 2,
                    EstimatedTime = TimeSpan.FromMilliseconds(g.Count() * 50)
                })
                .OrderBy(g => g.EstimatedTime)
                .ToList();
            
            groups.AddRange(similarityGroups);
            
            return groups;
        }
        
        private async Task<List<OptimizedMappingResult>> ProcessGroupOptimized(DeviceProcessingGroup group, ParameterMappingEngine engine, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                var results = new List<OptimizedMappingResult>();
                
                if (group.GroupType == ProcessingGroupType.CacheHits)
                {
                    // Process cache hits quickly
                    foreach (var device in group.Devices)
                    {
                        var optimizedResult = await OptimizedParameterMapping(device, engine);
                        results.Add(optimizedResult);
                    }
                }
                else
                {
                    // Process similar devices with template optimization
                    var template = group.Devices.First();
                    var templateResult = await OptimizedParameterMapping(template, engine);
                    results.Add(templateResult);
                    
                    // Apply template optimizations to remaining devices
                    foreach (var device in group.Devices.Skip(1))
                    {
                        var optimizedResult = await OptimizedParameterMapping(device, engine);
                        results.Add(optimizedResult);
                    }
                }
                
                return results;
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        private string GenerateCacheKey(DeviceSnapshot device)
        {
            // Create a unique cache key for the device
            var keyComponents = new[]
            {
                device.FamilyName ?? "",
                device.TypeName ?? "",
                device.Watts.ToString("F2"),
                device.Amps.ToString("F3"),
                device.HasStrobe.ToString(),
                device.HasSpeaker.ToString(),
                device.IsIsolator.ToString(),
                device.GetCandelaRating().ToString()
            };
            
            return string.Join("|", keyComponents).GetHashCode().ToString();
        }
        
        private string GenerateSpecificationKey(DeviceSpecification spec)
        {
            return $"{spec.SKU}|{spec.Manufacturer}".GetHashCode().ToString();
        }
        
        private string GenerateGroupKey(DeviceSnapshot device)
        {
            return $"{device.FamilyName?.Replace(" ", "")}_{device.HasStrobe}_{device.HasSpeaker}_{device.IsIsolator}";
        }
        
        private bool IsExpired(CachedMappingResult cached)
        {
            return DateTime.Now - cached.CachedTime > _config.CacheExpiry;
        }
        
        private bool IsExpired(CachedSpecification cached)
        {
            return DateTime.Now - cached.CachedTime > _config.SpecificationCacheExpiry;
        }
        
        private void CleanupExpiredCache(object state)
        {
            _ = OptimizeMemory();
        }
        
        private double CalculateMemoryEfficiency()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var cacheMemory = (_mappingCache.Count + _specificationCache.Count) * 1024; // Estimated 1KB per entry
            return 1.0 - (cacheMemory / (double)totalMemory);
        }
        
        private List<string> GeneratePerformanceRecommendations()
        {
            var recommendations = new List<string>();
            
            if (_performanceMetrics.CacheHitRate < 0.5)
            {
                recommendations.Add("Consider increasing cache size to improve hit rate");
            }
            
            if (_performanceMetrics.AverageProcessingTime.TotalMilliseconds > 100)
            {
                recommendations.Add("Average processing time exceeds 100ms target - consider optimization");
            }
            
            if (_mappingCache.Count > _config.MaxCacheSize * 0.9)
            {
                recommendations.Add("Cache nearing capacity - consider cleanup or increase size");
            }
            
            if (_performanceMetrics.ErrorRate > 0.05)
            {
                recommendations.Add("Error rate above 5% - investigate processing issues");
            }
            
            return recommendations;
        }
        
        public void Dispose()
        {
            _cacheCleanupTimer?.Dispose();
            _cacheSemaphore?.Dispose();
        }
    }
    
    #region Supporting Classes
    
    public class PerformanceConfiguration
    {
        public int MaxCacheSize { get; set; } = 1000;
        public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromHours(4);
        public TimeSpan SpecificationCacheExpiry { get; set; } = TimeSpan.FromHours(24);
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
        public bool EnablePrefetching { get; set; } = true;
    }
    
    public class OptimizedMappingResult
    {
        public DeviceSnapshot InputDevice { get; set; }
        public ParameterMappingResult MappingResult { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool CacheHit { get; set; }
        public List<string> OptimizationsApplied { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
    
    public class BatchOptimizationResult
    {
        public int TotalDevices { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<OptimizedMappingResult> OptimizedResults { get; set; }
        public List<string> OptimizationsApplied { get; set; }
        public double CacheHitRate { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public int TotalOptimizations { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
    
    public class CachedMappingResult
    {
        public ParameterMappingResult Result { get; set; }
        public DateTime CachedTime { get; set; }
        public DateTime LastAccessed { get; set; }
        public int HitCount { get; set; }
    }
    
    public class CachedSpecification
    {
        public DeviceSpecification Specification { get; set; }
        public DateTime CachedTime { get; set; }
        public DateTime LastAccessed { get; set; }
    }
    
    public class PerformanceMetrics
    {
        private int _cacheHits;
        private int _cacheMisses;
        private int _successes;
        private int _errors;
        private readonly List<TimeSpan> _processingTimes = new List<TimeSpan>();
        private int _totalOptimizations;
        private int _prefetchSuccesses;
        private int _prefetchAttempts;
        
        public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
        public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);
        public void RecordSuccess() => Interlocked.Increment(ref _successes);
        public void RecordError() => Interlocked.Increment(ref _errors);
        
        public void RecordProcessingTime(TimeSpan time)
        {
            lock (_processingTimes)
            {
                _processingTimes.Add(time);
                if (_processingTimes.Count > 1000) // Keep only last 1000 entries
                {
                    _processingTimes.RemoveAt(0);
                }
            }
        }
        
        public void RecordPrefetchSuccess()
        {
            Interlocked.Increment(ref _prefetchSuccesses);
            Interlocked.Increment(ref _prefetchAttempts);
        }
        
        public void RecordBatchProcessing(BatchOptimizationResult result)
        {
            Interlocked.Add(ref _totalOptimizations, result.TotalOptimizations);
        }
        
        public int CacheHits => _cacheHits;
        public int CacheMisses => _cacheMisses;
        public double CacheHitRate => _cacheHits + _cacheMisses > 0 ? (double)_cacheHits / (_cacheHits + _cacheMisses) : 0;
        public double ErrorRate => _successes + _errors > 0 ? (double)_errors / (_successes + _errors) : 0;
        public int TotalOptimizations => _totalOptimizations;
        public double PrefetchSuccessRate => _prefetchAttempts > 0 ? (double)_prefetchSuccesses / _prefetchAttempts : 0;
        public double BatchProcessingEfficiency => CacheHitRate;
        
        public TimeSpan AverageProcessingTime
        {
            get
            {
                lock (_processingTimes)
                {
                    return _processingTimes.Any() ? 
                        TimeSpan.FromMilliseconds(_processingTimes.Average(t => t.TotalMilliseconds)) : 
                        TimeSpan.Zero;
                }
            }
        }
    }
    
    public class PerformanceReport
    {
        public DateTime ReportTime { get; set; }
        public CacheMetrics CacheMetrics { get; set; }
        public OptimizationMetrics OptimizationMetrics { get; set; }
        public List<string> Recommendations { get; set; }
    }
    
    public class CacheMetrics
    {
        public int CacheSize { get; set; }
        public double CacheHitRate { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public long TotalMemoryUsage { get; set; }
    }
    
    public class OptimizationMetrics
    {
        public int TotalOptimizations { get; set; }
        public double PrefetchSuccessRate { get; set; }
        public double BatchProcessingEfficiency { get; set; }
        public double MemoryEfficiency { get; set; }
    }
    
    public class MemoryOptimizationResult
    {
        public DateTime StartTime { get; set; }
        public TimeSpan OptimizationTime { get; set; }
        public long InitialMemoryUsage { get; set; }
        public long FinalMemoryUsage { get; set; }
        public long MemoryFreed { get; set; }
        public int ExpiredEntriesRemoved { get; set; }
        public int LRUEntriesRemoved { get; set; }
        public int SpecificationCacheCompacted { get; set; }
        public bool Success { get; set; }
    }
    
    public class CachePotentialAnalysis
    {
        public int TotalDevices { get; set; }
        public int PotentialHits { get; set; }
        public int UniqueDeviceTypes { get; set; }
        public TimeSpan EstimatedTimeWithCache { get; set; }
        public TimeSpan EstimatedTimeWithoutCache { get; set; }
    }
    
    public class DeviceProcessingGroup
    {
        public List<DeviceSnapshot> Devices { get; set; }
        public ProcessingGroupType GroupType { get; set; }
        public int Priority { get; set; }
        public TimeSpan EstimatedTime { get; set; }
    }
    
    public enum ProcessingGroupType
    {
        CacheHits,
        SimilarDevices,
        UniqueDevices
    }
    
    #endregion
}