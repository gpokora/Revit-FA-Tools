using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Interfaces.Analysis;
using Revit_FA_Tools.Core.Services.Analysis.DeviceFilters;
using Revit_FA_Tools.Core.Services.Analysis.Pipeline;

namespace Revit_FA_Tools.Core.Services.Analysis.Pipeline.Stages
{
    /// <summary>
    /// Pipeline stage that filters devices based on circuit type requirements
    /// </summary>
    public class DeviceFilteringStage : IAnalysisStage<List<FamilyInstance>, List<FamilyInstance>>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly object _logger;

        public string StageName => "Device Filtering";

        public DeviceFilteringStage(IServiceProvider serviceProvider, object logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
        }

        public async Task<List<FamilyInstance>> ExecuteAsync(List<FamilyInstance> input, AnalysisContext context)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var circuitType = context.CircuitType;
            context.ReportProgress(StageName, $"Filtering {input.Count} elements for {circuitType} analysis...", 30);
            
            System.Diagnostics.Debug.WriteLine($"Starting device filtering for {circuitType}: {input.Count} candidate elements");

            try
            {
                // Get the appropriate filter for this circuit type
                var filter = GetFilterForCircuitType(circuitType);
                if (filter == null)
                {
                    throw new InvalidOperationException($"No device filter available for circuit type {circuitType}");
                }

                System.Diagnostics.Debug.WriteLine($"Using filter: {filter.GetType().Name}");

                // Execute filtering
                var filteredDevices = await filter.FilterDevicesAsync(input);

                // Store filtering results in context for later stages
                context.SetSharedData("FilteredDeviceCount", filteredDevices.Count);
                context.SetSharedData("FilterType", filter.GetType().Name);
                context.SetSharedData("FilteringResults", CreateFilteringResults(input, filteredDevices, filter));

                context.ReportProgress(StageName, $"Selected {filteredDevices.Count} {circuitType} devices", 40);
                
                System.Diagnostics.Debug.WriteLine($"Device filtering complete: {filteredDevices.Count} devices selected for {circuitType} analysis");

                // Log detailed filtering statistics
                LogFilteringStatistics(input, filteredDevices, filter, circuitType);

                return filteredDevices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Device filtering failed for {circuitType}: {ex.Message}");
                throw new InvalidOperationException($"Device filtering failed: {ex.Message}", ex);
            }
        }

        public bool CanExecute(List<FamilyInstance> input, AnalysisContext context)
        {
            return input != null && 
                   Enum.IsDefined(typeof(CircuitType), context.CircuitType) &&
                   GetFilterForCircuitType(context.CircuitType) != null;
        }

        /// <summary>
        /// Gets the appropriate device filter for the specified circuit type
        /// </summary>
        private IDeviceFilter GetFilterForCircuitType(CircuitType circuitType)
        {
            try
            {
                // Try to get keyed service first (preferred) - fallback to basic service resolution
                // var keyedFilter = _serviceProvider.GetKeyedService<IDeviceFilter>(circuitType);
                // if (keyedFilter != null)
                // {
                //     return keyedFilter;
                // }

                // Fall back to getting specific filter types
                return circuitType switch
                {
                    CircuitType.IDNET => (IDeviceFilter)_serviceProvider.GetService(typeof(IDNETDeviceFilter)),
                    CircuitType.IDNAC => (IDeviceFilter)_serviceProvider.GetService(typeof(IDNACDeviceFilter)),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resolve device filter for circuit type {circuitType}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates detailed filtering results for analysis
        /// </summary>
        private FilteringResults CreateFilteringResults(List<FamilyInstance> input, List<FamilyInstance> output, IDeviceFilter filter)
        {
            var results = new FilteringResults
            {
                TotalCandidates = input.Count,
                FilteredDevices = output.Count,
                ExcludedDevices = input.Count - output.Count,
                FilterType = filter.GetType().Name,
                CircuitType = filter.SupportedCircuitType
            };

            // Analyze exclusion reasons
            var excludedDevices = input.Except(output).ToList();
            var exclusionReasons = new Dictionary<string, int>();

            foreach (var device in excludedDevices)
            {
                try
                {
                    var filterResult = filter.GetFilterReason(device);
                    if (!filterResult.IsIncluded)
                    {
                        var reason = filterResult.Reason ?? "Unknown";
                        exclusionReasons[reason] = exclusionReasons.GetValueOrDefault(reason, 0) + 1;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting filter reason for device {device.Id}: {ex.Message}");
                    exclusionReasons["Error getting reason"] = exclusionReasons.GetValueOrDefault("Error getting reason", 0) + 1;
                }
            }

            results.ExclusionReasons = exclusionReasons;

            // Analyze inclusion reasons
            var inclusionReasons = new Dictionary<string, int>();
            foreach (var device in output)
            {
                try
                {
                    var filterResult = filter.GetFilterReason(device);
                    if (filterResult.IsIncluded)
                    {
                        var reason = filterResult.Reason ?? "Unknown";
                        inclusionReasons[reason] = inclusionReasons.GetValueOrDefault(reason, 0) + 1;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting filter reason for included device {device.Id}: {ex.Message}");
                    inclusionReasons["Error getting reason"] = inclusionReasons.GetValueOrDefault("Error getting reason", 0) + 1;
                }
            }

            results.InclusionReasons = inclusionReasons;

            return results;
        }

        /// <summary>
        /// Logs detailed filtering statistics for debugging
        /// </summary>
        private void LogFilteringStatistics(List<FamilyInstance> input, List<FamilyInstance> output, IDeviceFilter filter, CircuitType circuitType)
        {
            System.Diagnostics.Debug.WriteLine($"=== {circuitType} Device Filtering Statistics ===");
            System.Diagnostics.Debug.WriteLine($"Total candidates: {input.Count}");
            System.Diagnostics.Debug.WriteLine($"Devices included: {output.Count}");
            System.Diagnostics.Debug.WriteLine($"Devices excluded: {input.Count - output.Count}");
            System.Diagnostics.Debug.WriteLine($"Filter efficiency: {(double)output.Count / input.Count * 100:F1}%");

            // Log category breakdown for included devices
            var includedCategories = output
                .GroupBy(d => d.Category?.Name ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            if (includedCategories.Any())
            {
                System.Diagnostics.Debug.WriteLine("Top included device categories:");
                foreach (var category in includedCategories)
                {
                    System.Diagnostics.Debug.WriteLine($"  {category.Key}: {category.Count()} devices");
                }
            }

            // Log family breakdown for included devices
            var includedFamilies = output
                .GroupBy(d => d.Symbol?.Family?.Name ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToList();

            if (includedFamilies.Any())
            {
                System.Diagnostics.Debug.WriteLine("Top included device families:");
                foreach (var family in includedFamilies)
                {
                    System.Diagnostics.Debug.WriteLine($"  {family.Key}: {family.Count()} devices");
                }
            }
        }
    }

    /// <summary>
    /// Results of device filtering operation
    /// </summary>
    public class FilteringResults
    {
        public int TotalCandidates { get; set; }
        public int FilteredDevices { get; set; }
        public int ExcludedDevices { get; set; }
        public string FilterType { get; set; }
        public CircuitType CircuitType { get; set; }
        public Dictionary<string, int> ExclusionReasons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> InclusionReasons { get; set; } = new Dictionary<string, int>();
        public DateTime FilteringTimestamp { get; set; } = DateTime.Now;
    }
}