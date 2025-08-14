using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Interfaces.Analysis;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Unified request structure for all fire alarm analysis types
    /// </summary>
    public class AnalysisRequest
    {
        /// <summary>
        /// Gets or sets the type of circuit to analyze
        /// </summary>
        public CircuitType CircuitType { get; set; }

        /// <summary>
        /// Gets or sets the scope of analysis
        /// </summary>
        public AnalysisScope Scope { get; set; }

        /// <summary>
        /// Gets or sets the list of selected element IDs for selection-based analysis
        /// </summary>
        public List<ElementId> SelectedElements { get; set; } = new List<ElementId>();

        /// <summary>
        /// Gets or sets the analysis settings
        /// </summary>
        public AnalysisSettings Settings { get; set; } = new AnalysisSettings();

        /// <summary>
        /// Gets or sets the Revit document to analyze
        /// </summary>
        public Document Document { get; set; }

        /// <summary>
        /// Gets or sets the project name for reporting
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token for the analysis operation
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Validates the analysis request
        /// </summary>
        /// <returns>True if the request is valid</returns>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (Document == null)
                errors.Add("Document is required");

            if (Scope == AnalysisScope.Selection && (SelectedElements == null || SelectedElements.Count == 0))
                errors.Add("Selected elements are required for selection-based analysis");

            if (Settings == null)
                errors.Add("Analysis settings are required");

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Settings for fire alarm analysis operations
    /// </summary>
    public class AnalysisSettings
    {
        // IDNAC-specific settings
        /// <summary>
        /// Gets or sets the spare capacity percentage for IDNAC circuits (default: 20%)
        /// </summary>
        public double SpareCapacityPercent { get; set; } = 20.0;

        /// <summary>
        /// Gets or sets whether to include T-Taping analysis for IDNAC devices
        /// </summary>
        public bool IncludeTTapingAnalysis { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to calculate amplifier requirements for speaker circuits
        /// </summary>
        public bool CalculateAmplifierRequirements { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to perform voltage drop calculations
        /// </summary>
        public bool CalculateVoltageDrops { get; set; } = true;

        // IDNET-specific settings
        /// <summary>
        /// Gets or sets whether to analyze network topology for detection loops
        /// </summary>
        public bool AnalyzeNetworkTopology { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate address assignments
        /// </summary>
        public bool ValidateAddressAssignments { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to check zone coverage requirements
        /// </summary>
        public bool CheckZoneCoverage { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to calculate supervision requirements
        /// </summary>
        public bool CalculateSupervisionRequirements { get; set; } = true;

        // Shared settings
        /// <summary>
        /// Gets or sets whether to include detailed reporting in results
        /// </summary>
        public bool IncludeDetailedReporting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate parameter mappings during analysis
        /// </summary>
        public bool ValidateParameterMappings { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to export results automatically
        /// </summary>
        public bool AutoExportResults { get; set; } = false;

        /// <summary>
        /// Gets or sets the export format if auto-export is enabled
        /// </summary>
        public ExportFormat ExportFormat { get; set; } = ExportFormat.Excel;

        /// <summary>
        /// Gets or sets whether to update Revit parameters after analysis
        /// </summary>
        public bool UpdateRevitParameters { get; set; } = false;

        /// <summary>
        /// Creates default settings based on circuit type
        /// </summary>
        public static AnalysisSettings CreateDefaults(CircuitType circuitType)
        {
            var settings = new AnalysisSettings();

            // Adjust defaults based on circuit type
            switch (circuitType)
            {
                case CircuitType.IDNET:
                    settings.IncludeTTapingAnalysis = false;
                    settings.CalculateAmplifierRequirements = false;
                    settings.CalculateVoltageDrops = false;
                    break;

                case CircuitType.IDNAC:
                    settings.AnalyzeNetworkTopology = false;
                    settings.CheckZoneCoverage = false;
                    settings.CalculateSupervisionRequirements = false;
                    break;
            }

            return settings;
        }
    }

    /// <summary>
    /// Scope options for fire alarm analysis
    /// </summary>
    public enum AnalysisScope
    {
        /// <summary>
        /// Analyze elements in the active view only
        /// </summary>
        ActiveView,

        /// <summary>
        /// Analyze selected elements only
        /// </summary>
        Selection,

        /// <summary>
        /// Analyze all elements in the entire model
        /// </summary>
        EntireModel,

        /// <summary>
        /// Analyze elements on a specific level
        /// </summary>
        Level,

        /// <summary>
        /// Analyze elements in a specific zone
        /// </summary>
        Zone,

        /// <summary>
        /// Apply custom filter for analysis
        /// </summary>
        CustomFilter
    }

    /// <summary>
    /// Export format options for analysis results
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// Microsoft Excel format
        /// </summary>
        Excel,

        /// <summary>
        /// Comma-separated values format
        /// </summary>
        CSV,

        /// <summary>
        /// Portable Document Format
        /// </summary>
        PDF,

        /// <summary>
        /// JavaScript Object Notation format
        /// </summary>
        JSON,

        /// <summary>
        /// Extensible Markup Language format
        /// </summary>
        XML
    }
}