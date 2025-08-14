using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Models.Analysis;

namespace Revit_FA_Tools.Core.Services.Analysis.Pipeline
{
    /// <summary>
    /// Interface for analysis pipeline stages
    /// </summary>
    /// <typeparam name="TInput">Input type for the stage</typeparam>
    /// <typeparam name="TOutput">Output type for the stage</typeparam>
    public interface IAnalysisStage<TInput, TOutput>
    {
        /// <summary>
        /// Gets the name of this stage for logging and progress reporting
        /// </summary>
        string StageName { get; }

        /// <summary>
        /// Executes the analysis stage
        /// </summary>
        /// <param name="input">Input data for the stage</param>
        /// <param name="context">Analysis context with shared data and settings</param>
        /// <returns>Output data from the stage</returns>
        Task<TOutput> ExecuteAsync(TInput input, AnalysisContext context);

        /// <summary>
        /// Determines if this stage can execute with the given input and context
        /// </summary>
        /// <param name="input">Input data to validate</param>
        /// <param name="context">Analysis context to validate</param>
        /// <returns>True if the stage can execute</returns>
        bool CanExecute(TInput input, AnalysisContext context);
    }

    /// <summary>
    /// Context for analysis operations containing shared data and settings
    /// </summary>
    public class AnalysisContext
    {
        /// <summary>
        /// Gets or sets the type of circuit being analyzed
        /// </summary>
        public CircuitType CircuitType { get; set; }

        /// <summary>
        /// Gets or sets the original analysis request
        /// </summary>
        public AnalysisRequest Request { get; set; }

        /// <summary>
        /// Gets or sets the progress reporter for the analysis
        /// </summary>
        public IProgress<AnalysisProgress> Progress { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token for the analysis
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets shared data between pipeline stages
        /// </summary>
        public Dictionary<string, object> SharedData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the logger for the analysis (can be null)
        /// </summary>
        public object Logger { get; set; }

        /// <summary>
        /// Gets or sets the start time of the analysis
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets the elapsed time since analysis started
        /// </summary>
        public TimeSpan ElapsedTime => DateTime.Now - StartTime;

        /// <summary>
        /// Adds or updates shared data
        /// </summary>
        public void SetSharedData<T>(string key, T value)
        {
            SharedData[key] = value;
        }

        /// <summary>
        /// Gets shared data of the specified type
        /// </summary>
        public T GetSharedData<T>(string key, T defaultValue = default)
        {
            if (SharedData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Reports progress for the current operation
        /// </summary>
        public void ReportProgress(string operation, string message, int percentComplete)
        {
            Progress?.Report(new AnalysisProgress
            {
                Operation = operation,
                Message = message,
                PercentComplete = percentComplete,
                ElapsedTime = ElapsedTime,
                IsCompleted = percentComplete >= 100
            });
        }

        /// <summary>
        /// Checks if cancellation was requested and throws if so
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            CancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Progress information for analysis operations
    /// </summary>
    public class AnalysisProgress
    {
        /// <summary>
        /// Gets or sets the current operation name
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets the current progress message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the completion percentage (0-100)
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time since analysis started
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Gets or sets whether the operation is completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets additional details about the progress
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a progress report for a specific stage
        /// </summary>
        public static AnalysisProgress ForStage(string stageName, string message, int percent, TimeSpan elapsed)
        {
            return new AnalysisProgress
            {
                Operation = stageName,
                Message = message,
                PercentComplete = percent,
                ElapsedTime = elapsed,
                IsCompleted = percent >= 100
            };
        }

        /// <summary>
        /// Creates a completion progress report
        /// </summary>
        public static AnalysisProgress Completed(string operation, string message, TimeSpan elapsed)
        {
            return new AnalysisProgress
            {
                Operation = operation,
                Message = message,
                PercentComplete = 100,
                ElapsedTime = elapsed,
                IsCompleted = true
            };
        }
    }
}