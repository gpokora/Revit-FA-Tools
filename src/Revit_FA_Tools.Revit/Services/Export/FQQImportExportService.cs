using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Fire alarm Quantity Takeoff (FQQ) import/export service
    /// Supports CSV, Excel, and JSON formats for device schedules and quantity reports
    /// </summary>
    public class FQQImportExportService
    {
        /// <summary>
        /// Export device schedule to CSV format
        /// </summary>
        public void ExportDeviceScheduleToCSV(List<DeviceSnapshot> devices, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                
                // Write header
                writer.WriteLine("ID,Level,Family,Type,Watts,Amps,UnitLoads,HasStrobe,HasSpeaker,IsIsolator,IsRepeater,DeviceType,Zone,Description");

                // Write device data
                foreach (var device in devices.OrderBy(d => d.LevelName).ThenBy(d => d.FamilyName))
                {
                    var line = $"{device.ElementId}," +
                              $"\"{EscapeCsvValue(device.LevelName)}\"," +
                              $"\"{EscapeCsvValue(device.FamilyName)}\"," +
                              $"\"{EscapeCsvValue(device.TypeName)}\"," +
                              $"{device.Watts:F3}," +
                              $"{device.Amps:F4}," +
                              $"{device.UnitLoads}," +
                              $"{device.HasStrobe}," +
                              $"{device.HasSpeaker}," +
                              $"{device.IsIsolator}," +
                              $"{device.IsRepeater}," +
                              $"\"{device.DeviceType}\"," +
                              $"\"{device.Zone}\"," +
                              $"\"{EscapeCsvValue(device.Description)}\"";
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error exporting device schedule to CSV: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Import device data from CSV format
        /// </summary>
        public List<DeviceSnapshot> ImportDeviceScheduleFromCSV(string filePath)
        {
            var devices = new List<DeviceSnapshot>();

            try
            {
                using var reader = new StreamReader(filePath, Encoding.UTF8);
                
                // Skip header line
                var header = reader.ReadLine();
                if (string.IsNullOrEmpty(header))
                {
                    throw new InvalidDataException("CSV file appears to be empty");
                }

                int lineNumber = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var device = ParseCSVLine(line);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Error parsing line {lineNumber}: {ex.Message}");
                    }
                }

                return devices;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error importing device schedule from CSV: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export quantity takeoff summary to CSV
        /// </summary>
        public void ExportQuantityTakeoffToCSV(List<DeviceSnapshot> devices, string filePath)
        {
            try
            {
                var quantities = CalculateQuantities(devices);

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                
                // Write header
                writer.WriteLine("Family,Type,DeviceType,Zone,Quantity,TotalWatts,TotalAmps,TotalUnitLoads,HasStrobe,HasSpeaker");

                // Write quantity data
                foreach (var qty in quantities.OrderBy(q => q.Family).ThenBy(q => q.Type))
                {
                    var line = $"\"{EscapeCsvValue(qty.Family)}\"," +
                              $"\"{EscapeCsvValue(qty.Type)}\"," +
                              $"\"{qty.DeviceType}\"," +
                              $"\"{qty.Zone}\"," +
                              $"{qty.Quantity}," +
                              $"{qty.TotalWatts:F3}," +
                              $"{qty.TotalAmps:F4}," +
                              $"{qty.TotalUnitLoads}," +
                              $"{qty.HasStrobe}," +
                              $"{qty.HasSpeaker}";
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error exporting quantity takeoff to CSV: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export complete system analysis to JSON format
        /// </summary>
        public void ExportSystemAnalysisToJSON(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                var exportData = new
                {
                    ExportDate = DateTime.Now,
                    AnalysisScope = analysisResults.Scope,
                    TotalElementsAnalyzed = analysisResults.TotalElementsAnalyzed,
                    AnalysisTime = analysisResults.TotalAnalysisTime,
                    
                    ElectricalSummary = new
                    {
                        TotalCurrent = analysisResults.ElectricalResults?.TotalCurrent ?? 0,
                        TotalWattage = analysisResults.ElectricalResults?.TotalWattage ?? 0,
                        TotalDevices = analysisResults.ElectricalResults?.TotalDevices ?? 0,
                        TotalUnitLoads = analysisResults.ElectricalResults?.TotalUnitLoads ?? 0
                    },

                    IDNACSummary = new
                    {
                        TotalIdnacsNeeded = analysisResults.IDNACResults?.TotalIdnacsNeeded ?? 0,
                        TotalCurrent = analysisResults.IDNACResults?.TotalCurrent ?? 0,
                        TotalUnitLoads = analysisResults.IDNACResults?.TotalUnitLoads ?? 0,
                        TotalDevices = analysisResults.IDNACResults?.TotalDevices ?? 0,
                        LevelBreakdown = analysisResults.IDNACResults?.LevelResults?.ToDictionary(
                            lr => lr.Key,
                            lr => (object)new
                            {
                                lr.Value.IdnacsRequired,
                                TotalCurrent = lr.Value.Current,
                                TotalUnitLoads = lr.Value.UnitLoads,
                                DeviceCount = lr.Value.Devices,
                                lr.Value.LimitingFactor
                            }) ?? new Dictionary<string, object>()
                    },

                    IDNETSummary = new
                    {
                        TotalDevices = analysisResults.IDNETResults?.TotalDevices ?? 0,
                        ChannelsRequired = analysisResults.IDNETResults?.ChannelsRequired ?? 0,
                        MaxDevicesPerChannel = analysisResults.IDNETResults?.MaxDevicesPerChannel ?? 0,
                        TotalUnitLoads = analysisResults.IDNETResults?.TotalUnitLoads ?? 0
                    },

                    ConfigurationUsed = new
                    {
                        SpareCapacityPercent = ConfigurationService.Current.Spare.SpareFractionDefault * 100,
                        CurrentLimit = ConfigurationService.Current.Capacity.IdnacAlarmCurrentLimitA,
                        UnitLoadLimit = ConfigurationService.Current.Capacity.IdnacStandbyUnitLoadLimit,
                        VoltageDropLimit = ConfigurationService.Current.Capacity.MaxVoltageDropPct * 100,
                        RepeaterPolicy = ConfigurationService.Current.Repeater.TreatRepeaterAsFreshBudget
                    }
                };

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error exporting system analysis to JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Import system configuration from JSON
        /// </summary>
        public FireAlarmConfiguration ImportConfigurationFromJSON(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var config = JsonConvert.DeserializeObject<FireAlarmConfiguration>(json);
                
                if (config == null)
                {
                    throw new InvalidDataException("Invalid configuration JSON format");
                }

                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error importing configuration from JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export current configuration to JSON
        /// </summary>
        public void ExportConfigurationToJSON(string filePath)
        {
            try
            {
                var config = ConfigurationService.Current;
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error exporting configuration to JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate comprehensive system report in multiple formats
        /// </summary>
        public void GenerateComprehensiveReport(ComprehensiveAnalysisResults analysisResults, string outputDirectory)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var baseFileName = $"IDNAC_Analysis_{timestamp}";

                // Export device schedule
                if (analysisResults.ElectricalResults?.LevelData?.Any() == true)
                {
                    var allDevices = ExtractDevicesFromResults(analysisResults);
                    if (allDevices.Any())
                    {
                        ExportDeviceScheduleToCSV(allDevices, Path.Combine(outputDirectory, $"{baseFileName}_DeviceSchedule.csv"));
                        ExportQuantityTakeoffToCSV(allDevices, Path.Combine(outputDirectory, $"{baseFileName}_QuantityTakeoff.csv"));
                    }
                }

                // Export system analysis
                ExportSystemAnalysisToJSON(analysisResults, Path.Combine(outputDirectory, $"{baseFileName}_SystemAnalysis.json"));

                // Export current configuration
                ExportConfigurationToJSON(Path.Combine(outputDirectory, $"{baseFileName}_Configuration.json"));

                // Generate summary report
                GenerateSummaryReport(analysisResults, Path.Combine(outputDirectory, $"{baseFileName}_Summary.txt"));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating comprehensive report: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse CSV line into DeviceSnapshot
        /// </summary>
        private DeviceSnapshot ParseCSVLine(string line)
        {
            var values = ParseCSVValues(line);
            
            if (values.Length < 14)
            {
                throw new InvalidDataException($"Expected 14 columns, found {values.Length}");
            }

            return new DeviceSnapshot(
                int.Parse(values[0]),
                values[1],
                values[2], 
                values[3],
                double.Parse(values[4], CultureInfo.InvariantCulture),
                double.Parse(values[5], CultureInfo.InvariantCulture),
                int.Parse(values[6]),
                bool.Parse(values[7]),
                bool.Parse(values[8]),
                bool.Parse(values[9]),
                bool.Parse(values[10])
            );
        }

        /// <summary>
        /// Parse CSV values handling quoted strings
        /// </summary>
        private string[] ParseCSVValues(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        /// <summary>
        /// Escape CSV values containing special characters
        /// </summary>
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return value.Replace("\"", "\"\"");
            }

            return value;
        }

        /// <summary>
        /// Calculate device quantities for takeoff report
        /// </summary>
        private List<DeviceQuantity> CalculateQuantities(List<DeviceSnapshot> devices)
        {
            return devices
                .GroupBy(d => new { d.FamilyName, d.TypeName, d.DeviceType, d.Zone, d.HasStrobe, d.HasSpeaker })
                .Select(g => new DeviceQuantity
                {
                    Family = g.Key.FamilyName,
                    Type = g.Key.TypeName,
                    DeviceType = g.Key.DeviceType,
                    Zone = g.Key.Zone,
                    HasStrobe = g.Key.HasStrobe,
                    HasSpeaker = g.Key.HasSpeaker,
                    Quantity = g.Count(),
                    TotalWatts = g.Sum(d => d.Watts),
                    TotalAmps = g.Sum(d => d.Amps),
                    TotalUnitLoads = g.Sum(d => d.UnitLoads)
                })
                .ToList();
        }

        /// <summary>
        /// Extract device snapshots from analysis results (simplified for demo)
        /// </summary>
        private List<DeviceSnapshot> ExtractDevicesFromResults(ComprehensiveAnalysisResults analysisResults)
        {
            // In a real implementation, this would extract devices from the analysis results
            // For now, return empty list as the results don't contain original device snapshots
            return new List<DeviceSnapshot>();
        }

        /// <summary>
        /// Generate text summary report
        /// </summary>
        private void GenerateSummaryReport(ComprehensiveAnalysisResults analysisResults, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                writer.WriteLine("FIRE ALARM SYSTEM ANALYSIS SUMMARY");
                writer.WriteLine("===================================");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Analysis Scope: {analysisResults.Scope}");
                writer.WriteLine($"Analysis Time: {analysisResults.TotalAnalysisTime.TotalSeconds:F1} seconds");
                writer.WriteLine($"Elements Analyzed: {analysisResults.TotalElementsAnalyzed}");
                writer.WriteLine();

                // System configuration summary
                var config = ConfigurationService.Current;
                writer.WriteLine("SYSTEM CONFIGURATION:");
                writer.WriteLine($"  Spare Capacity: {config.Spare.SpareFractionDefault*100:F0}%");
                writer.WriteLine($"  Current Limit: {config.Capacity.IdnacAlarmCurrentLimitA:F1}A");
                writer.WriteLine($"  Unit Load Limit: {config.Capacity.IdnacStandbyUnitLoadLimit} UL");
                writer.WriteLine($"  Voltage Drop Limit: {config.Capacity.MaxVoltageDropPct*100:F0}%");
                writer.WriteLine();

                // IDNAC summary
                if (analysisResults.IDNACResults != null)
                {
                    writer.WriteLine("IDNAC (NOTIFICATION) SYSTEM:");
                    writer.WriteLine($"  Total IDNACs Required: {analysisResults.IDNACResults.TotalIdnacsNeeded}");
                    writer.WriteLine($"  Total Current: {analysisResults.IDNACResults.TotalCurrent:F2}A");
                    writer.WriteLine($"  Total Unit Loads: {analysisResults.IDNACResults.TotalUnitLoads}");
                    writer.WriteLine($"  Total Devices: {analysisResults.IDNACResults.TotalDevices}");
                    writer.WriteLine();
                }

                // IDNET summary
                if (analysisResults.IDNETResults != null)
                {
                    writer.WriteLine("IDNET (DETECTION) SYSTEM:");
                    writer.WriteLine($"  Total Devices: {analysisResults.IDNETResults.TotalDevices}");
                    writer.WriteLine($"  Channels Required: {analysisResults.IDNETResults.ChannelsRequired}");
                    writer.WriteLine($"  Max Devices/Channel: {analysisResults.IDNETResults.MaxDevicesPerChannel}");
                    writer.WriteLine($"  Total Unit Loads: {analysisResults.IDNETResults.TotalUnitLoads}");
                    writer.WriteLine();
                }

                writer.WriteLine("Analysis completed successfully.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating summary report: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Device quantity summary for takeoff reports
    /// </summary>
    public class DeviceQuantity
    {
        public string Family { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public bool HasStrobe { get; set; }
        public bool HasSpeaker { get; set; }
        public int Quantity { get; set; }
        public double TotalWatts { get; set; }
        public double TotalAmps { get; set; }
        public int TotalUnitLoads { get; set; }
    }
}