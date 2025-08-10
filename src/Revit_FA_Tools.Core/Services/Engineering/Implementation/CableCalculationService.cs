using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class CableCalculationService
    {
        private readonly Dictionary<string, CableSpecification> _cableSpecs;
        private readonly double NOMINAL_VOLTAGE = 24.0;
        private readonly double VOLTAGE_DROP_LIMIT_PERCENT = 10.0;
        
        public CableCalculationService()
        {
            _cableSpecs = InitializeCableSpecifications();
        }
        
        private Dictionary<string, CableSpecification> InitializeCableSpecifications()
        {
            return new Dictionary<string, CableSpecification>
            {
                ["18AWG"] = new CableSpecification 
                { 
                    AWGSize = 18, 
                    ResistancePerKFeet = 6.385, 
                    MaxCurrent = 7.0, 
                    Description = "18 AWG Fire Alarm Cable" 
                },
                ["16AWG"] = new CableSpecification 
                { 
                    AWGSize = 16, 
                    ResistancePerKFeet = 4.016, 
                    MaxCurrent = 10.0, 
                    Description = "16 AWG Fire Alarm Cable" 
                },
                ["14AWG"] = new CableSpecification 
                { 
                    AWGSize = 14, 
                    ResistancePerKFeet = 2.525, 
                    MaxCurrent = 15.0, 
                    Description = "14 AWG Fire Alarm Cable" 
                },
                ["12AWG"] = new CableSpecification 
                { 
                    AWGSize = 12, 
                    ResistancePerKFeet = 1.588, 
                    MaxCurrent = 20.0, 
                    Description = "12 AWG Fire Alarm Cable" 
                }
            };
        }
        
        public CableAnalysisResult AnalyzeCableRequirements(CircuitBranch branch)
        {
            var result = new CableAnalysisResult();
            
            if (branch.Devices == null || branch.Devices.Count == 0)
            {
                result.ValidationResult.AddError("No devices in branch for cable analysis");
                return result;
            }
            
            var cableLength = CalculateCableLength(branch);
            var recommendedCable = RecommendCableSize(branch.TotalAlarmCurrent, cableLength);
            
            result.CableLength = cableLength;
            result.RecommendedCableSpec = recommendedCable;
            result.VoltageDrop = CalculateVoltageDrop(branch.TotalAlarmCurrent, cableLength, recommendedCable);
            result.VoltageDropPercent = (result.VoltageDrop / NOMINAL_VOLTAGE) * 100;
            
            result.AlternativeCables = FindAlternativeCables(branch.TotalAlarmCurrent, cableLength);
            result.CostAnalysis = PerformCostAnalysis(result.AlternativeCables, cableLength);
            
            ValidateCableSelection(result);
            
            return result;
        }
        
        public List<CableAnalysisResult> AnalyzeAllBranches(List<CircuitBranch> branches)
        {
            var results = new List<CableAnalysisResult>();
            
            foreach (var branch in branches)
            {
                var analysis = AnalyzeCableRequirements(branch);
                analysis.BranchId = branch.Id;
                analysis.BranchName = branch.Name;
                results.Add(analysis);
            }
            
            return results;
        }
        
        private double CalculateCableLength(CircuitBranch branch)
        {
            if (branch.Devices.Count == 0)
                return 0;
            
            if (branch.CableLength > 0)
                return branch.CableLength;
            
            var devices = branch.Devices.OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
            double totalLength = 0;
            
            for (int i = 0; i < devices.Count - 1; i++)
            {
                var device1 = devices[i];
                var device2 = devices[i + 1];
                
                var distance = Math.Sqrt(
                    Math.Pow(device2.X - device1.X, 2) +
                    Math.Pow(device2.Y - device1.Y, 2) +
                    Math.Pow(device2.Z - device1.Z, 2));
                
                totalLength += distance;
            }
            
            totalLength *= 1.2;
            
            return totalLength;
        }
        
        private CableSpecification RecommendCableSize(double current, double length)
        {
            var orderedCables = _cableSpecs.Values
                .Where(spec => spec.MaxCurrent >= current)
                .OrderBy(spec => spec.AWGSize)
                .ToList();
            
            foreach (var cable in orderedCables)
            {
                var voltageDrop = CalculateVoltageDrop(current, length, cable);
                var voltageDropPercent = (voltageDrop / NOMINAL_VOLTAGE) * 100;
                
                if (voltageDropPercent <= VOLTAGE_DROP_LIMIT_PERCENT)
                {
                    return cable;
                }
            }
            
            return orderedCables.LastOrDefault() ?? _cableSpecs["12AWG"];
        }
        
        private double CalculateVoltageDrop(double current, double lengthFeet, CableSpecification cable)
        {
            if (cable == null || lengthFeet <= 0 || current <= 0)
                return 0;
            
            var resistance = cable.ResistancePerKFeet * (lengthFeet / 1000.0);
            
            return current * resistance * 2;
        }
        
        private List<CableOption> FindAlternativeCables(double current, double length)
        {
            var options = new List<CableOption>();
            
            foreach (var cable in _cableSpecs.Values.Where(c => c.MaxCurrent >= current))
            {
                var voltageDrop = CalculateVoltageDrop(current, length, cable);
                var voltageDropPercent = (voltageDrop / NOMINAL_VOLTAGE) * 100;
                
                var option = new CableOption
                {
                    CableSpec = cable,
                    VoltageDrop = voltageDrop,
                    VoltageDropPercent = voltageDropPercent,
                    IsCompliant = voltageDropPercent <= VOLTAGE_DROP_LIMIT_PERCENT,
                    MarginToLimit = VOLTAGE_DROP_LIMIT_PERCENT - voltageDropPercent
                };
                
                options.Add(option);
            }
            
            return options.OrderBy(o => o.CableSpec.AWGSize).ToList();
        }
        
        private CableCostAnalysis PerformCostAnalysis(List<CableOption> options, double length)
        {
            var analysis = new CableCostAnalysis();
            
            var baseCosts = new Dictionary<int, double>
            {
                [18] = 0.85,
                [16] = 1.15, 
                [14] = 1.65,
                [12] = 2.35
            };
            
            foreach (var option in options)
            {
                var costPerFoot = baseCosts.GetValueOrDefault(option.CableSpec.AWGSize, 2.0);
                var totalCost = costPerFoot * length;
                
                var costOption = new CableCostOption
                {
                    CableSpec = option.CableSpec,
                    CostPerFoot = costPerFoot,
                    TotalCost = totalCost,
                    IsCompliant = option.IsCompliant,
                    VoltageDropPercent = option.VoltageDropPercent
                };
                
                analysis.CostOptions.Add(costOption);
            }
            
            analysis.RecommendedOption = analysis.CostOptions
                .Where(o => o.IsCompliant)
                .OrderBy(o => o.TotalCost)
                .FirstOrDefault();
            
            analysis.PremiumOption = analysis.CostOptions
                .Where(o => o.IsCompliant)
                .OrderBy(o => o.CableSpec.AWGSize)
                .FirstOrDefault();
            
            if (analysis.RecommendedOption != null && analysis.PremiumOption != null)
            {
                analysis.CostDifference = analysis.PremiumOption.TotalCost - analysis.RecommendedOption.TotalCost;
                analysis.CostDifferencePercent = (analysis.CostDifference / analysis.RecommendedOption.TotalCost) * 100;
            }
            
            return analysis;
        }
        
        private void ValidateCableSelection(CableAnalysisResult result)
        {
            var validation = result.ValidationResult;
            
            if (result.RecommendedCableSpec == null)
            {
                validation.AddError("No suitable cable found for current and distance requirements");
                return;
            }
            
            if (result.VoltageDropPercent > VOLTAGE_DROP_LIMIT_PERCENT)
            {
                validation.AddError($"Voltage drop ({result.VoltageDropPercent:F1}%) exceeds limit ({VOLTAGE_DROP_LIMIT_PERCENT}%)");
            }
            else if (result.VoltageDropPercent > VOLTAGE_DROP_LIMIT_PERCENT * 0.8)
            {
                validation.AddWarning($"Voltage drop ({result.VoltageDropPercent:F1}%) approaching limit ({VOLTAGE_DROP_LIMIT_PERCENT}%)");
            }
            
            if (result.CableLength > 3000)
            {
                validation.AddWarning($"Long cable run ({result.CableLength:F0} ft) - consider using repeater");
            }
            
            if (result.CableLength > 5000)
            {
                validation.AddError($"Cable run too long ({result.CableLength:F0} ft) - repeater required");
            }
        }
        
        public CableSystemSummary AnalyzeSystemCabling(List<CableAnalysisResult> branchAnalyses)
        {
            var summary = new CableSystemSummary();
            
            summary.TotalBranches = branchAnalyses.Count;
            summary.TotalCableLength = branchAnalyses.Sum(a => a.CableLength);
            summary.AverageVoltageDropPercent = branchAnalyses.Average(a => a.VoltageDropPercent);
            summary.MaxVoltageDropPercent = branchAnalyses.Max(a => a.VoltageDropPercent);
            
            summary.CableBreakdown = branchAnalyses
                .Where(a => a.RecommendedCableSpec != null)
                .GroupBy(a => a.RecommendedCableSpec.AWGSize)
                .ToDictionary(
                    g => $"{g.Key}AWG",
                    g => new CableUsageSummary
                    {
                        BranchCount = g.Count(),
                        TotalLength = g.Sum(a => a.CableLength),
                        AverageLength = g.Average(a => a.CableLength)
                    });
            
            summary.ComplianceIssues = branchAnalyses
                .Where(a => !a.ValidationResult.IsValid)
                .Count();
            
            summary.WarningCount = branchAnalyses
                .Sum(a => a.ValidationResult.Issues.Count(i => i.Severity == "WARNING"));
            
            var totalCost = branchAnalyses
                .Where(a => a.CostAnalysis?.RecommendedOption != null)
                .Sum(a => a.CostAnalysis.RecommendedOption.TotalCost);
            
            summary.EstimatedCableCost = totalCost;
            
            return summary;
        }
    }
    
    public class CableSpecification
    {
        public int AWGSize { get; set; }
        public double ResistancePerKFeet { get; set; }
        public double MaxCurrent { get; set; }
        public string Description { get; set; }
    }
    
    public class CableAnalysisResult
    {
        public string BranchId { get; set; }
        public string BranchName { get; set; }
        public double CableLength { get; set; }
        public CableSpecification RecommendedCableSpec { get; set; }
        public double VoltageDrop { get; set; }
        public double VoltageDropPercent { get; set; }
        public List<CableOption> AlternativeCables { get; set; } = new List<CableOption>();
        public CableCostAnalysis CostAnalysis { get; set; }
        public Revit_FA_Tools.Models.ValidationResult ValidationResult { get; set; } = new Revit_FA_Tools.Models.ValidationResult();
    }
    
    public class CableOption
    {
        public CableSpecification CableSpec { get; set; }
        public double VoltageDrop { get; set; }
        public double VoltageDropPercent { get; set; }
        public bool IsCompliant { get; set; }
        public double MarginToLimit { get; set; }
    }
    
    public class CableCostAnalysis
    {
        public List<CableCostOption> CostOptions { get; set; } = new List<CableCostOption>();
        public CableCostOption RecommendedOption { get; set; }
        public CableCostOption PremiumOption { get; set; }
        public double CostDifference { get; set; }
        public double CostDifferencePercent { get; set; }
    }
    
    public class CableCostOption
    {
        public CableSpecification CableSpec { get; set; }
        public double CostPerFoot { get; set; }
        public double TotalCost { get; set; }
        public bool IsCompliant { get; set; }
        public double VoltageDropPercent { get; set; }
    }
    
    public class CableSystemSummary
    {
        public int TotalBranches { get; set; }
        public double TotalCableLength { get; set; }
        public double AverageVoltageDropPercent { get; set; }
        public double MaxVoltageDropPercent { get; set; }
        public Dictionary<string, CableUsageSummary> CableBreakdown { get; set; } = new Dictionary<string, CableUsageSummary>();
        public int ComplianceIssues { get; set; }
        public int WarningCount { get; set; }
        public double EstimatedCableCost { get; set; }
    }
    
    public class CableUsageSummary
    {
        public int BranchCount { get; set; }
        public double TotalLength { get; set; }
        public double AverageLength { get; set; }
    }
}