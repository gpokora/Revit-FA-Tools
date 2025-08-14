using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Models.Analysis;
using Revit_FA_Tools.Core.Models.Devices;

namespace Revit_FA_Tools.Core.Services.Analysis.Pipeline
{
    /// <summary>
    /// Main pipeline orchestrator for unified fire alarm analysis
    /// </summary>
    public class AnalysisPipeline
    {
        private readonly List<PipelineStageInfo> _stages = new List<PipelineStageInfo>();
        private readonly object _logger;
        private readonly IServiceProvider _serviceProvider;

        public AnalysisPipeline(IServiceProvider serviceProvider, object logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Adds a stage to the pipeline
        /// </summary>
        public AnalysisPipeline AddStage<TStage>(string stageName = null) where TStage : class
        {
            var stageType = typeof(TStage);
            var displayName = stageName ?? stageType.Name;
            
            _stages.Add(new PipelineStageInfo
            {
                StageType = stageType,
                StageName = displayName,
                Order = _stages.Count
            });

            System.Diagnostics.Debug.WriteLine($"Added pipeline stage: {displayName} ({stageType.Name})");
            return this;
        }

        /// <summary>
        /// Executes the complete analysis pipeline
        /// </summary>
        public async Task<IAnalysisResult> ExecuteAsync(AnalysisRequest request, Type analyzerType)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (analyzerType == null)
                throw new ArgumentNullException(nameof(analyzerType));

            var stopwatch = Stopwatch.StartNew();
            var context = CreateAnalysisContext(request);

            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting {request.CircuitType} analysis pipeline with {_stages.Count} stages");
                context.ReportProgress("Pipeline Starting", $"Initializing {request.CircuitType} analysis...", 0);

                // Execute all pipeline stages in sequence
                object currentData = request;
                
                for (int i = 0; i < _stages.Count; i++)
                {
                    var stageInfo = _stages[i];
                    var progressPercent = (int)((i * 80.0) / _stages.Count); // Reserve 20% for final analysis
                    
                    context.ThrowIfCancellationRequested();
                    
                    System.Diagnostics.Debug.WriteLine($"Executing stage {i + 1}/{_stages.Count}: {stageInfo.StageName}");
                    context.ReportProgress(stageInfo.StageName, $"Processing {stageInfo.StageName}...", progressPercent);

                    try
                    {
                        currentData = await ExecuteStageAsync(stageInfo, currentData, context);
                        
                        if (currentData == null)
                        {
                            throw new InvalidOperationException($"Stage {stageInfo.StageName} returned null data");
                        }

                        System.Diagnostics.Debug.WriteLine($"Stage {stageInfo.StageName} completed successfully");
                    }
                    catch (Exception stageEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Stage {stageInfo.StageName} failed: {stageEx.Message}");
                        throw new InvalidOperationException($"Pipeline failed at stage '{stageInfo.StageName}': {stageEx.Message}", stageEx);
                    }
                }

                // Execute final circuit-specific analysis
                context.ReportProgress("Circuit Analysis", "Running circuit-specific analysis...", 80);
                var analyzer = (ICircuitAnalyzer)_serviceProvider.GetService(analyzerType);
                if (analyzer == null)
                {
                    throw new InvalidOperationException($"Could not resolve analyzer of type {analyzerType.Name}");
                }
                
                if (!analyzer.CanAnalyze(request.CircuitType))
                {
                    throw new InvalidOperationException($"Analyzer {analyzerType.Name} cannot analyze {request.CircuitType} circuits");
                }

                var devices = currentData as List<DeviceSpecification>;
                if (devices == null)
                {
                    throw new InvalidOperationException($"Pipeline output is not a device specification list. Got: {currentData?.GetType().Name}");
                }

                var result = await analyzer.AnalyzeAsync(devices, context);
                
                stopwatch.Stop();
                context.ReportProgress("Analysis Complete", $"{request.CircuitType} analysis completed successfully", 100);
                
                System.Diagnostics.Debug.WriteLine($"{request.CircuitType} analysis pipeline completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                
                // Add pipeline metrics to result
                result.Metrics["PipelineExecutionTime"] = stopwatch.Elapsed;
                result.Metrics["StagesExecuted"] = _stages.Count;
                result.Metrics["DevicesProcessed"] = devices.Count;
                
                return result;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"{request.CircuitType} analysis pipeline was cancelled after {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                context.ReportProgress("Analysis Cancelled", "Analysis was cancelled by user", 0);
                
                return CreateCancelledResult(request.CircuitType, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"{request.CircuitType} analysis pipeline failed after {stopwatch.Elapsed.TotalSeconds:F2} seconds: {ex.Message}");
                context.ReportProgress("Analysis Failed", $"Analysis failed: {ex.Message}", 0);
                
                return CreateFailedResult(request.CircuitType, ex, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Executes a specific pipeline stage
        /// </summary>
        private async Task<object> ExecuteStageAsync(PipelineStageInfo stageInfo, object input, AnalysisContext context)
        {
            var stage = _serviceProvider.GetService(stageInfo.StageType);
            if (stage == null)
            {
                throw new InvalidOperationException($"Could not resolve stage of type {stageInfo.StageType.Name}");
            }
            
            // Use reflection to call ExecuteAsync method with proper types
            var executeMethod = stageInfo.StageType.GetMethod("ExecuteAsync");
            if (executeMethod == null)
            {
                throw new InvalidOperationException($"Stage {stageInfo.StageName} does not have an ExecuteAsync method");
            }

            var task = executeMethod.Invoke(stage, new object[] { input, context });
            if (task is Task taskResult)
            {
                await taskResult;
                
                // Get the result from the completed task
                var resultProperty = taskResult.GetType().GetProperty("Result");
                return resultProperty?.GetValue(taskResult);
            }

            throw new InvalidOperationException($"Stage {stageInfo.StageName} ExecuteAsync method did not return a Task");
        }

        /// <summary>
        /// Creates the analysis context for the pipeline
        /// </summary>
        private AnalysisContext CreateAnalysisContext(AnalysisRequest request)
        {
            return new AnalysisContext
            {
                CircuitType = request.CircuitType,
                Request = request,
                Progress = null, // Will be set by caller
                CancellationToken = request.CancellationToken,
                Logger = _logger,
                StartTime = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a cancelled analysis result
        /// </summary>
        private IAnalysisResult CreateCancelledResult(CircuitType circuitType, TimeSpan elapsed)
        {
            return new BasicAnalysisResult
            {
                CircuitType = circuitType,
                AnalysisTimestamp = DateTime.Now,
                Status = AnalysisStatus.Cancelled,
                Devices = new List<DeviceSpecification>(),
                Metrics = new Dictionary<string, object> { ["ExecutionTime"] = elapsed },
                Warnings = new List<string>(),
                Errors = new List<string> { "Analysis was cancelled by user" }
            };
        }

        /// <summary>
        /// Creates a failed analysis result
        /// </summary>
        private IAnalysisResult CreateFailedResult(CircuitType circuitType, Exception exception, TimeSpan elapsed)
        {
            return new BasicAnalysisResult
            {
                CircuitType = circuitType,
                AnalysisTimestamp = DateTime.Now,
                Status = AnalysisStatus.Failed,
                Devices = new List<DeviceSpecification>(),
                Metrics = new Dictionary<string, object> { ["ExecutionTime"] = elapsed },
                Warnings = new List<string>(),
                Errors = new List<string> { $"Pipeline execution failed: {exception.Message}" }
            };
        }

        /// <summary>
        /// Gets information about configured pipeline stages
        /// </summary>
        public IReadOnlyList<PipelineStageInfo> GetStages()
        {
            return _stages.AsReadOnly();
        }
    }

    /// <summary>
    /// Information about a pipeline stage
    /// </summary>
    public class PipelineStageInfo
    {
        public Type StageType { get; set; }
        public string StageName { get; set; }
        public int Order { get; set; }
    }

    /// <summary>
    /// Basic implementation of IAnalysisResult for pipeline control flow
    /// </summary>
    internal class BasicAnalysisResult : IAnalysisResult
    {
        public CircuitType CircuitType { get; set; }
        public DateTime AnalysisTimestamp { get; set; }
        public AnalysisStatus Status { get; set; }
        public List<DeviceSpecification> Devices { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }
    }
}