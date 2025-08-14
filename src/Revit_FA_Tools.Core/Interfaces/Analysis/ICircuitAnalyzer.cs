using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Models.Analysis;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Services.Analysis.Pipeline;

namespace Revit_FA_Tools.Core.Interfaces.Analysis
{
    /// <summary>
    /// Interface for circuit-specific analyzers in the unified analysis framework
    /// </summary>
    public interface ICircuitAnalyzer
    {
        /// <summary>
        /// Gets the type of circuit this analyzer supports
        /// </summary>
        CircuitType SupportedCircuitType { get; }

        /// <summary>
        /// Performs circuit-specific analysis on the provided devices
        /// </summary>
        /// <param name="devices">List of device specifications to analyze</param>
        /// <param name="context">Analysis context containing settings and shared data</param>
        /// <returns>Circuit-specific analysis results</returns>
        Task<IAnalysisResult> AnalyzeAsync(List<DeviceSpecification> devices, AnalysisContext context);

        /// <summary>
        /// Determines if this analyzer can handle the specified circuit type
        /// </summary>
        /// <param name="circuitType">The circuit type to check</param>
        /// <returns>True if this analyzer supports the circuit type</returns>
        bool CanAnalyze(CircuitType circuitType);
    }

    /// <summary>
    /// Base interface for all analysis results
    /// </summary>
    public interface IAnalysisResult
    {
        /// <summary>
        /// Gets the type of circuit analyzed
        /// </summary>
        CircuitType CircuitType { get; }

        /// <summary>
        /// Gets the timestamp when the analysis was performed
        /// </summary>
        DateTime AnalysisTimestamp { get; }

        /// <summary>
        /// Gets the current status of the analysis
        /// </summary>
        AnalysisStatus Status { get; }

        /// <summary>
        /// Gets the list of devices included in the analysis
        /// </summary>
        List<DeviceSpecification> Devices { get; }

        /// <summary>
        /// Gets analysis metrics as key-value pairs
        /// </summary>
        Dictionary<string, object> Metrics { get; }

        /// <summary>
        /// Gets list of warning messages from the analysis
        /// </summary>
        List<string> Warnings { get; }

        /// <summary>
        /// Gets list of error messages from the analysis
        /// </summary>
        List<string> Errors { get; }
    }

    /// <summary>
    /// Types of fire alarm circuits supported by the unified analysis framework
    /// </summary>
    public enum CircuitType
    {
        /// <summary>
        /// Intelligent Detection Network (initiating devices)
        /// </summary>
        IDNET,

        /// <summary>
        /// Intelligent Notification Appliance Circuit
        /// </summary>
        IDNAC,

        /// <summary>
        /// Conventional Notification Appliance Circuit
        /// </summary>
        NAC,

        /// <summary>
        /// Conventional detection circuit
        /// </summary>
        Conventional
    }

    /// <summary>
    /// Status of an analysis operation
    /// </summary>
    public enum AnalysisStatus
    {
        /// <summary>
        /// Analysis has not started
        /// </summary>
        NotStarted,

        /// <summary>
        /// Analysis is currently running
        /// </summary>
        InProgress,

        /// <summary>
        /// Analysis completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Analysis failed with errors
        /// </summary>
        Failed,

        /// <summary>
        /// Analysis was cancelled by user
        /// </summary>
        Cancelled
    }
}