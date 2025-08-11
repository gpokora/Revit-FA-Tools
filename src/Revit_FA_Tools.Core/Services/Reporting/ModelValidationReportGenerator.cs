using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Comprehensive reporting for model validation results
    /// </summary>
    public class ModelValidationReportGenerator
    {
        /// <summary>
        /// Generate executive summary report for high-level status
        /// </summary>
        /// <param name="summary">Model validation summary</param>
        /// <returns>Executive summary report text</returns>
        public string GenerateExecutiveSummary(ModelValidationSummary summary)
        {
            var report = new StringBuilder();
            
            report.AppendLine("FIRE ALARM MODEL VALIDATION - EXECUTIVE SUMMARY");
            report.AppendLine("=" + new string('=', 50));
            report.AppendLine();
            
            // Overall Status
            var statusIcon = summary.OverallStatus switch
            {
                ValidationStatus.Pass => "✓",
                ValidationStatus.Warning => "⚠",
                ValidationStatus.Fail => "✗",
                _ => "?"
            };
            
            report.AppendLine($"OVERALL STATUS: {statusIcon} {summary.OverallStatus.ToString().ToUpper()}");
            report.AppendLine($"Model Readiness: {summary.ReadinessPercentage:F1}% ({summary.ValidDevicesCount} of {summary.TotalDevicesFound} devices)");
            report.AppendLine($"Analysis Accuracy: {summary.AnalysisAccuracy}");
            report.AppendLine();

            // Critical Issues
            var criticalIssues = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Fail);
            var warnings = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Warning);
            
            if (criticalIssues > 0)
            {
                report.AppendLine("CRITICAL ISSUES REQUIRING IMMEDIATE ATTENTION:");
                foreach (var category in summary.ValidationDetails.Where((Func<ValidationCategoryResult, bool>)(vd => vd.Status == ValidationStatus.Fail)))
                {
                    report.AppendLine($"• {category.CategoryName}: {category.ErrorItems} critical issues");
                }
                report.AppendLine();
            }

            if (warnings > 0)
            {
                report.AppendLine($"WARNINGS: {warnings} categories have minor issues that may affect accuracy");
                report.AppendLine();
            }

            // Analysis Readiness
            report.AppendLine("ANALYSIS CAPABILITIES:");
            if (summary.AnalysisReadiness)
            {
                report.AppendLine("✓ Model is ready for IDNAC analysis");
                report.AppendLine($"✓ Expected result accuracy: {summary.AnalysisAccuracy}");
            }
            else
            {
                report.AppendLine("✗ Model requires improvements before analysis");
                report.AppendLine("  Issues must be resolved for reliable results");
            }
            report.AppendLine();

            // Next Steps
            report.AppendLine("RECOMMENDED NEXT STEPS:");
            if (summary.RequiredActions.Any())
            {
                foreach (var action in summary.RequiredActions.Take(5))
                {
                    report.AppendLine($"1. {action}");
                }
                if (summary.RequiredActions.Count > 5)
                    report.AppendLine($"   ... and {summary.RequiredActions.Count - 5} additional actions");
            }
            else
            {
                report.AppendLine("No immediate actions required - proceed with analysis");
            }

            report.AppendLine();
            report.AppendLine($"Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            return report.ToString();
        }

        /// <summary>
        /// Generate detailed validation report with complete analysis
        /// </summary>
        /// <param name="summary">Model validation summary</param>
        /// <returns>Detailed report text</returns>
        public string GenerateDetailedReport(ModelValidationSummary summary)
        {
            var report = new StringBuilder();
            
            report.AppendLine("FIRE ALARM MODEL VALIDATION - DETAILED REPORT");
            report.AppendLine("=" + new string('=', 60));
            report.AppendLine();

            // Model Overview
            report.AppendLine("1. MODEL OVERVIEW");
            report.AppendLine("   " + new string('-', 20));
            report.AppendLine($"   Total Fire Alarm Devices: {summary.TotalDevicesFound}");
            report.AppendLine($"   Devices Ready for Analysis: {summary.ValidDevicesCount}");
            report.AppendLine($"   Devices Missing Parameters: {summary.MissingParametersCount}");
            report.AppendLine($"   Overall Readiness: {summary.ReadinessPercentage:F1}%");
            report.AppendLine();

            // Category Analysis
            report.AppendLine("2. VALIDATION CATEGORY ANALYSIS");
            report.AppendLine("   " + new string('-', 30));
            
            foreach (var category in summary.ValidationDetails.OrderBy(c => c.Status))
            {
                var statusIcon = category.Status switch
                {
                    ValidationStatus.Pass => "✓",
                    ValidationStatus.Warning => "⚠",
                    ValidationStatus.Fail => "✗",
                    _ => "?"
                };

                report.AppendLine($"   {statusIcon} {category.CategoryName}");
                report.AppendLine($"      Status: {category.Status}");
                
                if (category.TotalItems > 0)
                {
                    report.AppendLine($"      Items: {category.ValidItems} valid, {category.WarningItems} warnings, {category.ErrorItems} errors of {category.TotalItems} total");
                }

                if (category.Issues.Any())
                {
                    report.AppendLine("      Issues:");
                    foreach (var issue in category.Issues.Take(3))
                    {
                        report.AppendLine($"        • {issue}");
                    }
                    if (category.Issues.Count > 3)
                        report.AppendLine($"        ... and {category.Issues.Count - 3} more issues");
                }

                if (category.Recommendations.Any())
                {
                    report.AppendLine("      Recommendations:");
                    foreach (var recommendation in category.Recommendations.Take(2))
                    {
                        report.AppendLine($"        → {recommendation}");
                    }
                }
                report.AppendLine();
            }

            // Quality Metrics
            report.AppendLine("3. DATA QUALITY METRICS");
            report.AppendLine("   " + new string('-', 25));
            
            var passCount = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Pass);
            var warningCount = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Warning);
            var failCount = summary.ValidationDetails.Count(vd => vd.Status == ValidationStatus.Fail);
            var totalCategories = summary.ValidationDetails.Count;

            if (totalCategories > 0)
            {
                report.AppendLine($"   Validation Categories: {totalCategories} total");
                report.AppendLine($"   ✓ Passed: {passCount} ({(double)passCount / totalCategories * 100:F1}%)");
                report.AppendLine($"   ⚠ Warnings: {warningCount} ({(double)warningCount / totalCategories * 100:F1}%)");
                report.AppendLine($"   ✗ Failed: {failCount} ({(double)failCount / totalCategories * 100:F1}%)");
            }
            report.AppendLine();

            // Analysis Limitations
            report.AppendLine("4. ANALYSIS READINESS ASSESSMENT");
            report.AppendLine("   " + new string('-', 35));
            report.AppendLine($"   Current Status: {(summary.AnalysisReadiness ? "READY" : "NOT READY")}");
            report.AppendLine($"   Expected Accuracy: {summary.AnalysisAccuracy}");
            
            if (!summary.AnalysisReadiness)
            {
                report.AppendLine("   Blocking Issues:");
                var blockingCategories = summary.ValidationDetails.Where((Func<ValidationCategoryResult, bool>)(vd => vd.Status == ValidationStatus.Fail));
                foreach (var category in blockingCategories)
                {
                    report.AppendLine($"     • {category.CategoryName}: {category.ErrorItems} critical errors");
                }
            }
            else
            {
                report.AppendLine("   ✓ All requirements met for analysis");
                if (summary.ReadinessPercentage < 95)
                {
                    report.AppendLine($"   Note: {100 - summary.ReadinessPercentage:F1}% of data may be estimated");
                }
            }
            report.AppendLine();

            // Action Plan
            report.AppendLine("5. PRIORITIZED ACTION PLAN");
            report.AppendLine("   " + new string('-', 28));
            
            if (summary.RequiredActions.Any())
            {
                for (int i = 0; i < Math.Min(summary.RequiredActions.Count, 10); i++)
                {
                    report.AppendLine($"   {i + 1}. {summary.RequiredActions[i]}");
                }
                
                if (summary.RequiredActions.Count > 10)
                    report.AppendLine($"   ... and {summary.RequiredActions.Count - 10} additional actions");
            }
            else
            {
                report.AppendLine("   No actions required - model is ready for analysis");
            }

            report.AppendLine();
            report.AppendLine($"Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("For technical support, consult the model validation documentation.");
            
            return report.ToString();
        }

        /// <summary>
        /// Generate device-specific report with individual device issues
        /// </summary>
        /// <param name="devices">List of device validation results</param>
        /// <returns>Device report text</returns>
        public string GenerateDeviceReport(List<DeviceValidationResult> devices)
        {
            var report = new StringBuilder();
            
            report.AppendLine("FIRE ALARM DEVICE VALIDATION REPORT");
            report.AppendLine("=" + new string('=', 40));
            report.AppendLine();

            if (!devices.Any())
            {
                report.AppendLine("No device validation data available.");
                return report.ToString();
            }

            // Summary by device type
            var devicesByType = devices.GroupBy(d => d.DeviceType).ToDictionary(g => g.Key, g => g.ToList());
            
            report.AppendLine("DEVICE SUMMARY BY TYPE:");
            foreach (var deviceType in devicesByType.OrderBy(kvp => kvp.Key))
            {
                var validCount = deviceType.Value.Count(d => d.ValidationStatus == ValidationStatus.Pass);
                var warningCount = deviceType.Value.Count(d => d.ValidationStatus == ValidationStatus.Warning);
                var errorCount = deviceType.Value.Count(d => d.ValidationStatus == ValidationStatus.Fail);
                
                report.AppendLine($"  {deviceType.Key}: {deviceType.Value.Count} devices ({validCount} valid, {warningCount} warnings, {errorCount} errors)");
            }
            report.AppendLine();

            // Devices with issues
            var problemDevices = devices.Where((Func<DeviceValidationResult, bool>)(d => d.ValidationStatus != ValidationStatus.Pass)).ToList();
            
            if (problemDevices.Any())
            {
                report.AppendLine("DEVICES REQUIRING ATTENTION:");
                report.AppendLine();

                foreach (var device in problemDevices.Take(20)) // Limit to first 20 for readability
                {
                    var statusIcon = device.ValidationStatus switch
                    {
                        ValidationStatus.Warning => "⚠",
                        ValidationStatus.Fail => "✗",
                        _ => "?"
                    };

                    report.AppendLine($"{statusIcon} {device.FamilyName} - {device.TypeName}");
                    report.AppendLine($"    Element ID: {device.ElementId}");
                    report.AppendLine($"    Level: {device.Level}");
                    report.AppendLine($"    Device Type: {device.DeviceType} (Confidence: {device.ClassificationConfidence})");

                    if (device.MissingParameters.Any())
                    {
                        report.AppendLine($"    Missing Parameters: {string.Join(", ", device.MissingParameters)}");
                    }

                    if (device.ParameterIssues.Any())
                    {
                        report.AppendLine("    Parameter Issues:");
                        foreach (var issue in device.ParameterIssues.Take(3))
                        {
                            report.AppendLine($"      • {issue.ParameterName}: {issue.IssueDescription}");
                        }
                    }

                    if (device.Recommendations.Any())
                    {
                        report.AppendLine("    Recommended Actions:");
                        foreach (var recommendation in device.Recommendations.Take(2))
                        {
                            report.AppendLine($"      → {recommendation}");
                        }
                    }
                    report.AppendLine();
                }

                if (problemDevices.Count > 20)
                {
                    report.AppendLine($"... and {problemDevices.Count - 20} additional devices with issues");
                }
            }
            else
            {
                report.AppendLine("✓ All devices passed validation - no issues found!");
            }

            report.AppendLine();
            report.AppendLine($"Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            return report.ToString();
        }

        /// <summary>
        /// Generate action plan with prioritized improvement steps
        /// </summary>
        /// <param name="summary">Model validation summary</param>
        /// <returns>Action plan text</returns>
        public string GenerateActionPlan(ModelValidationSummary summary)
        {
            var report = new StringBuilder();
            
            report.AppendLine("MODEL IMPROVEMENT ACTION PLAN");
            report.AppendLine("=" + new string('=', 35));
            report.AppendLine();

            if (summary.AnalysisReadiness)
            {
                report.AppendLine("✓ MODEL IS READY FOR ANALYSIS");
                report.AppendLine();
                report.AppendLine("Current Status: All critical requirements met");
                report.AppendLine($"Analysis Accuracy: {summary.AnalysisAccuracy}");
                
                if (summary.RequiredActions.Any())
                {
                    report.AppendLine();
                    report.AppendLine("OPTIONAL IMPROVEMENTS:");
                    foreach (var action in summary.RequiredActions.Take(5))
                    {
                        report.AppendLine($"  • {action}");
                    }
                }
                
                return report.ToString();
            }

            // Prioritized action items
            var criticalActions = new List<string>();
            var highActions = new List<string>();
            var mediumActions = new List<string>();

            foreach (var category in summary.ValidationDetails)
            {
                switch (category.Status)
                {
                    case ValidationStatus.Fail:
                        criticalActions.AddRange(category.Recommendations);
                        break;
                    case ValidationStatus.Warning when category.ErrorItems > 0:
                        highActions.AddRange(category.Recommendations);
                        break;
                    case ValidationStatus.Warning:
                        mediumActions.AddRange(category.Recommendations);
                        break;
                }
            }

            // Critical Actions
            if (criticalActions.Any())
            {
                report.AppendLine("CRITICAL ACTIONS (Required before analysis):");
                for (int i = 0; i < Math.Min(criticalActions.Count, 8); i++)
                {
                    report.AppendLine($"  {i + 1}. {criticalActions[i]}");
                }
                if (criticalActions.Count > 8)
                    report.AppendLine($"     ... and {criticalActions.Count - 8} more critical actions");
                report.AppendLine();
            }

            // High Priority Actions
            if (highActions.Any())
            {
                report.AppendLine("HIGH PRIORITY (Significantly improves accuracy):");
                for (int i = 0; i < Math.Min(highActions.Count, 5); i++)
                {
                    report.AppendLine($"  {i + 1}. {highActions[i]}");
                }
                report.AppendLine();
            }

            // Medium Priority Actions
            if (mediumActions.Any())
            {
                report.AppendLine("MEDIUM PRIORITY (Quality improvements):");
                for (int i = 0; i < Math.Min(mediumActions.Count, 3); i++)
                {
                    report.AppendLine($"  {i + 1}. {mediumActions[i]}");
                }
                report.AppendLine();
            }

            // Estimated Timeline
            var totalActions = criticalActions.Count + highActions.Count + mediumActions.Count;
            var estimatedMinutes = criticalActions.Count * 10 + highActions.Count * 5 + mediumActions.Count * 2;

            report.AppendLine("ESTIMATED TIMELINE:");
            report.AppendLine($"  Total Actions: {totalActions}");
            report.AppendLine($"  Estimated Time: {estimatedMinutes} minutes ({estimatedMinutes / 60:F1} hours)");
            report.AppendLine($"  Critical Actions: ~{criticalActions.Count * 10} minutes");
            
            report.AppendLine();
            report.AppendLine("PRIORITY SEQUENCE:");
            report.AppendLine("  1. Complete all Critical Actions first");
            report.AppendLine("  2. Address High Priority items for better accuracy");
            report.AppendLine("  3. Tackle Medium Priority for optimal results");
            report.AppendLine("  4. Re-run validation to confirm improvements");

            report.AppendLine();
            report.AppendLine($"Action plan generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            return report.ToString();
        }
    }
}