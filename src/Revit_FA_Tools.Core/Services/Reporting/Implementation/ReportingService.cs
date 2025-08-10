using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class ReportingService
    {
        private readonly Dictionary<string, ReportTemplate> _templates;
        
        public ReportingService()
        {
            _templates = InitializeReportTemplates();
        }
        
        private Dictionary<string, ReportTemplate> InitializeReportTemplates()
        {
            return new Dictionary<string, ReportTemplate>
            {
                ["COMPREHENSIVE"] = new ReportTemplate
                {
                    Id = "COMPREHENSIVE",
                    Name = "Comprehensive Fire Alarm Analysis",
                    Sections = new List<string> 
                    { 
                        "EXECUTIVE_SUMMARY", 
                        "SYSTEM_OVERVIEW", 
                        "CIRCUIT_ANALYSIS", 
                        "POWER_SUPPLY_ANALYSIS", 
                        "CABLE_ANALYSIS", 
                        "COMPLIANCE_SUMMARY", 
                        "RECOMMENDATIONS",
                        "TECHNICAL_APPENDIX"
                    }
                },
                ["TECHNICAL"] = new ReportTemplate
                {
                    Id = "TECHNICAL",
                    Name = "Technical Design Report",
                    Sections = new List<string>
                    {
                        "SYSTEM_OVERVIEW",
                        "CIRCUIT_ANALYSIS",
                        "POWER_SUPPLY_ANALYSIS", 
                        "CABLE_ANALYSIS",
                        "TECHNICAL_APPENDIX"
                    }
                },
                ["SUMMARY"] = new ReportTemplate
                {
                    Id = "SUMMARY",
                    Name = "Executive Summary",
                    Sections = new List<string>
                    {
                        "EXECUTIVE_SUMMARY",
                        "SYSTEM_OVERVIEW",
                        "COMPLIANCE_SUMMARY",
                        "RECOMMENDATIONS"
                    }
                }
            };
        }
        
        public async Task<ReportGenerationResult> GenerateReport(
            ReportRequest request,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var result = new ReportGenerationResult();
            var startTime = DateTime.Now;
            
            try
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Generating Report",
                    Current = 0,
                    Total = 100,
                    Message = "Preparing report data..."
                });
                
                var template = _templates.GetValueOrDefault(request.TemplateId, _templates["COMPREHENSIVE"]);
                var reportData = PrepareReportData(request);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Generating Report",
                    Current = 20,
                    Total = 100,
                    Message = "Building report sections..."
                });
                
                var content = await BuildReportContent(template, reportData, request.Format, cancellationToken, progress);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Generating Report",
                    Current = 90,
                    Total = 100,
                    Message = "Finalizing report..."
                });
                
                result.Content = content;
                result.FileName = GenerateFileName(request);
                result.Format = request.Format;
                result.GeneratedAt = DateTime.Now;
                result.Template = template;
                result.Statistics = CalculateReportStatistics(content, reportData);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Generating Report",
                    Current = 100,
                    Total = 100,
                    Message = "Report generation complete",
                    ElapsedTime = DateTime.Now - startTime
                });
                
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }
        
        private ReportData PrepareReportData(ReportRequest request)
        {
            return new ReportData
            {
                ProjectName = request.ProjectName,
                AnalysisDate = DateTime.Now,
                PowerSupplies = request.PowerSupplies ?? new List<PowerSupply>(),
                Branches = request.Branches ?? new List<CircuitBranch>(),
                CableAnalyses = request.CableAnalyses ?? new List<CableAnalysisResult>(),
                ValidationSummary = request.ValidationSummary ?? new ValidationSummary(),
                SystemSummary = request.SystemSummary,
                Recommendations = request.Recommendations ?? new List<string>(),
                CustomSections = request.CustomSections ?? new Dictionary<string, object>()
            };
        }
        
        private async Task<string> BuildReportContent(
            ReportTemplate template,
            ReportData data,
            string format,
            CancellationToken cancellationToken,
            IProgress<AnalysisProgress> progress)
        {
            var content = new StringBuilder();
            var sectionCount = template.Sections.Count;
            var processedSections = 0;
            
            if (format.ToUpper() == "HTML")
            {
                content.AppendLine(GetHtmlHeader(data.ProjectName));
            }
            
            foreach (var sectionId in template.Sections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var sectionContent = await GenerateSection(sectionId, data, format, cancellationToken);
                content.AppendLine(sectionContent);
                
                processedSections++;
                
                if (progress != null)
                {
                    progress.Report(new AnalysisProgress
                    {
                        Operation = "Generating Report",
                        Current = 20 + (processedSections * 60 / sectionCount),
                        Total = 100,
                        Message = $"Generated section: {GetSectionDisplayName(sectionId)}"
                    });
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                content.AppendLine(GetHtmlFooter());
            }
            
            return content.ToString();
        }
        
        private async Task<string> GenerateSection(string sectionId, ReportData data, string format, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                return sectionId switch
                {
                    "EXECUTIVE_SUMMARY" => GenerateExecutiveSummary(data, format),
                    "SYSTEM_OVERVIEW" => GenerateSystemOverview(data, format),
                    "CIRCUIT_ANALYSIS" => GenerateCircuitAnalysis(data, format),
                    "POWER_SUPPLY_ANALYSIS" => GeneratePowerSupplyAnalysis(data, format),
                    "CABLE_ANALYSIS" => GenerateCableAnalysis(data, format),
                    "COMPLIANCE_SUMMARY" => GenerateComplianceSummary(data, format),
                    "RECOMMENDATIONS" => GenerateRecommendations(data, format),
                    "TECHNICAL_APPENDIX" => GenerateTechnicalAppendix(data, format),
                    _ => $"Unknown section: {sectionId}"
                };
            }, cancellationToken);
        }
        
        private string GenerateExecutiveSummary(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Executive Summary</h1>");
                sb.AppendLine("<div class='summary-grid'>");
            }
            else
            {
                sb.AppendLine("=== EXECUTIVE SUMMARY ===");
                sb.AppendLine();
            }
            
            sb.AppendLine($"Project: {data.ProjectName}");
            sb.AppendLine($"Analysis Date: {data.AnalysisDate:MMMM dd, yyyy}");
            sb.AppendLine();
            
            var totalDevices = data.Branches.Sum(b => b.Devices.Count);
            var totalCurrent = data.Branches.Sum(b => b.TotalAlarmCurrent);
            var totalPowerSupplies = data.PowerSupplies.Count;
            var totalBranches = data.Branches.Count;
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine($"<div class='metric'><span class='label'>Total Devices:</span> <span class='value'>{totalDevices:N0}</span></div>");
                sb.AppendLine($"<div class='metric'><span class='label'>Total Current:</span> <span class='value'>{totalCurrent:F2}A</span></div>");
                sb.AppendLine($"<div class='metric'><span class='label'>Power Supplies:</span> <span class='value'>{totalPowerSupplies}</span></div>");
                sb.AppendLine($"<div class='metric'><span class='label'>Circuit Branches:</span> <span class='value'>{totalBranches}</span></div>");
            }
            else
            {
                sb.AppendLine($"Total Devices: {totalDevices:N0}");
                sb.AppendLine($"Total Current: {totalCurrent:F2}A");
                sb.AppendLine($"Power Supplies Required: {totalPowerSupplies}");
                sb.AppendLine($"Circuit Branches: {totalBranches}");
            }
            
            sb.AppendLine();
            sb.AppendLine("Key Findings:");
            
            var findings = GenerateKeyFindings(data);
            foreach (var finding in findings)
            {
                if (format.ToUpper() == "HTML")
                {
                    sb.AppendLine($"<li>{finding}</li>");
                }
                else
                {
                    sb.AppendLine($"• {finding}");
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateSystemOverview(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>System Overview</h1>");
                sb.AppendLine("<table class='data-table'>");
                sb.AppendLine("<tr><th>Level</th><th>Devices</th><th>Current (A)</th><th>Branches</th></tr>");
            }
            else
            {
                sb.AppendLine("=== SYSTEM OVERVIEW ===");
                sb.AppendLine();
                sb.AppendLine("Level                    Devices    Current     Branches");
                sb.AppendLine("".PadRight(50, '-'));
            }
            
            var levelSummary = data.Branches
                .SelectMany(b => b.Devices.Select(d => new { Level = d.Level, Branch = b }))
                .GroupBy(x => x.Level)
                .Select(g => new
                {
                    Level = g.Key ?? "Unknown",
                    Devices = g.Count(),
                    Current = g.Sum(x => x.Branch.TotalAlarmCurrent) / g.Select(x => x.Branch.Id).Distinct().Count(),
                    Branches = g.Select(x => x.Branch.Id).Distinct().Count()
                })
                .OrderBy(x => x.Level);
            
            foreach (var level in levelSummary)
            {
                if (format.ToUpper() == "HTML")
                {
                    sb.AppendLine($"<tr><td>{level.Level}</td><td>{level.Devices}</td><td>{level.Current:F2}</td><td>{level.Branches}</td></tr>");
                }
                else
                {
                    sb.AppendLine($"{level.Level,-20} {level.Devices,8} {level.Current,10:F2} {level.Branches,10}");
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateCircuitAnalysis(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Circuit Analysis</h1>");
            }
            else
            {
                sb.AppendLine("=== CIRCUIT ANALYSIS ===");
                sb.AppendLine();
            }
            
            foreach (var branch in data.Branches.OrderBy(b => b.Name))
            {
                var utilization = Math.Max(
                    branch.TotalAlarmCurrent / 2.4,
                    branch.TotalUnitLoads / 111.0) * 100;
                
                if (format.ToUpper() == "HTML")
                {
                    sb.AppendLine($"<div class='branch-summary'>");
                    sb.AppendLine($"<h3>{branch.Name}</h3>");
                    sb.AppendLine($"<p>Devices: {branch.Devices.Count}, Current: {branch.TotalAlarmCurrent:F2}A, Utilization: {utilization:F1}%</p>");
                    sb.AppendLine($"</div>");
                }
                else
                {
                    sb.AppendLine($"Branch: {branch.Name}");
                    sb.AppendLine($"  Devices: {branch.Devices.Count}");
                    sb.AppendLine($"  Alarm Current: {branch.TotalAlarmCurrent:F2}A");
                    sb.AppendLine($"  Standby Current: {branch.TotalStandbyCurrent:F2}A");
                    sb.AppendLine($"  Unit Loads: {branch.TotalUnitLoads}");
                    sb.AppendLine($"  Utilization: {utilization:F1}%");
                    sb.AppendLine();
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GeneratePowerSupplyAnalysis(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Power Supply Analysis</h1>");
            }
            else
            {
                sb.AppendLine("=== POWER SUPPLY ANALYSIS ===");
                sb.AppendLine();
            }
            
            foreach (var ps in data.PowerSupplies.OrderBy(p => p.Name))
            {
                var utilization = (ps.TotalAlarmLoad / ps.TotalCapacity) * 100;
                
                if (format.ToUpper() == "HTML")
                {
                    sb.AppendLine($"<div class='ps-summary'>");
                    sb.AppendLine($"<h3>{ps.Name}</h3>");
                    sb.AppendLine($"<p>Capacity: {ps.TotalCapacity}A, Load: {ps.TotalAlarmLoad:F2}A, Utilization: {utilization:F1}%</p>");
                    sb.AppendLine($"<p>Branches: {ps.Branches.Count}/{ps.MaxBranches}</p>");
                    sb.AppendLine($"</div>");
                }
                else
                {
                    sb.AppendLine($"Power Supply: {ps.Name}");
                    sb.AppendLine($"  Total Capacity: {ps.TotalCapacity}A");
                    sb.AppendLine($"  Alarm Load: {ps.TotalAlarmLoad:F2}A");
                    sb.AppendLine($"  Standby Load: {ps.TotalStandbyLoad:F2}A");
                    sb.AppendLine($"  Utilization: {utilization:F1}%");
                    sb.AppendLine($"  Branches: {ps.Branches.Count}/{ps.MaxBranches}");
                    sb.AppendLine($"  Spare Capacity: {(ps.TotalCapacity - ps.TotalAlarmLoad):F2}A ({((ps.TotalCapacity - ps.TotalAlarmLoad) / ps.TotalCapacity * 100):F1}%)");
                    sb.AppendLine();
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateCableAnalysis(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Cable Analysis</h1>");
            }
            else
            {
                sb.AppendLine("=== CABLE ANALYSIS ===");
                sb.AppendLine();
            }
            
            if (data.CableAnalyses.Any())
            {
                var totalLength = data.CableAnalyses.Sum(c => c.CableLength);
                var avgVoltageDrop = data.CableAnalyses.Average(c => c.VoltageDropPercent);
                var maxVoltageDrop = data.CableAnalyses.Max(c => c.VoltageDropPercent);
                
                sb.AppendLine($"Total Cable Length: {totalLength:F0} feet");
                sb.AppendLine($"Average Voltage Drop: {avgVoltageDrop:F1}%");
                sb.AppendLine($"Maximum Voltage Drop: {maxVoltageDrop:F1}%");
                sb.AppendLine();
                
                var cableBreakdown = data.CableAnalyses
                    .Where(c => c.RecommendedCableSpec != null)
                    .GroupBy(c => c.RecommendedCableSpec.AWGSize)
                    .OrderBy(g => g.Key);
                
                sb.AppendLine("Cable Requirements:");
                foreach (var group in cableBreakdown)
                {
                    var length = group.Sum(c => c.CableLength);
                    sb.AppendLine($"  {group.Key}AWG: {length:F0} feet ({group.Count()} branches)");
                }
            }
            else
            {
                sb.AppendLine("No cable analysis data available.");
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateComplianceSummary(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Compliance Summary</h1>");
            }
            else
            {
                sb.AppendLine("=== COMPLIANCE SUMMARY ===");
                sb.AppendLine();
            }
            
            var totalErrors = data.ValidationSummary?.TotalErrors ?? 0;
            var totalWarnings = data.ValidationSummary?.TotalWarnings ?? 0;
            
            if (totalErrors == 0 && totalWarnings == 0)
            {
                sb.AppendLine("✓ System is fully compliant with IDNAC requirements");
            }
            else
            {
                if (totalErrors > 0)
                {
                    sb.AppendLine($"❌ {totalErrors} compliance errors found");
                }
                if (totalWarnings > 0)
                {
                    sb.AppendLine($"⚠️ {totalWarnings} warnings identified");
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateRecommendations(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Recommendations</h1>");
                sb.AppendLine("<ul>");
            }
            else
            {
                sb.AppendLine("=== RECOMMENDATIONS ===");
                sb.AppendLine();
            }
            
            foreach (var recommendation in data.Recommendations)
            {
                if (format.ToUpper() == "HTML")
                {
                    sb.AppendLine($"<li>{recommendation}</li>");
                }
                else
                {
                    sb.AppendLine($"• {recommendation}");
                }
            }
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</ul>");
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private string GenerateTechnicalAppendix(ReportData data, string format)
        {
            var sb = new StringBuilder();
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h1>Technical Appendix</h1>");
            }
            else
            {
                sb.AppendLine("=== TECHNICAL APPENDIX ===");
                sb.AppendLine();
            }
            
            sb.AppendLine("Design Parameters:");
            sb.AppendLine("• IDNAC Alarm Current Limit: 3.0A");
            sb.AppendLine("• IDNAC Standby Current Limit: 3.0A");
            sb.AppendLine("• IDNAC Unit Load Limit: 139 UL");
            sb.AppendLine("• Voltage Drop Limit: 10%");
            sb.AppendLine("• Nominal System Voltage: 24V");
            sb.AppendLine("• Default Spare Capacity: 20%");
            
            if (format.ToUpper() == "HTML")
            {
                sb.AppendLine("</div>");
            }
            
            return sb.ToString();
        }
        
        private List<string> GenerateKeyFindings(ReportData data)
        {
            var findings = new List<string>();
            
            var avgUtilization = data.PowerSupplies.Any() 
                ? data.PowerSupplies.Average(ps => (ps.TotalAlarmLoad / ps.TotalCapacity) * 100)
                : 0;
            
            if (avgUtilization > 85)
            {
                findings.Add("High system utilization detected - consider additional capacity");
            }
            else if (avgUtilization < 50)
            {
                findings.Add("System is underutilized - optimization opportunities available");
            }
            else
            {
                findings.Add($"System utilization is within optimal range ({avgUtilization:F1}%)");
            }
            
            if (data.ValidationSummary?.HasErrors == true)
            {
                findings.Add($"{data.ValidationSummary.TotalErrors} compliance issues require attention");
            }
            
            if (data.CableAnalyses?.Any(c => c.VoltageDropPercent > 8) == true)
            {
                findings.Add("Some cable runs have high voltage drop - review cable sizing");
            }
            
            return findings;
        }
        
        private string GenerateFileName(ReportRequest request)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var projectName = SanitizeFileName(request.ProjectName ?? "FireAlarmAnalysis");
            var extension = request.Format.ToLower() switch
            {
                "html" => ".html",
                "csv" => ".csv",
                _ => ".txt"
            };
            
            return $"{projectName}_{request.TemplateId}_{timestamp}{extension}";
        }
        
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        }
        
        private string GetHtmlHeader(string title)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .section {{ margin-bottom: 30px; }}
        h1 {{ color: #2c3e50; border-bottom: 2px solid #3498db; }}
        .data-table {{ width: 100%; border-collapse: collapse; }}
        .data-table th, .data-table td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        .data-table th {{ background-color: #f2f2f2; }}
        .metric {{ margin: 10px 0; }}
        .label {{ font-weight: bold; }}
        .value {{ color: #27ae60; }}
    </style>
</head>
<body>";
        }
        
        private string GetHtmlFooter()
        {
            return @"<footer style='margin-top: 50px; border-top: 1px solid #ddd; padding-top: 20px; color: #666;'>
    <p>Report generated by IDNAC Calculator Engine on " + DateTime.Now.ToString("MMMM dd, yyyy 'at' h:mm tt") + @"</p>
</footer>
</body>
</html>";
        }
        
        private string GetSectionDisplayName(string sectionId)
        {
            return sectionId switch
            {
                "EXECUTIVE_SUMMARY" => "Executive Summary",
                "SYSTEM_OVERVIEW" => "System Overview",
                "CIRCUIT_ANALYSIS" => "Circuit Analysis",
                "POWER_SUPPLY_ANALYSIS" => "Power Supply Analysis",
                "CABLE_ANALYSIS" => "Cable Analysis",
                "COMPLIANCE_SUMMARY" => "Compliance Summary",
                "RECOMMENDATIONS" => "Recommendations",
                "TECHNICAL_APPENDIX" => "Technical Appendix",
                _ => sectionId.Replace("_", " ")
            };
        }
        
        private ReportStatistics CalculateReportStatistics(string content, ReportData data)
        {
            return new ReportStatistics
            {
                ContentLength = content.Length,
                WordCount = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length,
                SectionCount = data.CustomSections?.Count ?? 0,
                TableCount = content.Split(new[] {"<table"}, StringSplitOptions.None).Length - 1,
                GenerationTime = DateTime.Now
            };
        }
    }
    
    public class ReportTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Sections { get; set; } = new List<string>();
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }
    
    public class ReportRequest
    {
        public string TemplateId { get; set; } = "COMPREHENSIVE";
        public string ProjectName { get; set; }
        public string Format { get; set; } = "HTML";
        
        public List<PowerSupply> PowerSupplies { get; set; }
        public List<CircuitBranch> Branches { get; set; }
        public List<CableAnalysisResult> CableAnalyses { get; set; }
        public ValidationSummary ValidationSummary { get; set; }
        public object SystemSummary { get; set; }
        public List<string> Recommendations { get; set; }
        public Dictionary<string, object> CustomSections { get; set; }
    }
    
    public class ReportData
    {
        public string ProjectName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<PowerSupply> PowerSupplies { get; set; } = new List<PowerSupply>();
        public List<CircuitBranch> Branches { get; set; } = new List<CircuitBranch>();
        public List<CableAnalysisResult> CableAnalyses { get; set; } = new List<CableAnalysisResult>();
        public ValidationSummary ValidationSummary { get; set; }
        public object SystemSummary { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public Dictionary<string, object> CustomSections { get; set; } = new Dictionary<string, object>();
    }
    
    public class ReportGenerationResult
    {
        public string Content { get; set; }
        public string FileName { get; set; }
        public string Format { get; set; }
        public DateTime GeneratedAt { get; set; }
        public ReportTemplate Template { get; set; }
        public ReportStatistics Statistics { get; set; }
        public string Error { get; set; }
    }
    
    public class ReportStatistics
    {
        public int ContentLength { get; set; }
        public int WordCount { get; set; }
        public int SectionCount { get; set; }
        public int TableCount { get; set; }
        public DateTime GenerationTime { get; set; }
    }
}