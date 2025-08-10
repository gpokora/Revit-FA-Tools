using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools
{

    /// <summary>
    /// Analysis stage enumeration for progress tracking
    /// </summary>
    public enum AnalysisStage
    {
        Initializing,
        CollectingElements,
        ExtractingParameters,
        CalculatingLoads,
        AnalyzingIDNAC,
        AnalyzingIDNET,
        AnalyzingAmplifiers,
        GeneratingRecommendations,
        Completing
    }

    /// <summary>
    /// Interface for device snapshotting service - converts Revit API objects to thread-safe DTOs
    /// </summary>
    public interface IDeviceSnapshotService
    {
        List<DeviceSnapshot> CreateSnapshots(List<FamilyInstance> elements);
        DeviceSnapshot CreateSnapshot(FamilyInstance element);
    }

    /// <summary>
    /// Interface for electrical calculation service
    /// </summary>
    public interface IElectricalCalculationService
    {
        Task<ElectricalResults> CalculateAsync(
            List<DeviceSnapshot> devices,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null);

        ElectricalResults Calculate(List<DeviceSnapshot> devices);
    }

    /// <summary>
    /// Interface for IDNAC analysis service
    /// </summary>
    public interface IIDNACAnalysisService
    {
        Task<IDNACSystemResults> AnalyzeAsync(
            List<DeviceSnapshot> devices,
            string scope,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null);

        IDNACSystemResults Analyze(List<DeviceSnapshot> devices, string scope);
    }

    /// <summary>
    /// Interface for IDNET analysis service
    /// </summary>
    public interface IIDNETAnalysisService
    {
        Task<IDNETSystemResults> AnalyzeAsync(
            List<DeviceSnapshot> devices,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null);

        IDNETSystemResults Analyze(List<DeviceSnapshot> devices);
    }

    /// <summary>
    /// Device snapshot service - converts Revit API objects to thread-safe DTOs on UI thread
    /// </summary>
    public class DeviceSnapshotService : IDeviceSnapshotService
    {
        public List<DeviceSnapshot> CreateSnapshots(List<FamilyInstance> elements)
        {
            if (elements == null) return new List<DeviceSnapshot>();

            var snapshots = new List<DeviceSnapshot>(elements.Count);
            foreach (var element in elements)
            {
                try
                {
                    var snapshot = CreateSnapshot(element);
                    if (snapshot != null) snapshots.Add(snapshot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create snapshot for element {element?.Id?.Value}: {ex.Message}");
                }
            }
            return snapshots;
        }

        public DeviceSnapshot CreateSnapshot(FamilyInstance element)
        {
            if (element == null) return null;

            try
            {
                // Extract all Revit API data on UI thread
                var elementId = element.Id.Value;
                var levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM) 
                    ?? element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                var levelName = levelParam?.AsValueString() ?? element.Host?.Name ?? "Unknown";
                var familyName = element.Symbol?.FamilyName ?? "Unknown";
                var typeName = element.Symbol?.Name ?? "Unknown";

                // Extract electrical parameters
                var watts = ExtractParameterValue(element, "Wattage") ?? 
                           ExtractParameterValue(element, "WATTAGE") ?? 0.0;

                // Use device classification service for proper current/UL calculation
                var classification = CandelaConfigurationService.ClassifyDevice(familyName, typeName);
                var amps = classification.Current;
                var unitLoads = classification.UnitLoads;

                // Determine device characteristics from classification
                var hasStrobe = classification.HasStrobe;
                var hasSpeaker = classification.IsSpeaker;
                var isIsolator = DetermineIsIsolator(familyName, typeName);
                var isRepeater = DetermineIsRepeater(familyName, typeName);

                // Override unit loads for special devices
                if (isIsolator) unitLoads = 4;
                if (isRepeater) 
                {
                    var config = ConfigurationService.Current;
                    unitLoads = config.Repeater.RepeaterUnitLoad;
                }

                return new DeviceSnapshot(
                    (int)elementId, levelName, familyName, typeName,
                    watts, amps, unitLoads,
                    hasStrobe, hasSpeaker, isIsolator, isRepeater);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating device snapshot: {ex.Message}");
                return null;
            }
        }

        private double? ExtractParameterValue(FamilyInstance element, string parameterName)
        {
            try
            {
                var param = element.LookupParameter(parameterName) ?? 
                           element.Symbol?.LookupParameter(parameterName);
                
                if (param?.HasValue == true)
                {
                    return param.AsDouble();
                }
            }
            catch { }
            return null;
        }

        private bool DetermineHasStrobe(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLower();
            return combined.Contains("strobe");
        }

        private bool DetermineHasSpeaker(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLower();
            return combined.Contains("speaker") || combined.Contains("horn");
        }

        private bool DetermineIsIsolator(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLower();
            return combined.Contains("isolator");
        }

        private bool DetermineIsRepeater(string familyName, string typeName)
        {
            var combined = $"{familyName} {typeName}".ToLower();
            return combined.Contains("repeater");
        }

    }

    /// <summary>
    /// Async wrapper for electrical calculations
    /// </summary>
    public class AsyncElectricalCalculationService : IElectricalCalculationService
    {
        private readonly ElectricalCalculator _calculator;

        public AsyncElectricalCalculationService()
        {
            _calculator = new ElectricalCalculator();
        }

        public async Task<ElectricalResults> CalculateAsync(
            List<DeviceSnapshot> devices,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startTime = DateTime.Now;
                    var totalDevices = devices?.Count ?? 0;
                    
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Electrical Analysis",
                        Message = "Calculating electrical loads from device snapshots",
                        Total = totalDevices,
                        Current = 0,
                        ElapsedTime = TimeSpan.Zero
                    });

                    cancellationToken.ThrowIfCancellationRequested();

                    // Process device snapshots without Revit API calls
                    var results = ProcessDeviceSnapshots(devices, progress, cancellationToken);

                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Electrical Analysis",
                        Message = "Electrical analysis complete",
                        Total = totalDevices,
                        Current = totalDevices,
                        ElapsedTime = DateTime.Now - startTime
                    });

                    return results;
                }
                catch (OperationCanceledException)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Cancelled",
                        Message = "Electrical analysis cancelled by user"
                    });
                    throw;
                }
                catch (Exception ex)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Error",
                        Message = $"Electrical analysis failed: {ex.Message}"
                    });
                    throw;
                }
            }, cancellationToken);
        }

        public ElectricalResults Calculate(List<DeviceSnapshot> devices)
        {
            return ProcessDeviceSnapshots(devices, null, CancellationToken.None);
        }

        private ElectricalResults ProcessDeviceSnapshots(List<DeviceSnapshot> devices, 
            IProgress<AnalysisProgress> progress, CancellationToken cancellationToken)
        {
            var results = new ElectricalResults();
            
            // Process devices without any Revit API calls
            var totalCurrent = devices?.Sum(d => d.Amps) ?? 0;
            var totalWattage = devices?.Sum(d => d.Watts) ?? 0;
            var totalUnitLoads = devices?.Sum(d => d.UnitLoads) ?? 0;
            var deviceCount = devices?.Count ?? 0;

            // Group by levels for analysis
            var levelGroups = devices?.GroupBy(d => d.LevelName) ?? Enumerable.Empty<IGrouping<string, DeviceSnapshot>>();
            
            // Create level data entries
            foreach (var levelGroup in levelGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var levelData = new LevelData
                {
                    Devices = levelGroup.Count(),
                    Current = levelGroup.Sum(d => d.Amps),
                    Wattage = levelGroup.Sum(d => d.Watts),
                    Families = levelGroup.GroupBy(d => d.FamilyName)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    RequiresIsolators = levelGroup.Any(d => d.IsIsolator)
                };
                
                results.LevelData[levelGroup.Key] = levelData;
            }

            // TotalCurrent, TotalWattage, TotalDevices, and TotalUnitLoads are computed properties
            // They are automatically calculated from the Elements collection

            return results;
        }
    }

    /// <summary>
    /// Async wrapper for IDNAC analysis
    /// </summary>
    public class AsyncIDNACAnalysisService : IIDNACAnalysisService
    {
        private readonly IDNACAnalyzer _analyzer;

        public AsyncIDNACAnalysisService()
        {
            _analyzer = new IDNACAnalyzer();
        }

        public async Task<IDNACSystemResults> AnalyzeAsync(
            List<DeviceSnapshot> devices,
            string scope,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startTime = DateTime.Now;
                    
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "IDNAC Analysis",
                        Message = "Analyzing notification device requirements - Calculating IDNAC circuit requirements...",
                        ElapsedTime = TimeSpan.Zero
                    });

                    cancellationToken.ThrowIfCancellationRequested();

                    // Perform thread-safe analysis with device snapshots
                    var results = AnalyzeIDNACFromSnapshots(devices, scope, progress, cancellationToken);

                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "IDNAC Analysis",
                        Message = $"IDNAC analysis complete - {results.TotalIdnacsNeeded} circuits required",
                        ElapsedTime = DateTime.Now - startTime
                    });

                    return results;
                }
                catch (OperationCanceledException)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Cancelled",
                        Message = "IDNAC analysis cancelled by user"
                    });
                    throw;
                }
                catch (Exception ex)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Error",
                        Message = $"IDNAC analysis failed: {ex.Message}"
                    });
                    throw;
                }
            }, cancellationToken);
        }

        public IDNACSystemResults Analyze(List<DeviceSnapshot> devices, string scope)
        {
            return AnalyzeIDNACFromSnapshots(devices, scope, null, CancellationToken.None);
        }

        private IDNACSystemResults AnalyzeIDNACFromSnapshots(List<DeviceSnapshot> devices, string scope, 
            IProgress<AnalysisProgress> progress, CancellationToken cancellationToken)
        {
            var config = ConfigurationService.Current;
            var results = new IDNACSystemResults();

            // Filter notification devices only
            var notificationDevices = devices.Where(d => 
                d.HasStrobe || d.HasSpeaker || d.DeviceType == "HORN").ToList();

            if (!notificationDevices.Any())
            {
                return results; // No notification devices found
            }

            // Group by levels and apply balancing exclusions
            var levelGroups = notificationDevices.GroupBy(d => d.Zone);

            foreach (var levelGroup in levelGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var levelDevices = levelGroup.ToList();
                var totalCurrent = levelDevices.Sum(d => d.Amps);
                var totalUnitLoads = levelDevices.Sum(d => d.UnitLoads);

                // Calculate IDNAC requirements with dual limits and spare capacity
                var currentLimit = config.Capacity.IdnacAlarmCurrentLimitA * (1 - config.Spare.SpareFractionDefault);
                var ulLimit = config.Capacity.IdnacStandbyUnitLoadLimit * (1 - config.Spare.SpareFractionDefault);

                var idnacsNeededByCurrent = Math.Ceiling(totalCurrent / currentLimit);
                var idnacsNeededByUL = Math.Ceiling((double)totalUnitLoads / ulLimit);
                var idnacsNeeded = Math.Max(idnacsNeededByCurrent, idnacsNeededByUL);

                var levelResult = new IDNACAnalysisResult
                {
                    Current = totalCurrent,
                    UnitLoads = totalUnitLoads,
                    Devices = levelDevices.Count,
                    IdnacsRequired = (int)idnacsNeeded,
                    LimitingFactor = idnacsNeededByCurrent > idnacsNeededByUL ? "Current" : "Unit Loads",
                    Status = "Analyzed"
                };

                results.LevelResults[levelGroup.Key] = levelResult;
            }

            results.TotalIdnacsNeeded = results.LevelResults.Values.Sum(lr => lr.IdnacsRequired);
            // TotalCurrent, TotalUnitLoads, and TotalDevices are computed properties
            // They are automatically calculated from the LevelResults collection

            return results;
        }
    }

    /// <summary>
    /// Async wrapper for IDNET analysis
    /// </summary>
    public class AsyncIDNETAnalysisService : IIDNETAnalysisService
    {
        private readonly IDNETAnalyzer _analyzer;

        public AsyncIDNETAnalysisService()
        {
            _analyzer = new IDNETAnalyzer();
        }

        public async Task<IDNETSystemResults> AnalyzeAsync(
            List<DeviceSnapshot> devices,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startTime = DateTime.Now;
                    
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "IDNET Analysis",
                        Message = "Analyzing detection device network requirements - Processing fire alarm detection devices...",
                        ElapsedTime = TimeSpan.Zero
                    });

                    cancellationToken.ThrowIfCancellationRequested();

                    // Perform thread-safe analysis with device snapshots
                    var results = AnalyzeIDNETFromSnapshots(devices, progress, cancellationToken);

                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "IDNET Analysis",
                        Message = $"IDNET analysis complete - {results.TotalDevices} devices processed",
                        ElapsedTime = DateTime.Now - startTime
                    });

                    return results;
                }
                catch (OperationCanceledException)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Cancelled",
                        Message = "IDNET analysis cancelled by user"
                    });
                    throw;
                }
                catch (Exception ex)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Error",
                        Message = $"IDNET analysis failed: {ex.Message}"
                    });
                    throw;
                }
            }, cancellationToken);
        }

        public IDNETSystemResults Analyze(List<DeviceSnapshot> devices)
        {
            return AnalyzeIDNETFromSnapshots(devices, null, CancellationToken.None);
        }

        private IDNETSystemResults AnalyzeIDNETFromSnapshots(List<DeviceSnapshot> devices, 
            IProgress<AnalysisProgress> progress, CancellationToken cancellationToken)
        {
            var config = ConfigurationService.Current;
            var results = new IDNETSystemResults();

            // Filter detection devices (non-notification devices)
            var detectionDevices = devices.Where(d => 
                d.DeviceType == "SMOKE_DETECTOR" || 
                d.DeviceType == "HEAT_DETECTOR" || 
                d.DeviceType == "MANUAL_STATION" ||
                d.DeviceType == "MODULE" ||
                (!d.HasStrobe && !d.HasSpeaker && d.DeviceType != "HORN")).ToList();

            if (!detectionDevices.Any())
            {
                return results; // No detection devices found
            }

            var totalDevices = detectionDevices.Count;
            var devicesPerChannel = config.IDNET.UsableDevicesPerChannel;
            var channelsNeeded = Math.Ceiling((double)totalDevices / devicesPerChannel);

            results.TotalDevices = totalDevices;
            results.TotalUnitLoads = detectionDevices.Sum(d => d.UnitLoads);
            
            // Set up system summary with required channels
            results.SystemSummary = new IDNETSystemSummary
            {
                RecommendedNetworkChannels = (int)channelsNeeded,
                SystemRecommendations = new List<string>()
            };

            return results;
        }
    }

    /// <summary>
    /// Comprehensive analysis service that coordinates all analysis types
    /// </summary>
    public class ComprehensiveAnalysisService
    {
        private readonly IDeviceSnapshotService _snapshotService;
        private readonly IElectricalCalculationService _electricalService;
        private readonly IIDNACAnalysisService _idnacService;
        private readonly IIDNETAnalysisService _idnetService;

        public ComprehensiveAnalysisService()
        {
            _snapshotService = new DeviceSnapshotService();
            _electricalService = new AsyncElectricalCalculationService();
            _idnacService = new AsyncIDNACAnalysisService();
            _idnetService = new AsyncIDNETAnalysisService();
        }

        /// <summary>
        /// Perform complete fire alarm system analysis with proper thread safety
        /// </summary>
        public async Task<ComprehensiveAnalysisResults> AnalyzeAsync(
            List<FamilyInstance> elements,
            string scope,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var results = new ComprehensiveAnalysisResults();
            var overallStartTime = DateTime.Now;

            try
            {
                // Stage 1: Create thread-safe snapshots on UI thread (CRITICAL: No Revit API calls beyond this point)
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Creating Snapshots",
                    Message = "Creating thread-safe device snapshots...",
                    ElapsedTime = TimeSpan.Zero
                });

                var deviceSnapshots = _snapshotService.CreateSnapshots(elements);
                results.TotalElementsAnalyzed = deviceSnapshots.Count;

                cancellationToken.ThrowIfCancellationRequested();

                // Stage 2: Electrical Analysis (thread-safe)
                results.ElectricalResults = await _electricalService.CalculateAsync(
                    deviceSnapshots, cancellationToken, progress);

                cancellationToken.ThrowIfCancellationRequested();

                // Stage 3: IDNAC Analysis (thread-safe)
                results.IDNACResults = await _idnacService.AnalyzeAsync(
                    deviceSnapshots, scope, cancellationToken, progress);

                cancellationToken.ThrowIfCancellationRequested();

                // Stage 4: IDNET Analysis (thread-safe)
                results.IDNETResults = await _idnetService.AnalyzeAsync(
                    deviceSnapshots, cancellationToken, progress);

                cancellationToken.ThrowIfCancellationRequested();

                // Stage 5: Finalization
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Completing Analysis",
                    Message = "Analysis complete - generating final results",
                    Current = 100,
                    Total = 100,
                    ElapsedTime = DateTime.Now - overallStartTime
                });

                results.AnalysisCompleted = true;
                results.TotalAnalysisTime = DateTime.Now - overallStartTime;
                results.Scope = scope;
                results.AnalysisStartTime = overallStartTime;
                results.AnalysisEndTime = DateTime.Now;

                return results;
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Cancelled",
                    Message = "Analysis cancelled by user"
                });
                results.AnalysisCompleted = false;
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Error",
                    Message = $"Analysis failed: {ex.Message}"
                });
                results.AnalysisCompleted = false;
                throw;
            }
        }
    }

    /// <summary>
    /// Container for all analysis results
    /// </summary>
    public class ComprehensiveAnalysisResults
    {
        public ElectricalResults ElectricalResults { get; set; }
        public IDNACSystemResults IDNACResults { get; set; }
        public IDNETSystemResults IDNETResults { get; set; }
        public AmplifierRequirements AmplifierResults { get; set; }
        public List<PanelPlacementRecommendation> PanelRecommendations { get; set; }
        
        public bool AnalysisCompleted { get; set; }
        public TimeSpan TotalAnalysisTime { get; set; }
        public DateTime AnalysisStartTime { get; set; }
        public DateTime AnalysisEndTime { get; set; }
        public string Scope { get; set; }
        public int TotalElementsAnalyzed { get; set; }
    }
}