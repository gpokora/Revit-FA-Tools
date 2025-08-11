using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Pre-analysis validation gate that runs before IDNAC analysis to ensure model readiness
    /// </summary>
    public class PreAnalysisValidator
    {
        private readonly ModelComponentValidator _modelValidator;

        // Analysis readiness thresholds
        private const double HIGH_ACCURACY_THRESHOLD = 0.95;
        private const double GOOD_ACCURACY_THRESHOLD = 0.80;
        private const double MODERATE_ACCURACY_THRESHOLD = 0.60;
        private const double MINIMUM_ANALYSIS_THRESHOLD = 0.50;

        public PreAnalysisValidator(object document)
        {
            _modelValidator = new ModelComponentValidator(document);
        }

        /// <summary>
        /// Gate decision for analysis readiness
        /// </summary>
        public enum AnalysisGateDecision
        {
            Proceed,              // >80% data quality, no critical issues
            ProceedWithWarnings,  // 60-80% data quality, minor issues noted
            Block,                // <60% data quality or critical parameter issues
            GuidedFix             // Offer to fix common issues automatically
        }

        /// <summary>
        /// Result of pre-analysis validation gate
        /// </summary>
        public class PreAnalysisValidationResult
        {
            public AnalysisGateDecision Decision { get; set; }
            public ModelValidationSummary ValidationSummary { get; set; }
            public string AccuracyEstimate { get; set; }
            public List<string> BlockingIssues { get; set; } = new List<string>();
            public List<string> WarningIssues { get; set; } = new List<string>();
            public List<AutoFixOption> AutoFixOptions { get; set; } = new List<AutoFixOption>();
            public string RecommendedAction { get; set; }
        }

        /// <summary>
        /// Automatic fix options for common validation issues
        /// </summary>
        public class AutoFixOption
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int AffectedDevices { get; set; }
            public string ImpactDescription { get; set; }
            public bool IsRecommended { get; set; }
            public Action AutoFixAction { get; set; }
        }

        /// <summary>
        /// Run pre-analysis validation and determine if analysis should proceed
        /// </summary>
        /// <returns>Pre-analysis validation result with gate decision</returns>
        public PreAnalysisValidationResult ValidateBeforeAnalysis()
        {
            var result = new PreAnalysisValidationResult();

            try
            {
                // Run comprehensive model validation
                result.ValidationSummary = _modelValidator.ValidateModelForAnalysis();

                // Determine analysis gate decision
                result.Decision = DetermineGateDecision(result.ValidationSummary);

                // Generate accuracy estimate
                result.AccuracyEstimate = GenerateAccuracyEstimate(result.ValidationSummary);

                // Identify blocking and warning issues
                CategorizeIssues(result.ValidationSummary, result);

                // Generate auto-fix options
                result.AutoFixOptions = GenerateAutoFixOptions(result.ValidationSummary);

                // Set recommended action
                result.RecommendedAction = GenerateRecommendedAction(result.Decision, result.ValidationSummary);
            }
            catch (Exception ex)
            {
                result.Decision = AnalysisGateDecision.Block;
                result.BlockingIssues.Add($"Validation failed with error: {ex.Message}");
                result.RecommendedAction = "Resolve validation system error before proceeding";
                System.Diagnostics.Debug.WriteLine($"Pre-analysis validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Check if analysis meets minimum requirements for proceeding
        /// </summary>
        /// <param name="summary">Model validation summary</param>
        /// <returns>True if minimum requirements are met</returns>
        public bool MeetsMinimumRequirements(ModelValidationSummary summary)
        {
            if (summary == null) return false;

            // Check critical requirements
            var criticalFailures = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Fail);
            if (criticalFailures > 0)
            {
                // Check if failures are blocking
                var analysisReadiness = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Analysis Readiness");
                if (analysisReadiness?.Status == ValidationStatus.Fail)
                    return false;
            }

            // Check minimum data completeness
            var readinessPercentage = summary.ReadinessPercentage / 100.0;
            if (readinessPercentage < MINIMUM_ANALYSIS_THRESHOLD)
                return false;

            // Must have at least some devices
            if (summary.TotalDevicesFound == 0)
                return false;

            // Must have some valid devices
            if (summary.ValidDevicesCount == 0)
                return false;

            return true;
        }

        #region Private Methods

        /// <summary>
        /// Determine the appropriate gate decision based on validation results
        /// </summary>
        private AnalysisGateDecision DetermineGateDecision(ModelValidationSummary summary)
        {
            var readinessPercentage = summary.ReadinessPercentage / 100.0;
            var criticalFailures = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Fail);
            var hasBlockingIssues = HasBlockingIssues(summary);

            // Block if critical issues exist
            if (hasBlockingIssues || readinessPercentage < MINIMUM_ANALYSIS_THRESHOLD)
            {
                // Check if we can offer guided fixes
                var fixableIssues = CountFixableIssues(summary);
                if (fixableIssues > 0 && !hasBlockingIssues)
                    return AnalysisGateDecision.GuidedFix;
                else
                    return AnalysisGateDecision.Block;
            }

            // Proceed with high confidence
            if (readinessPercentage >= GOOD_ACCURACY_THRESHOLD && criticalFailures == 0)
                return AnalysisGateDecision.Proceed;

            // Proceed with warnings for moderate quality
            if (readinessPercentage >= MODERATE_ACCURACY_THRESHOLD)
                return AnalysisGateDecision.ProceedWithWarnings;

            // Default to guided fix for borderline cases
            return AnalysisGateDecision.GuidedFix;
        }

        /// <summary>
        /// Generate analysis accuracy estimate based on data quality
        /// </summary>
        private string GenerateAccuracyEstimate(ModelValidationSummary summary)
        {
            var readinessPercentage = summary.ReadinessPercentage / 100.0;

            if (readinessPercentage >= HIGH_ACCURACY_THRESHOLD)
                return "High Accuracy (95%+) - Excellent data quality enables reliable analysis results";
            else if (readinessPercentage >= GOOD_ACCURACY_THRESHOLD)
                return "Good Accuracy (80-95%) - Good data quality with minor extrapolation needed";
            else if (readinessPercentage >= MODERATE_ACCURACY_THRESHOLD)
                return "Moderate Accuracy (60-80%) - Significant extrapolation may affect result precision";
            else if (readinessPercentage >= MINIMUM_ANALYSIS_THRESHOLD)
                return "Low Accuracy (50-60%) - Results may be unreliable due to incomplete data";
            else
                return "Insufficient Data (<50%) - Analysis cannot produce reliable results";
        }

        /// <summary>
        /// Categorize validation issues into blocking and warning categories
        /// </summary>
        private void CategorizeIssues(ModelValidationSummary summary, PreAnalysisValidationResult result)
        {
            foreach (var category in summary.ValidationDetails)
            {
                if (category.Status == ValidationStatus.Fail)
                {
                    // Check if this is a blocking failure
                    if (IsBlockingCategory(category.CategoryName))
                    {
                        result.BlockingIssues.AddRange(category.Issues.Take(3)); // Limit to top 3 issues per category
                    }
                    else
                    {
                        result.WarningIssues.AddRange(category.Issues.Take(2));
                    }
                }
                else if (category.Status == ValidationStatus.Warning)
                {
                    result.WarningIssues.AddRange(category.Issues.Take(2));
                }
            }

            // Limit total issues for readability
            result.BlockingIssues = result.BlockingIssues.Take(5).ToList();
            result.WarningIssues = result.WarningIssues.Take(8).ToList();
        }

        /// <summary>
        /// Generate automatic fix options for common issues
        /// </summary>
        private List<AutoFixOption> GenerateAutoFixOptions(ModelValidationSummary summary)
        {
            var options = new List<AutoFixOption>();

            // Check for missing parameters that can be enriched
            var parameterCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Family Parameters");
            if (parameterCategory?.WarningItems > 0)
            {
                options.Add(new AutoFixOption
                {
                    Name = "Enrich Missing Parameters",
                    Description = "Automatically add typical electrical parameters to devices missing them",
                    AffectedDevices = parameterCategory.WarningItems,
                    ImpactDescription = "Uses device catalog and typical values to fill missing parameters",
                    IsRecommended = true,
                    AutoFixAction = () => EnrichMissingParameters()
                });
            }

            // Check for level assignment issues
            var levelCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Level Organization");
            if (levelCategory?.ErrorItems > 0 && levelCategory.ErrorItems < 10) // Only for small numbers
            {
                options.Add(new AutoFixOption
                {
                    Name = "Auto-Assign Levels",
                    Description = "Assign devices to levels based on their Z-coordinate",
                    AffectedDevices = levelCategory.ErrorItems,
                    ImpactDescription = "Automatically assigns nearest level based on device elevation",
                    IsRecommended = levelCategory.ErrorItems < 5,
                    AutoFixAction = () => AutoAssignLevels()
                });
            }

            // Check for parameter value fixes
            var valuesCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Parameter Values");
            if (valuesCategory?.ErrorItems > 0)
            {
                options.Add(new AutoFixOption
                {
                    Name = "Fix Invalid Parameter Values",
                    Description = "Replace zero/negative values with typical device values",
                    AffectedDevices = valuesCategory.ErrorItems,
                    ImpactDescription = "Replaces clearly invalid values with reasonable defaults",
                    IsRecommended = valuesCategory.ErrorItems < summary.TotalDevicesFound * 0.1,
                    AutoFixAction = () => FixInvalidParameterValues()
                });
            }

            return options.Where(o => o.AffectedDevices > 0).ToList();
        }

        /// <summary>
        /// Generate recommended action based on gate decision
        /// </summary>
        private string GenerateRecommendedAction(AnalysisGateDecision decision, ModelValidationSummary summary)
        {
            switch (decision)
            {
                case AnalysisGateDecision.Proceed:
                    return $"Model is ready for analysis. Proceed with confidence - analysis will use {summary.ValidDevicesCount} devices with {summary.AnalysisAccuracy.ToLower()}.";

                case AnalysisGateDecision.ProceedWithWarnings:
                    return $"Analysis can proceed with {summary.ReadinessPercentage:F1}% data completeness. Results will have {summary.AnalysisAccuracy.ToLower()}. Consider fixing warnings for better results.";

                case AnalysisGateDecision.Block:
                    return "Model requires improvements before analysis can proceed. Resolve blocking issues first, then re-run validation.";

                case AnalysisGateDecision.GuidedFix:
                    return "Model can be quickly improved for analysis. Use the suggested auto-fix options to resolve common issues automatically.";

                default:
                    return "Unknown analysis readiness status.";
            }
        }

        /// <summary>
        /// Check if there are blocking issues that prevent analysis
        /// </summary>
        private bool HasBlockingIssues(ModelValidationSummary summary)
        {
            // No devices found
            if (summary.TotalDevicesFound == 0)
                return true;

            // No valid devices
            if (summary.ValidDevicesCount == 0)
                return true;

            // Critical category failures
            var criticalCategories = new[] { "Analysis Readiness", "Fire Alarm Families" };
            return summary.ValidationDetails
                .Where(vd => criticalCategories.Contains(vd.CategoryName))
                .Any(vd => vd.Status == ValidationStatus.Fail);
        }

        /// <summary>
        /// Check if a validation category represents blocking issues
        /// </summary>
        private bool IsBlockingCategory(string categoryName)
        {
            var blockingCategories = new[] 
            { 
                "Analysis Readiness", 
                "Fire Alarm Families", 
                "Level Organization" 
            };
            return blockingCategories.Contains(categoryName);
        }

        /// <summary>
        /// Count issues that can be automatically fixed
        /// </summary>
        private int CountFixableIssues(ModelValidationSummary summary)
        {
            int fixableCount = 0;

            // Missing parameters are fixable
            var parameterCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Family Parameters");
            if (parameterCategory != null)
                fixableCount += parameterCategory.WarningItems;

            // Some level assignment issues are fixable
            var levelCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Level Organization");
            if (levelCategory != null && levelCategory.ErrorItems < 20) // Only small numbers are practically fixable
                fixableCount += Math.Min(levelCategory.ErrorItems, 10);

            // Invalid parameter values are fixable
            var valuesCategory = summary.ValidationDetails.FirstOrDefault(vd => vd.CategoryName == "Parameter Values");
            if (valuesCategory != null)
                fixableCount += valuesCategory.ErrorItems;

            return fixableCount;
        }

        #endregion

        #region Auto-Fix Implementation Placeholders

        /// <summary>
        /// Enrich missing parameters with typical values (placeholder)
        /// </summary>
        private void EnrichMissingParameters()
        {
            // Would implement parameter enrichment using:
            // - Device catalog lookup
            // - Typical values based on device classification
            // - Similar device parameter copying
            System.Diagnostics.Debug.WriteLine("Auto-fix: Enriching missing parameters");
        }

        /// <summary>
        /// Auto-assign devices to levels based on elevation (placeholder)
        /// </summary>
        private void AutoAssignLevels()
        {
            // Would implement level assignment using:
            // - Device Z-coordinate analysis
            // - Nearest level matching
            // - Level boundary detection
            System.Diagnostics.Debug.WriteLine("Auto-fix: Auto-assigning levels");
        }

        /// <summary>
        /// Fix invalid parameter values (placeholder)
        /// </summary>
        private void FixInvalidParameterValues()
        {
            // Would implement value fixing using:
            // - Zero/negative value replacement
            // - Out-of-range value correction
            // - Typical value assignment
            System.Diagnostics.Debug.WriteLine("Auto-fix: Fixing invalid parameter values");
        }

        #endregion
    }
}