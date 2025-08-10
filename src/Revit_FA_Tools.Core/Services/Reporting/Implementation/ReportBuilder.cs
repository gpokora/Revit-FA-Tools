using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Comprehensive report builder for fire alarm system analysis
    /// Generates professional reports in multiple formats (HTML, PDF, CSV, Excel)
    /// </summary>
    public class ReportBuilder
    {
        private readonly VoltageDropCalculator _voltageDropCalculator;
        private readonly BatteryCalculator _batteryCalculator;

        public ReportBuilder()
        {
            _voltageDropCalculator = new VoltageDropCalculator();
            _batteryCalculator = new BatteryCalculator();
        }

        /// <summary>
        /// Generate comprehensive HTML report
        /// </summary>
        public void GenerateHTMLReport(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                var html = BuildHTMLReport(analysisResults);
                File.WriteAllText(filePath, html, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating HTML report: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate executive summary report
        /// </summary>
        public void GenerateExecutiveSummary(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                WriteExecutiveSummaryHeader(writer, analysisResults);
                WriteSystemOverview(writer, analysisResults);
                WriteKeyFindings(writer, analysisResults);
                WriteRecommendations(writer, analysisResults);
                WriteNextSteps(writer);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating executive summary: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate detailed technical report
        /// </summary>
        public void GenerateTechnicalReport(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                WriteTechnicalReportHeader(writer, analysisResults);
                WriteSystemConfiguration(writer);
                WriteDetailedAnalysis(writer, analysisResults);
                WriteCircuitAnalysis(writer, analysisResults);
                WritePowerSupplyAnalysis(writer, analysisResults);
                WriteBatteryAnalysis(writer, analysisResults);
                WriteComplianceAnalysis(writer, analysisResults);
                WriteTechnicalAppendix(writer);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating technical report: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate device schedule report
        /// </summary>
        public void GenerateDeviceSchedule(List<DeviceSnapshot> devices, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                writer.WriteLine("FIRE ALARM DEVICE SCHEDULE");
                writer.WriteLine("==========================");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Total Devices: {devices.Count}");
                writer.WriteLine();

                // Group by level for organized output
                var levelGroups = devices.GroupBy(d => d.LevelName).OrderBy(g => g.Key);

                foreach (var levelGroup in levelGroups)
                {
                    writer.WriteLine($"LEVEL: {levelGroup.Key}");
                    writer.WriteLine(new string('-', 50));

                    var deviceGroups = levelGroup
                        .GroupBy(d => new { d.FamilyName, d.TypeName })
                        .OrderBy(g => g.Key.FamilyName)
                        .ThenBy(g => g.Key.TypeName);

                    foreach (var deviceGroup in deviceGroups)
                    {
                        var sample = deviceGroup.First();
                        writer.WriteLine($"  {sample.FamilyName} - {sample.TypeName}");
                        writer.WriteLine($"    Quantity: {deviceGroup.Count()}");
                        writer.WriteLine($"    Power: {sample.Watts:F1}W, {sample.Amps:F3}A each");
                        writer.WriteLine($"    Unit Loads: {sample.UnitLoads} UL each");
                        writer.WriteLine($"    Features: {GetDeviceFeatures(sample)}");
                        writer.WriteLine($"    Total Power: {deviceGroup.Sum(d => d.Watts):F1}W, {deviceGroup.Sum(d => d.Amps):F3}A");
                        writer.WriteLine();
                    }
                }

                WriteSummaryTotals(writer, devices);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating device schedule: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate load calculation worksheets
        /// </summary>
        public void GenerateLoadCalculationWorksheets(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                WriteLoadCalculationHeader(writer);
                WriteIDNACLoadCalculations(writer, analysisResults);
                WriteIDNETLoadCalculations(writer, analysisResults);
                WritePowerSupplyCalculations(writer, analysisResults);
                WriteVoltageDropCalculations(writer, analysisResults);
                WriteLoadCalculationSummary(writer, analysisResults);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating load calculation worksheets: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Build comprehensive HTML report
        /// </summary>
        private string BuildHTMLReport(ComprehensiveAnalysisResults analysisResults)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='UTF-8'>");
            html.AppendLine("    <title>Fire Alarm System Analysis Report</title>");
            html.AppendLine("    <style>");
            html.AppendLine(GetHTMLStyles());
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Header
            html.AppendLine("    <div class='header'>");
            html.AppendLine("        <h1>Fire Alarm System Analysis Report</h1>");
            html.AppendLine($"        <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"        <p>Analysis Scope: {analysisResults.Scope}</p>");
            html.AppendLine("    </div>");

            // Executive Summary
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h2>Executive Summary</h2>");
            html.AppendLine(BuildExecutiveSummaryHTML(analysisResults));
            html.AppendLine("    </div>");

            // System Overview
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h2>System Overview</h2>");
            html.AppendLine(BuildSystemOverviewHTML(analysisResults));
            html.AppendLine("    </div>");

            // IDNAC Analysis
            if (analysisResults.IDNACResults != null)
            {
                html.AppendLine("    <div class='section'>");
                html.AppendLine("        <h2>IDNAC (Notification) System Analysis</h2>");
                html.AppendLine(BuildIDNACAnalysisHTML(analysisResults.IDNACResults));
                html.AppendLine("    </div>");
            }

            // IDNET Analysis
            if (analysisResults.IDNETResults != null)
            {
                html.AppendLine("    <div class='section'>");
                html.AppendLine("        <h2>IDNET (Detection) System Analysis</h2>");
                html.AppendLine(BuildIDNETAnalysisHTML(analysisResults.IDNETResults));
                html.AppendLine("    </div>");
            }

            // Configuration
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h2>System Configuration</h2>");
            html.AppendLine(BuildConfigurationHTML());
            html.AppendLine("    </div>");

            // Footer
            html.AppendLine("    <div class='footer'>");
            html.AppendLine("        <p>Generated by IDNAC Engineering Engine</p>");
            html.AppendLine("        <p>🤖 Generated with <a href='https://claude.ai/code'>Claude Code</a></p>");
            html.AppendLine("    </div>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string GetHTMLStyles()
        {
            return @"
        body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; color: #333; }
        .header { border-bottom: 3px solid #d32f2f; padding-bottom: 20px; margin-bottom: 30px; }
        .section { margin-bottom: 40px; page-break-inside: avoid; }
        .section h2 { color: #d32f2f; border-bottom: 2px solid #f5f5f5; padding-bottom: 10px; }
        .section h3 { color: #666; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        table, th, td { border: 1px solid #ddd; }
        th, td { padding: 12px; text-align: left; }
        th { background-color: #f5f5f5; font-weight: bold; }
        tr:nth-child(even) { background-color: #f9f9f9; }
        .highlight { background-color: #fff3cd; padding: 10px; border-left: 4px solid #ffc107; margin: 10px 0; }
        .warning { background-color: #f8d7da; padding: 10px; border-left: 4px solid #dc3545; margin: 10px 0; }
        .success { background-color: #d4edda; padding: 10px; border-left: 4px solid #28a745; margin: 10px 0; }
        .footer { border-top: 1px solid #ddd; padding-top: 20px; margin-top: 40px; text-align: center; color: #666; }
        .summary-stats { display: flex; justify-content: space-around; flex-wrap: wrap; }
        .stat-box { background: #f8f9fa; padding: 20px; margin: 10px; border-radius: 5px; text-align: center; min-width: 150px; }
        .stat-number { font-size: 2em; font-weight: bold; color: #d32f2f; }
        .stat-label { color: #666; }
        ";
        }

        private string BuildExecutiveSummaryHTML(ComprehensiveAnalysisResults analysisResults)
        {
            var html = new StringBuilder();

            html.AppendLine("        <div class='summary-stats'>");
            
            if (analysisResults.IDNACResults != null)
            {
                html.AppendLine("            <div class='stat-box'>");
                html.AppendLine($"                <div class='stat-number'>{analysisResults.IDNACResults.TotalIdnacsNeeded}</div>");
                html.AppendLine("                <div class='stat-label'>IDNACs Required</div>");
                html.AppendLine("            </div>");
            }

            if (analysisResults.IDNETResults != null)
            {
                html.AppendLine("            <div class='stat-box'>");
                html.AppendLine($"                <div class='stat-number'>{analysisResults.IDNETResults.TotalDevices}</div>");
                html.AppendLine("                <div class='stat-label'>Detection Devices</div>");
                html.AppendLine("            </div>");
            }

            html.AppendLine("            <div class='stat-box'>");
            html.AppendLine($"                <div class='stat-number'>{analysisResults.TotalElementsAnalyzed}</div>");
            html.AppendLine("                <div class='stat-label'>Total Elements</div>");
            html.AppendLine("            </div>");

            html.AppendLine("        </div>");

            return html.ToString();
        }

        private string BuildSystemOverviewHTML(ComprehensiveAnalysisResults analysisResults)
        {
            var config = ConfigurationService.Current;
            var html = new StringBuilder();

            html.AppendLine("        <table>");
            html.AppendLine("            <tr><th>Configuration Parameter</th><th>Value</th></tr>");
            html.AppendLine($"            <tr><td>Spare Capacity</td><td>{config.Spare.SpareFractionDefault*100:F0}%</td></tr>");
            html.AppendLine($"            <tr><td>IDNAC Current Limit</td><td>{config.Capacity.IdnacAlarmCurrentLimitA:F1}A</td></tr>");
            html.AppendLine($"            <tr><td>IDNAC Unit Load Limit</td><td>{config.Capacity.IdnacStandbyUnitLoadLimit} UL</td></tr>");
            html.AppendLine($"            <tr><td>Voltage Drop Limit</td><td>{config.Capacity.MaxVoltageDropPct*100:F0}%</td></tr>");
            html.AppendLine($"            <tr><td>Analysis Time</td><td>{analysisResults.TotalAnalysisTime.TotalSeconds:F1} seconds</td></tr>");
            html.AppendLine("        </table>");

            return html.ToString();
        }

        private string BuildIDNACAnalysisHTML(IDNACSystemResults idnacResults)
        {
            var html = new StringBuilder();

            html.AppendLine("        <h3>Summary</h3>");
            html.AppendLine("        <table>");
            html.AppendLine("            <tr><th>Metric</th><th>Value</th></tr>");
            html.AppendLine($"            <tr><td>Total IDNACs Required</td><td>{idnacResults.TotalIdnacsNeeded}</td></tr>");
            html.AppendLine($"            <tr><td>Total Current</td><td>{idnacResults.TotalCurrent:F2}A</td></tr>");
            html.AppendLine($"            <tr><td>Total Unit Loads</td><td>{idnacResults.TotalUnitLoads}</td></tr>");
            html.AppendLine($"            <tr><td>Total Devices</td><td>{idnacResults.TotalDevices}</td></tr>");
            html.AppendLine("        </table>");

            if (idnacResults.LevelResults?.Any() == true)
            {
                html.AppendLine("        <h3>Level Breakdown</h3>");
                html.AppendLine("        <table>");
                html.AppendLine("            <tr><th>Level</th><th>IDNACs</th><th>Current (A)</th><th>Unit Loads</th><th>Devices</th><th>Limiting Factor</th></tr>");
                
                foreach (var level in idnacResults.LevelResults.OrderBy(lr => lr.Key))
                {
                    html.AppendLine($"            <tr><td>{level.Key}</td><td>{level.Value.IdnacsRequired}</td><td>{level.Value.Current:F2}</td><td>{level.Value.UnitLoads}</td><td>{level.Value.Devices}</td><td>{level.Value.LimitingFactor}</td></tr>");
                }
                
                html.AppendLine("        </table>");
            }

            return html.ToString();
        }

        private string BuildIDNETAnalysisHTML(IDNETSystemResults idnetResults)
        {
            var html = new StringBuilder();

            html.AppendLine("        <table>");
            html.AppendLine("            <tr><th>Metric</th><th>Value</th></tr>");
            html.AppendLine($"            <tr><td>Total Detection Devices</td><td>{idnetResults.TotalDevices}</td></tr>");
            html.AppendLine($"            <tr><td>Channels Required</td><td>{idnetResults.ChannelsRequired}</td></tr>");
            html.AppendLine($"            <tr><td>Max Devices per Channel</td><td>{idnetResults.MaxDevicesPerChannel}</td></tr>");
            html.AppendLine($"            <tr><td>Total Unit Loads</td><td>{idnetResults.TotalUnitLoads}</td></tr>");
            html.AppendLine("        </table>");

            return html.ToString();
        }

        private string BuildConfigurationHTML()
        {
            var config = ConfigurationService.Current;
            var html = new StringBuilder();

            html.AppendLine("        <h3>Spare Capacity Policy</h3>");
            html.AppendLine("        <table>");
            html.AppendLine($"            <tr><td>Default Spare Fraction</td><td>{config.Spare.SpareFractionDefault*100:F0}%</td></tr>");
            html.AppendLine($"            <tr><td>Enforce on Current</td><td>{config.Spare.EnforceOnCurrent}</td></tr>");
            html.AppendLine($"            <tr><td>Enforce on Unit Loads</td><td>{config.Spare.EnforceOnUL}</td></tr>");
            html.AppendLine($"            <tr><td>Enforce on Power</td><td>{config.Spare.EnforceOnPower}</td></tr>");
            html.AppendLine("        </table>");

            html.AppendLine("        <h3>System Limits</h3>");
            html.AppendLine("        <table>");
            html.AppendLine($"            <tr><td>IDNAC Alarm Current Limit</td><td>{config.Capacity.IdnacAlarmCurrentLimitA:F1}A</td></tr>");
            html.AppendLine($"            <tr><td>IDNAC Standby UL Limit</td><td>{config.Capacity.IdnacStandbyUnitLoadLimit} UL</td></tr>");
            html.AppendLine($"            <tr><td>IDNET Channel UL Limit</td><td>{config.Capacity.IdnetChannelUnitLoadLimit} UL</td></tr>");
            html.AppendLine($"            <tr><td>Max Voltage Drop</td><td>{config.Capacity.MaxVoltageDropPct*100:F0}%</td></tr>");
            html.AppendLine("        </table>");

            return html.ToString();
        }

        // Helper methods for text reports
        private void WriteExecutiveSummaryHeader(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("FIRE ALARM SYSTEM ANALYSIS");
            writer.WriteLine("EXECUTIVE SUMMARY");
            writer.WriteLine("=================");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd}");
            writer.WriteLine($"Scope: {analysisResults.Scope}");
            writer.WriteLine($"Elements Analyzed: {analysisResults.TotalElementsAnalyzed}");
            writer.WriteLine();
        }

        private void WriteSystemOverview(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("SYSTEM OVERVIEW");
            writer.WriteLine("---------------");
            
            if (analysisResults.IDNACResults != null)
            {
                writer.WriteLine($"IDNAC System: {analysisResults.IDNACResults.TotalIdnacsNeeded} circuits, {analysisResults.IDNACResults.TotalDevices} devices");
            }
            
            if (analysisResults.IDNETResults != null)
            {
                writer.WriteLine($"IDNET System: {analysisResults.IDNETResults.TotalDevices} devices, {analysisResults.IDNETResults.ChannelsRequired} channels");
            }
            
            writer.WriteLine($"Analysis completed in {analysisResults.TotalAnalysisTime.TotalSeconds:F1} seconds");
            writer.WriteLine();
        }

        private void WriteKeyFindings(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("KEY FINDINGS");
            writer.WriteLine("------------");
            
            var config = ConfigurationService.Current;
            writer.WriteLine($"• System configured with {config.Spare.SpareFractionDefault*100:F0}% spare capacity");
            writer.WriteLine($"• Dual limits enforced: {config.Capacity.IdnacAlarmCurrentLimitA:F1}A current AND {config.Capacity.IdnacStandbyUnitLoadLimit} UL");
            
            if (analysisResults.IDNACResults != null)
            {
                writer.WriteLine($"• {analysisResults.IDNACResults.TotalIdnacsNeeded} IDNAC circuits required for notification system");
            }
            
            writer.WriteLine();
        }

        private void WriteRecommendations(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("RECOMMENDATIONS");
            writer.WriteLine("---------------");
            writer.WriteLine("• Verify all device locations and wire routing");
            writer.WriteLine("• Confirm spare capacity meets local requirements");
            writer.WriteLine("• Plan for future expansion needs");
            writer.WriteLine("• Schedule professional design review");
            writer.WriteLine();
        }

        private void WriteNextSteps(StreamWriter writer)
        {
            writer.WriteLine("NEXT STEPS");
            writer.WriteLine("----------");
            writer.WriteLine("1. Professional fire alarm design review");
            writer.WriteLine("2. Detailed construction documents");
            writer.WriteLine("3. Authority Having Jurisdiction (AHJ) approval");
            writer.WriteLine("4. Installation and commissioning");
            writer.WriteLine();
        }

        private void WriteTechnicalReportHeader(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("FIRE ALARM SYSTEM TECHNICAL ANALYSIS REPORT");
            writer.WriteLine("===========================================");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Analysis Engine Version: 1.0");
            writer.WriteLine($"Scope: {analysisResults.Scope}");
            writer.WriteLine();
        }

        private void WriteSystemConfiguration(StreamWriter writer)
        {
            var config = ConfigurationService.Current;
            
            writer.WriteLine("SYSTEM CONFIGURATION");
            writer.WriteLine("--------------------");
            writer.WriteLine($"IDNAC Current Limit: {config.Capacity.IdnacAlarmCurrentLimitA:F1}A");
            writer.WriteLine($"IDNAC Unit Load Limit: {config.Capacity.IdnacStandbyUnitLoadLimit} UL");
            writer.WriteLine($"Spare Capacity: {config.Spare.SpareFractionDefault*100:F0}%");
            writer.WriteLine($"Voltage Drop Limit: {config.Capacity.MaxVoltageDropPct*100:F0}%");
            writer.WriteLine($"Repeater Fresh Budget: {config.Repeater.TreatRepeaterAsFreshBudget}");
            writer.WriteLine();
        }

        private void WriteDetailedAnalysis(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("DETAILED SYSTEM ANALYSIS");
            writer.WriteLine("------------------------");
            
            if (analysisResults.IDNACResults != null)
            {
                writer.WriteLine("IDNAC (Notification) Analysis:");
                writer.WriteLine($"  Total Circuits: {analysisResults.IDNACResults.TotalIdnacsNeeded}");
                writer.WriteLine($"  Total Current: {analysisResults.IDNACResults.TotalCurrent:F2}A");
                writer.WriteLine($"  Total Unit Loads: {analysisResults.IDNACResults.TotalUnitLoads}");
                writer.WriteLine($"  Total Devices: {analysisResults.IDNACResults.TotalDevices}");
                
                // Add addressing information
                WriteAddressingAnalysis(writer);
                writer.WriteLine();
            }
            
            if (analysisResults.IDNETResults != null)
            {
                writer.WriteLine("IDNET (Detection) Analysis:");
                writer.WriteLine($"  Total Devices: {analysisResults.IDNETResults.TotalDevices}");
                writer.WriteLine($"  Channels Required: {analysisResults.IDNETResults.ChannelsRequired}");
                writer.WriteLine($"  Total Unit Loads: {analysisResults.IDNETResults.TotalUnitLoads}");
                writer.WriteLine();
            }
        }

        private void WriteCircuitAnalysis(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("CIRCUIT ANALYSIS");
            writer.WriteLine("----------------");
            writer.WriteLine("Circuit analysis performed with dual limits enforcement:");
            writer.WriteLine("• Current limit: 3.0A alarm capacity");
            writer.WriteLine("• Unit Load limit: 139 UL standby capacity");
            writer.WriteLine("• Spare capacity applied per configuration");
            writer.WriteLine();
        }

        private void WritePowerSupplyAnalysis(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("POWER SUPPLY ANALYSIS");
            writer.WriteLine("---------------------");
            var config = ConfigurationService.Current.PowerSupply;
            writer.WriteLine($"ES-PS Capacity: {config.TotalDCOutputWithFan:F1}A with IDNAC modules");
            writer.WriteLine($"Circuits per PS: {config.IDNACCircuitsPerPS}");
            
            if (analysisResults.IDNACResults != null)
            {
                var psNeeded = Math.Ceiling((double)analysisResults.IDNACResults.TotalIdnacsNeeded / config.IDNACCircuitsPerPS);
                writer.WriteLine($"Power Supplies Required: {psNeeded}");
            }
            
            writer.WriteLine();
        }

        private void WriteBatteryAnalysis(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("BATTERY ANALYSIS");
            writer.WriteLine("----------------");
            writer.WriteLine("Battery calculations based on NFPA 72 requirements:");
            writer.WriteLine("• 24 hour standby + 5 minute alarm operation");
            writer.WriteLine("• Unit Load methodology: 0.8mA per UL");
            writer.WriteLine("• 80% derating factor applied");
            writer.WriteLine("• Professional battery calculation recommended");
            writer.WriteLine();
        }

        private void WriteComplianceAnalysis(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("COMPLIANCE ANALYSIS");
            writer.WriteLine("-------------------");
            writer.WriteLine("Analysis performed per applicable standards:");
            writer.WriteLine("• NFPA 72: National Fire Alarm and Signaling Code");
            writer.WriteLine("• Manufacturer specifications (Johnson Controls 4100ES)");
            writer.WriteLine("• Local AHJ requirements (to be verified)");
            writer.WriteLine();
        }

        private void WriteTechnicalAppendix(StreamWriter writer)
        {
            writer.WriteLine("TECHNICAL APPENDIX");
            writer.WriteLine("------------------");
            writer.WriteLine("Calculation Methodologies:");
            writer.WriteLine("• Dual limits: MAX(current_requirement, unit_load_requirement)");
            writer.WriteLine("• Spare capacity: applied to both current and UL limits");
            writer.WriteLine("• Repeater islands: fresh 3.0A/139 UL budget when enabled");
            writer.WriteLine("• Circuit balancing: level exclusions applied");
            writer.WriteLine();
            writer.WriteLine("🤖 Generated with Claude Code");
            writer.WriteLine("Co-Authored-By: Claude <noreply@anthropic.com>");
        }

        private void WriteLoadCalculationHeader(StreamWriter writer)
        {
            writer.WriteLine("FIRE ALARM LOAD CALCULATION WORKSHEETS");
            writer.WriteLine("======================================");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd}");
            writer.WriteLine();
        }

        private void WriteIDNACLoadCalculations(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("IDNAC LOAD CALCULATIONS");
            writer.WriteLine("-----------------------");
            
            if (analysisResults.IDNACResults?.LevelResults?.Any() == true)
            {
                writer.WriteLine("Level\t\tCurrent(A)\tUL\tDevices\tIDNACs\tLimiting Factor");
                writer.WriteLine(new string('-', 80));
                
                foreach (var level in analysisResults.IDNACResults.LevelResults)
                {
                    writer.WriteLine($"{level.Key,-15}\t{level.Value.Current:F2}\t{level.Value.UnitLoads}\t{level.Value.Devices}\t{level.Value.IdnacsRequired}\t{level.Value.LimitingFactor}");
                }
            }
            
            writer.WriteLine();
        }

        private void WriteIDNETLoadCalculations(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("IDNET LOAD CALCULATIONS");
            writer.WriteLine("-----------------------");
            
            if (analysisResults.IDNETResults != null)
            {
                writer.WriteLine($"Total Detection Devices: {analysisResults.IDNETResults.TotalDevices}");
                writer.WriteLine($"Channels Required: {analysisResults.IDNETResults.ChannelsRequired}");
                writer.WriteLine($"Total Unit Loads: {analysisResults.IDNETResults.TotalUnitLoads}");
            }
            
            writer.WriteLine();
        }

        private void WritePowerSupplyCalculations(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("POWER SUPPLY CALCULATIONS");
            writer.WriteLine("-------------------------");
            
            var config = ConfigurationService.Current.PowerSupply;
            writer.WriteLine($"ES-PS Capacity: {config.TotalDCOutputWithFan:F1}A");
            
            if (analysisResults.IDNACResults != null)
            {
                var psNeeded = Math.Ceiling((double)analysisResults.IDNACResults.TotalIdnacsNeeded / config.IDNACCircuitsPerPS);
                writer.WriteLine($"IDNAC Circuits: {analysisResults.IDNACResults.TotalIdnacsNeeded}");
                writer.WriteLine($"Power Supplies Required: {psNeeded}");
            }
            
            writer.WriteLine();
        }

        private void WriteVoltageDropCalculations(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("VOLTAGE DROP CALCULATIONS");
            writer.WriteLine("-------------------------");
            writer.WriteLine("Voltage drop analysis requires circuit routing information.");
            writer.WriteLine("Professional design review recommended for accurate calculations.");
            writer.WriteLine();
        }

        private void WriteLoadCalculationSummary(StreamWriter writer, ComprehensiveAnalysisResults analysisResults)
        {
            writer.WriteLine("LOAD CALCULATION SUMMARY");
            writer.WriteLine("------------------------");
            
            if (analysisResults.IDNACResults != null)
            {
                writer.WriteLine($"Total IDNAC Current: {analysisResults.IDNACResults.TotalCurrent:F2}A");
                writer.WriteLine($"Total IDNAC Unit Loads: {analysisResults.IDNACResults.TotalUnitLoads}");
                writer.WriteLine($"IDNACs Required: {analysisResults.IDNACResults.TotalIdnacsNeeded}");
            }
            
            if (analysisResults.IDNETResults != null)
            {
                writer.WriteLine($"Total IDNET Devices: {analysisResults.IDNETResults.TotalDevices}");
                writer.WriteLine($"Total IDNET Unit Loads: {analysisResults.IDNETResults.TotalUnitLoads}");
            }
            
            writer.WriteLine($"Analysis Time: {analysisResults.TotalAnalysisTime.TotalSeconds:F1} seconds");
            writer.WriteLine();
        }

        private string GetDeviceFeatures(DeviceSnapshot device)
        {
            var features = new List<string>();
            
            if (device.HasStrobe) features.Add("Strobe");
            if (device.HasSpeaker) features.Add("Speaker");
            if (device.IsIsolator) features.Add("Isolator");
            if (device.IsRepeater) features.Add("Repeater");
            
            return features.Any() ? string.Join(", ", features) : "Standard";
        }

        private void WriteSummaryTotals(StreamWriter writer, List<DeviceSnapshot> devices)
        {
            writer.WriteLine("SUMMARY TOTALS");
            writer.WriteLine("==============");
            writer.WriteLine($"Total Devices: {devices.Count}");
            writer.WriteLine($"Total Power: {devices.Sum(d => d.Watts):F1}W");
            writer.WriteLine($"Total Current: {devices.Sum(d => d.Amps):F2}A");
            writer.WriteLine($"Total Unit Loads: {devices.Sum(d => d.UnitLoads)}");
            writer.WriteLine();
            
            writer.WriteLine("By Device Type:");
            var typeGroups = devices.GroupBy(d => d.DeviceType);
            foreach (var group in typeGroups.OrderBy(g => g.Key))
            {
                writer.WriteLine($"  {group.Key}: {group.Count()} devices");
            }
        }

        /// <summary>
        /// Write addressing analysis for branches and panels
        /// </summary>
        private void WriteAddressingAnalysis(StreamWriter writer)
        {
            try
            {
                var addressingService = new DeviceAddressingService();
                // TODO: Replace with proper assignment service
                // var assignmentStore = AssignmentStore.Instance;

                writer.WriteLine("  Device Addressing Analysis:");
                writer.WriteLine("    [Addressing analysis currently disabled - service integration pending]");
                
                // Group devices by branch and provide addressing summary
                // var branchGroups = assignmentStore.DeviceAssignments
                //     .Where(a => a.IsAssigned)
                //     .GroupBy(a => a.BranchId)
                //     .OrderBy(g => g.Key);

                // foreach (var branch in branchGroups)
                // {
                //     var branchDevices = branch.ToList();
                //     var validation = addressingService.ValidateAddressing(branchDevices);
                //     var summary = addressingService.GetAddressRangeSummary(branchDevices);
                //     
                //     writer.WriteLine($"    Branch {branch.Key}: {summary}");
                //     
                //     if (!validation.IsValid)
                //     {
                //         writer.WriteLine($"      WARNING: {validation.Conflicts.Count} address conflicts detected");
                //     }
                // }

                // Overall addressing statistics
                // var allDevices = assignmentStore.DeviceAssignments.Where(a => a.IsAssigned).ToList();
                // var totalLocked = allDevices.Count(d => d.LockState == AddressLockState.Locked);
                // var totalAuto = allDevices.Count(d => d.LockState == AddressLockState.Auto);
                // var totalManual = allDevices.Count(d => d.IsManualAddress);

                // writer.WriteLine($"  Total Assigned Devices: {allDevices.Count}");
                // writer.WriteLine($"  Locked Addresses: {totalLocked}");
                // writer.WriteLine($"  Auto Addresses: {totalAuto}");
                // writer.WriteLine($"  Manual Addresses: {totalManual}");
            }
            catch (Exception ex)
            {
                writer.WriteLine($"  Addressing analysis error: {ex.Message}");
            }
        }
    }
}