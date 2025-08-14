using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Models.Analysis;
using Revit_FA_Tools.Core.Services.Analysis.Pipeline;

namespace Revit_FA_Tools.Core.Services.Analysis.Pipeline.Stages
{
    /// <summary>
    /// Pipeline stage that resolves the analysis scope to specific Revit elements
    /// </summary>
    public class ScopeResolutionStage : IAnalysisStage<AnalysisRequest, List<FamilyInstance>>
    {
        private readonly object _logger;

        public string StageName => "Scope Resolution";

        public ScopeResolutionStage(object logger = null)
        {
            _logger = logger;
        }

        public async Task<List<FamilyInstance>> ExecuteAsync(AnalysisRequest input, AnalysisContext context)
        {
            if (input?.Document == null)
            {
                throw new ArgumentException("Analysis request must contain a valid document", nameof(input));
            }

            context.ReportProgress(StageName, $"Resolving {input.Scope} scope...", 10);
            
            System.Diagnostics.Debug.WriteLine($"Resolving scope: {input.Scope} for {input.CircuitType} analysis");

            List<FamilyInstance> elements;

            try
            {
                elements = input.Scope switch
                {
                    AnalysisScope.ActiveView => await ResolveActiveViewScope(input),
                    AnalysisScope.Selection => await ResolveSelectionScope(input),
                    AnalysisScope.EntireModel => await ResolveEntireModelScope(input),
                    AnalysisScope.Level => await ResolveLevelScope(input),
                    AnalysisScope.Zone => await ResolveZoneScope(input),
                    AnalysisScope.CustomFilter => await ResolveCustomFilterScope(input),
                    _ => throw new NotSupportedException($"Analysis scope {input.Scope} is not supported")
                };

                // Store scope information for later stages
                context.SetSharedData("OriginalScope", input.Scope);
                context.SetSharedData("ScopeElementCount", elements.Count);

                context.ReportProgress(StageName, $"Found {elements.Count} candidate elements", 20);
                
                System.Diagnostics.Debug.WriteLine($"Scope resolution complete: {elements.Count} elements found");

                // Log category breakdown for debugging
                var categoryBreakdown = elements
                    .GroupBy(e => e.Category?.Name ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var category in categoryBreakdown)
                {
                    System.Diagnostics.Debug.WriteLine($"Category '{category.Key}': {category.Value} elements");
                }

                return elements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resolve scope {input.Scope}: {ex.Message}");
                throw new InvalidOperationException($"Scope resolution failed for {input.Scope}: {ex.Message}", ex);
            }
        }

        public bool CanExecute(AnalysisRequest input, AnalysisContext context)
        {
            return input?.Document != null && 
                   Enum.IsDefined(typeof(AnalysisScope), input.Scope);
        }

        /// <summary>
        /// Resolves elements in the active view
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveActiveViewScope(AnalysisRequest request)
        {
            var activeView = request.Document.ActiveView;
            if (activeView == null)
            {
                throw new InvalidOperationException("No active view available for analysis");
            }

            System.Diagnostics.Debug.WriteLine($"Resolving active view scope: {activeView.Name}");

            var collector = new FilteredElementCollector(request.Document, activeView.Id)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(e => e.Symbol?.Family != null)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Active view '{activeView.Name}' contains {collector.Count} family instances");

            return await Task.FromResult(collector);
        }

        /// <summary>
        /// Resolves selected elements
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveSelectionScope(AnalysisRequest request)
        {
            if (request.SelectedElements == null || request.SelectedElements.Count == 0)
            {
                throw new ArgumentException("No elements selected for analysis");
            }

            System.Diagnostics.Debug.WriteLine($"Resolving selection scope: {request.SelectedElements.Count} selected elements");

            var familyInstances = new List<FamilyInstance>();

            foreach (var elementId in request.SelectedElements)
            {
                try
                {
                    var element = request.Document.GetElement(elementId);
                    if (element is FamilyInstance familyInstance && familyInstance.Symbol?.Family != null)
                    {
                        familyInstances.Add(familyInstance);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not resolve selected element {elementId}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Selection resolved to {familyInstances.Count} family instances");

            return await Task.FromResult(familyInstances);
        }

        /// <summary>
        /// Resolves all elements in the entire model
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveEntireModelScope(AnalysisRequest request)
        {
            System.Diagnostics.Debug.WriteLine("Resolving entire model scope");

            var collector = new FilteredElementCollector(request.Document)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(e => e.Symbol?.Family != null)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Entire model contains {collector.Count} family instances");

            return await Task.FromResult(collector);
        }

        /// <summary>
        /// Resolves elements on specific levels
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveLevelScope(AnalysisRequest request)
        {
            // For now, fall back to entire model - can be enhanced later to filter by level
            System.Diagnostics.Debug.WriteLine("Level scope not fully implemented, falling back to entire model");
            return await ResolveEntireModelScope(request);
        }

        /// <summary>
        /// Resolves elements in specific zones
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveZoneScope(AnalysisRequest request)
        {
            // For now, fall back to entire model - can be enhanced later to filter by zone
            System.Diagnostics.Debug.WriteLine("Zone scope not fully implemented, falling back to entire model");
            return await ResolveEntireModelScope(request);
        }

        /// <summary>
        /// Resolves elements using custom filters
        /// </summary>
        private async Task<List<FamilyInstance>> ResolveCustomFilterScope(AnalysisRequest request)
        {
            // For now, fall back to entire model - can be enhanced later for custom filters
            System.Diagnostics.Debug.WriteLine("Custom filter scope not fully implemented, falling back to entire model");
            return await ResolveEntireModelScope(request);
        }
    }
}