using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Services
{
    public class IDNACEngineeringEngine
    {
        private readonly CircuitOrganizationService _circuitService;
        private readonly CircuitBalancingService _balancingService;
        private readonly ValidationService _validationService;
        private readonly PowerSupplyCalculationService _powerSupplyService;
        private readonly CableCalculationService _cableService;
        private readonly ReportingService _reportingService;
        private readonly ConfigurationManagementService _configService;
        
        public IDNACEngineeringEngine()
        {
            _configService = new ConfigurationManagementService();
            var config = _configService.GetSystemConfiguration();
            
            _circuitService = new CircuitOrganizationService(config.SpareCapacityPercent);
            _balancingService = new CircuitBalancingService(config.SpareCapacityPercent);
            _validationService = new ValidationService();
            _powerSupplyService = new PowerSupplyCalculationService();
            _cableService = new CableCalculationService();
            _reportingService = new ReportingService();
        }
        
        public async Task<EngineeringAnalysisResult> PerformComprehensiveAnalysis(
            List<FamilyInstance> revitElements,
            AnalysisConfiguration analysisConfig,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var result = new EngineeringAnalysisResult();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 0,
                    Total = 100,
                    Message = "Converting Revit elements to device snapshots..."
                });
                
                var deviceSnapshots = await ConvertRevitElementsToDeviceSnapshots(
                    revitElements, cancellationToken, progress);
                
                _configService.ApplyDeviceProfiles(deviceSnapshots);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 15,
                    Total = 100,
                    Message = "Organizing circuits..."
                });
                
                var circuitOptions = CreateCircuitBalancingOptions(analysisConfig);
                var balancingResult = await _balancingService.BalanceCircuits(
                    deviceSnapshots, circuitOptions, cancellationToken, progress);
                
                if (!string.IsNullOrEmpty(balancingResult.Error))
                {
                    result.Error = balancingResult.Error;
                    return result;
                }
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 40,
                    Total = 100,
                    Message = "Calculating power supply requirements..."
                });
                
                var amplifierReqs = CalculateAmplifierRequirements(deviceSnapshots, analysisConfig);
                var powerConfig = CreatePowerSupplyConfiguration();
                var powerSupplyResult = await _powerSupplyService.AnalyzePowerSupplyRequirements(
                    balancingResult.Branches, amplifierReqs, powerConfig, cancellationToken, progress);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 65,
                    Total = 100,
                    Message = "Analyzing cable requirements..."
                });
                
                var cableAnalyses = _cableService.AnalyzeAllBranches(balancingResult.Branches);
                var cableSystemSummary = _cableService.AnalyzeSystemCabling(cableAnalyses);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 80,
                    Total = 100,
                    Message = "Validating system compliance..."
                });
                
                var circuitOrganizationResult = new CircuitOrganizationResult
                {
                    Branches = balancingResult.Branches,
                    PowerSupplies = powerSupplyResult.PowerSupplies,
                    TotalDevices = deviceSnapshots.Count,
                    TotalBranches = balancingResult.Branches.Count,
                    TotalPowerSupplies = powerSupplyResult.PowerSupplies.Count
                };
                
                var validationSummary = _validationService.ValidateSystem(circuitOrganizationResult);
                
                result.DeviceSnapshots = deviceSnapshots;
                result.CircuitBalancingResult = balancingResult;
                result.PowerSupplyResult = powerSupplyResult;
                result.CableAnalyses = cableAnalyses;
                result.CableSystemSummary = cableSystemSummary;
                result.ValidationSummary = validationSummary;
                result.SystemMetrics = CalculateSystemMetrics(result);
                result.Recommendations = GenerateSystemRecommendations(result);
                
                stopwatch.Stop();
                result.AnalysisTime = stopwatch.Elapsed;
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Comprehensive IDNAC Analysis",
                    Current = 100,
                    Total = 100,
                    Message = "Analysis complete",
                    ElapsedTime = stopwatch.Elapsed
                });
                
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }
        
        private async Task<List<DeviceSnapshot>> ConvertRevitElementsToDeviceSnapshots(
            List<FamilyInstance> revitElements,
            CancellationToken cancellationToken,
            IProgress<AnalysisProgress> progress)
        {
            return await Task.Run(() =>
            {
                var devices = new List<DeviceSnapshot>();
            var processedElements = 0;
            
            foreach (var element in revitElements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var location = GetElementLocation(element);
                    var elementLevel = GetElementLevel(element);
                    var elementZone = GetElementZone(element);
                    
                    // Extract electrical parameters first
                    var electricalData = ExtractElectricalParametersData(element);
                    
                    var device = new DeviceSnapshot(
                        ElementId: (int)element.Id.Value, // Use Value instead of deprecated IntegerValue
                        LevelName: elementLevel,
                        FamilyName: element.Symbol.FamilyName,
                        TypeName: element.Symbol.Name,
                        Watts: electricalData.Watts,
                        Amps: electricalData.Amps,
                        UnitLoads: electricalData.UnitLoads,
                        HasStrobe: electricalData.HasStrobe,
                        HasSpeaker: electricalData.HasSpeaker,
                        IsIsolator: electricalData.IsIsolator,
                        IsRepeater: electricalData.IsRepeater,
                        Zone: elementZone,
                        X: location.X,
                        Y: location.Y,
                        Z: location.Z,
                        StandbyCurrent: electricalData.StandbyCurrent,
                        HasOverride: electricalData.HasOverride,
                        CustomProperties: electricalData.CustomProperties
                    );
                    
                    device = _validationService.ApplyDeviceOverrides(device);
                    
                    devices.Add(device);
                    processedElements++;
                    
                    if (progress != null && processedElements % 50 == 0)
                    {
                        var progressPercent = 5 + (processedElements * 10 / revitElements.Count);
                        progress.Report(new AnalysisProgress
                        {
                            Operation = "Comprehensive IDNAC Analysis",
                            Current = progressPercent,
                            Total = 100,
                            Message = $"Processed {processedElements}/{revitElements.Count} elements"
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing element {element.Id}: {ex.Message}");
                }
            }
            
            return devices;
            }, cancellationToken);
        }
        
        private string GetElementLevel(FamilyInstance element)
        {
            try
            {
                // Try to get level from host first
                if (element.Host is Level hostLevel)
                {
                    return hostLevel.Name;
                }
                
                // Try to get level parameter
                var levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam != null && levelParam.HasValue)
                {
                    var levelId = levelParam.AsElementId();
                    var level = element.Document.GetElement(levelId) as Level;
                    if (level != null)
                    {
                        return level.Name;
                    }
                }
                
                // Try to get reference level
                var refLevelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                if (refLevelParam != null && refLevelParam.HasValue)
                {
                    var levelId = refLevelParam.AsElementId();
                    var level = element.Document.GetElement(levelId) as Level;
                    if (level != null)
                    {
                        return level.Name;
                    }
                }
                
                return "Unknown Level";
            }
            catch
            {
                return "Unknown Level";
            }
        }
        
        private string GetElementZone(FamilyInstance element)
        {
            try
            {
                var zoneParam = element.LookupParameter("Zone") ?? 
                               element.LookupParameter("ZONE") ??
                               element.LookupParameter("Fire Zone");
                
                return zoneParam?.AsString() ?? "Default Zone";
            }
            catch
            {
                return "Default Zone";
            }
        }
        
        private XYZ GetElementLocation(FamilyInstance element)
        {
            try
            {
                if (element.Location is LocationPoint locationPoint)
                {
                    return locationPoint.Point;
                }
                else if (element.Location is LocationCurve locationCurve)
                {
                    var curve = locationCurve.Curve;
                    return (curve.GetEndPoint(0) + curve.GetEndPoint(1)) / 2;
                }
            }
            catch
            {
                // Fallback to origin
            }
            
            return XYZ.Zero;
        }
        
        private ElectricalParameterData ExtractElectricalParametersData(FamilyInstance element)
        {
            var data = new ElectricalParameterData();
            
            try
            {
                // Extract current parameters
                var alarmCurrentParam = element.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD) ??
                                       element.LookupParameter("Alarm Current") ??
                                       element.LookupParameter("ALARM_CURRENT") ??
                                       element.LookupParameter("Current");
                
                if (alarmCurrentParam != null && alarmCurrentParam.HasValue)
                {
                    data.Amps = alarmCurrentParam.AsDouble();
                }
                
                // Extract standby current
                var standbyCurrentParam = element.LookupParameter("Standby Current") ??
                                         element.LookupParameter("STANDBY_CURRENT");
                
                if (standbyCurrentParam != null && standbyCurrentParam.HasValue)
                {
                    data.StandbyCurrent = standbyCurrentParam.AsDouble();
                }
                
                // Extract watts
                var wattsParam = element.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD) ??
                                element.LookupParameter("Watts") ??
                                element.LookupParameter("Wattage") ??
                                element.LookupParameter("Power");
                
                if (wattsParam != null && wattsParam.HasValue)
                {
                    data.Watts = wattsParam.AsDouble();
                }
                
                // Extract unit loads
                var unitLoadsParam = element.LookupParameter("Unit Loads") ??
                                   element.LookupParameter("UNIT_LOADS") ??
                                   element.LookupParameter("UL");
                
                if (unitLoadsParam != null && unitLoadsParam.HasValue)
                {
                    data.UnitLoads = unitLoadsParam.AsInteger();
                }
                
                // Determine device capabilities from family/type names
                var familyName = element.Symbol.FamilyName?.ToUpper() ?? "";
                var typeName = element.Symbol.Name?.ToUpper() ?? "";
                
                data.HasStrobe = familyName.Contains("STROBE") || typeName.Contains("STROBE");
                data.HasSpeaker = familyName.Contains("SPEAKER") || typeName.Contains("SPEAKER") || 
                                 familyName.Contains("HORN") || typeName.Contains("HORN");
                data.IsIsolator = familyName.Contains("ISOLATOR") || typeName.Contains("ISOLATOR");
                data.IsRepeater = familyName.Contains("REPEATER") || typeName.Contains("REPEATER");
                
                // Extract custom properties
                data.CustomProperties = new Dictionary<string, object>();
                foreach (Parameter param in element.Parameters)
                {
                    try
                    {
                        if (param.HasValue && !param.IsReadOnly)
                        {
                            var paramValue = (object)(param.StorageType switch
                            {
                                StorageType.Double => param.AsDouble(),
                                StorageType.Integer => param.AsInteger(),
                                StorageType.String => param.AsString(),
                                _ => param.AsValueString()
                            });
                            
                            data.CustomProperties[param.Definition.Name] = paramValue;
                        }
                    }
                    catch
                    {
                        // Skip parameters that can't be read
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting parameters: {ex.Message}");
            }
            
            return data;
        }
        
        private BalancingConfiguration CreateCircuitBalancingOptions(AnalysisConfiguration analysisConfig)
        {
            var systemConfig = _configService.GetSystemConfiguration();
            
            return new BalancingConfiguration
            {
                TargetUtilizationPercent = systemConfig.TargetUtilizationPercent,
                EnableIntraLevelBalancing = systemConfig.EnableAutoBalancing,
                EnableCrossLevelOptimization = analysisConfig.EnableCrossLevelOptimization,
                ExcludeVillaLevels = systemConfig.ExcludeVillaLevels,
                ExcludeGarageLevels = systemConfig.ExcludeGarageLevels,
                ExcludedLevels = analysisConfig.ExcludedLevels ?? new HashSet<string>(),
                MaxLevelMergeDistance = analysisConfig.MaxLevelMergeDistance
            };
        }
        
        private List<AmplifierRequirement> CalculateAmplifierRequirements(
            List<DeviceSnapshot> devices, 
            AnalysisConfiguration config)
        {
            var amplifierReqs = new List<AmplifierRequirement>();
            
            var speakerDevices = devices.Where(d => 
                d.FamilyName?.ToUpper().Contains("SPEAKER") == true ||
                d.DeviceType?.ToUpper().Contains("NOTIFICATION") == true).ToList();
            
            if (speakerDevices.Any())
            {
                var speakersByLevel = speakerDevices.GroupBy(d => d.Level);
                
                foreach (var levelGroup in speakersByLevel)
                {
                    var speakerCount = levelGroup.Count();
                    var totalCurrent = levelGroup.Sum(d => d.AlarmCurrent);
                    
                    string amplifierType;
                    int blocksRequired;
                    double amplifierCurrent;
                    
                    if (totalCurrent <= 1.8)
                    {
                        amplifierType = "Flex-35";
                        blocksRequired = 1;
                        amplifierCurrent = 1.8;
                    }
                    else if (totalCurrent <= 2.4)
                    {
                        amplifierType = "Flex-50";
                        blocksRequired = 1;
                        amplifierCurrent = 2.4;
                    }
                    else
                    {
                        amplifierType = "Flex-100";
                        blocksRequired = 2;
                        amplifierCurrent = 9.6;
                    }
                    
                    amplifierReqs.Add(new AmplifierRequirement
                    {
                        AmplifierType = amplifierType,
                        BlocksRequired = blocksRequired,
                        AmplifierCurrent = amplifierCurrent,
                        SpeakerCount = speakerCount,
                        ServingLevels = new List<string> { levelGroup.Key }
                    });
                }
            }
            
            return amplifierReqs;
        }
        
        private PowerSupplyConfiguration CreatePowerSupplyConfiguration()
        {
            var systemConfig = _configService.GetSystemConfiguration();
            
            return new PowerSupplyConfiguration
            {
                ESPSCapacity = systemConfig.ESPSCapacity,
                SpareCapacityPercent = systemConfig.SpareCapacityPercent,
                MaxBranchesPerPS = systemConfig.MaxBranchesPerPS
            };
        }
        
        private SystemMetrics CalculateSystemMetrics(EngineeringAnalysisResult result)
        {
            var metrics = new SystemMetrics
            {
                TotalDevices = result.DeviceSnapshots.Count,
                TotalBranches = result.CircuitBalancingResult.Branches.Count,
                TotalPowerSupplies = result.PowerSupplyResult.PowerSupplies.Count,
                TotalAlarmCurrent = result.CircuitBalancingResult.Branches.Sum(b => b.TotalAlarmCurrent),
                TotalStandbyCurrent = result.CircuitBalancingResult.Branches.Sum(b => b.TotalStandbyCurrent),
                TotalUnitLoads = result.CircuitBalancingResult.Branches.Sum(b => b.TotalUnitLoads),
                TotalCableLength = result.CableSystemSummary.TotalCableLength,
                AverageUtilization = result.CircuitBalancingResult.Statistics.AverageBranchUtilization,
                SystemEfficiencyScore = result.CircuitBalancingResult.Statistics.LoadBalanceScore,
                ComplianceScore = CalculateComplianceScore(result.ValidationSummary)
            };
            
            var totalCapacity = result.PowerSupplyResult.PowerSupplies.Sum(ps => ps.TotalCapacity);
            var totalLoad = result.PowerSupplyResult.PowerSupplies.Sum(ps => ps.TotalAlarmLoad);
            metrics.SystemSpareCapacityPercent = ((totalCapacity - totalLoad) / totalCapacity) * 100;
            
            return metrics;
        }
        
        private double CalculateComplianceScore(ValidationSummary validationSummary)
        {
            if (validationSummary == null)
                return 100;
            
            var totalIssues = validationSummary.TotalErrors + validationSummary.TotalWarnings;
            if (totalIssues == 0)
                return 100;
            
            var errorPenalty = validationSummary.TotalErrors * 10;
            var warningPenalty = validationSummary.TotalWarnings * 2;
            
            return Math.Max(0, 100 - errorPenalty - warningPenalty);
        }
        
        private List<string> GenerateSystemRecommendations(EngineeringAnalysisResult result)
        {
            var recommendations = new List<string>();
            
            recommendations.AddRange(result.CircuitBalancingResult.Recommendations);
            recommendations.AddRange(result.PowerSupplyResult.Recommendations.Select(r => r.Message));
            
            if (result.SystemMetrics.AverageUtilization < 60)
            {
                recommendations.Add("System utilization is low - consider consolidating circuits for cost optimization");
            }
            
            if (result.SystemMetrics.SystemSpareCapacityPercent < 15)
            {
                recommendations.Add("System spare capacity is below recommended minimum - consider additional power supplies");
            }
            
            if (result.ValidationSummary.HasErrors)
            {
                recommendations.Add($"Address {result.ValidationSummary.TotalErrors} compliance errors before installation");
            }
            
            if (result.CableSystemSummary.ComplianceIssues > 0)
            {
                recommendations.Add($"Review {result.CableSystemSummary.ComplianceIssues} cable compliance issues");
            }
            
            return recommendations.Distinct().ToList();
        }
        
        public async Task<ReportGenerationResult> GenerateComprehensiveReport(
            EngineeringAnalysisResult analysisResult,
            string projectName,
            string templateId = "COMPREHENSIVE",
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var reportRequest = new ReportRequest
            {
                TemplateId = templateId,
                ProjectName = projectName,
                Format = "HTML",
                PowerSupplies = analysisResult.PowerSupplyResult.PowerSupplies,
                Branches = analysisResult.CircuitBalancingResult.Branches,
                CableAnalyses = analysisResult.CableAnalyses,
                ValidationSummary = analysisResult.ValidationSummary,
                SystemSummary = analysisResult.SystemMetrics,
                Recommendations = analysisResult.Recommendations
            };
            
            return await _reportingService.GenerateReport(reportRequest, cancellationToken, progress);
        }
        
        public ConfigurationManagementService GetConfigurationService()
        {
            return _configService;
        }
        
        public ValidationService GetValidationService()
        {
            return _validationService;
        }
    }
    
    public class AnalysisConfiguration
    {
        public bool EnableCrossLevelOptimization { get; set; } = true;
        public HashSet<string> ExcludedLevels { get; set; } = new HashSet<string>();
        public int MaxLevelMergeDistance { get; set; } = 1;
        public bool IncludeAmplifierAnalysis { get; set; } = true;
        public bool IncludeCableAnalysis { get; set; } = true;
        public bool GenerateDetailedReports { get; set; } = true;
    }
    
    public class EngineeringAnalysisResult
    {
        public List<DeviceSnapshot> DeviceSnapshots { get; set; } = new List<DeviceSnapshot>();
        public CircuitBalancingResult CircuitBalancingResult { get; set; }
        public PowerSupplyAnalysisResult PowerSupplyResult { get; set; }
        public List<CableAnalysisResult> CableAnalyses { get; set; } = new List<CableAnalysisResult>();
        public CableSystemSummary CableSystemSummary { get; set; }
        public ValidationSummary ValidationSummary { get; set; }
        public SystemMetrics SystemMetrics { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public TimeSpan AnalysisTime { get; set; }
        public string Error { get; set; }
    }
    
    public class SystemMetrics
    {
        public int TotalDevices { get; set; }
        public int TotalBranches { get; set; }
        public int TotalPowerSupplies { get; set; }
        public double TotalAlarmCurrent { get; set; }
        public double TotalStandbyCurrent { get; set; }
        public int TotalUnitLoads { get; set; }
        public double TotalCableLength { get; set; }
        public double AverageUtilization { get; set; }
        public double SystemEfficiencyScore { get; set; }
        public double SystemSpareCapacityPercent { get; set; }
        public double ComplianceScore { get; set; }
    }
    
    /// <summary>
    /// Data structure for extracted electrical parameters from Revit elements
    /// </summary>
    public class ElectricalParameterData
    {
        public double Watts { get; set; }
        public double Amps { get; set; }
        public int UnitLoads { get; set; }
        public bool HasStrobe { get; set; }
        public bool HasSpeaker { get; set; }
        public bool IsIsolator { get; set; }
        public bool IsRepeater { get; set; }
        public double StandbyCurrent { get; set; }
        public bool HasOverride { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }
}