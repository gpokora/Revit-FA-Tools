using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class PowerSupplyCalculationService
    {
        private readonly ValidationService _validationService;
        
        public PowerSupplyCalculationService()
        {
            _validationService = new ValidationService();
        }
        
        public async Task<PowerSupplyAnalysisResult> AnalyzePowerSupplyRequirements(
            List<CircuitBranch> branches,
            List<AmplifierRequirement> amplifierReqs,
            PowerSupplyConfiguration config,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var result = new PowerSupplyAnalysisResult();
            var startTime = DateTime.Now;
            
            try
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Power Supply Analysis",
                    Current = 0,
                    Total = 100,
                    Message = "Analyzing power requirements..."
                });
                
                var powerSupplies = await OrganizeBranchesIntoPowerSupplies(
                    branches, amplifierReqs, config, cancellationToken, progress);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Power Supply Analysis",
                    Current = 70,
                    Total = 100,
                    Message = "Calculating cabinet configurations..."
                });
                
                var cabinetConfigs = CalculateCabinetConfigurations(powerSupplies, config);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Power Supply Analysis",
                    Current = 90,
                    Total = 100,
                    Message = "Generating recommendations..."
                });
                
                result.PowerSupplies = powerSupplies;
                result.CabinetConfigurations = cabinetConfigs;
                result.SystemSummary = CalculateSystemSummary(powerSupplies, cabinetConfigs);
                result.Recommendations = GeneratePowerSupplyRecommendations(result);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Power Supply Analysis",
                    Current = 100,
                    Total = 100,
                    Message = "Analysis complete",
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
        
        private async Task<List<PowerSupply>> OrganizeBranchesIntoPowerSupplies(
            List<CircuitBranch> branches,
            List<AmplifierRequirement> amplifierReqs,
            PowerSupplyConfiguration config,
            CancellationToken cancellationToken,
            IProgress<AnalysisProgress> progress)
        {
            var powerSupplies = new List<PowerSupply>();
            var processedBranches = 0;
            var totalBranches = branches.Count;
            
            var sortedBranches = branches
                .OrderByDescending(b => b.TotalAlarmCurrent)
                .ThenBy(b => GetBranchLevel(b))
                .ToList();
            
            PowerSupply currentPS = null;
            var psNumber = 1;
            
            foreach (var branch in sortedBranches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (currentPS == null || !CanAddBranchToPowerSupply(currentPS, branch, amplifierReqs, config))
                {
                    currentPS = CreateNewPowerSupply(psNumber, config);
                    powerSupplies.Add(currentPS);
                    psNumber++;
                    
                    AssignAmplifiersToPS(currentPS, amplifierReqs);
                }
                
                AddBranchToPowerSupply(currentPS, branch);
                processedBranches++;
                
                if (progress != null && processedBranches % 10 == 0)
                {
                    progress.Report(new AnalysisProgress
                    {
                        Operation = "Power Supply Analysis",
                        Current = (processedBranches * 60) / totalBranches,
                        Total = 100,
                        Message = $"Processed {processedBranches}/{totalBranches} branches"
                    });
                }
            }
            
            foreach (var ps in powerSupplies)
            {
                await ValidateAndOptimizePowerSupply(ps, config, cancellationToken);
            }
            
            return powerSupplies;
        }
        
        private PowerSupply CreateNewPowerSupply(int number, PowerSupplyConfiguration config)
        {
            return new PowerSupply
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"ES-PS-{number}",
                MaxAlarmCurrent = config.ESPSCapacity, // TotalCapacity is calculated from this
                SparePercent = config.SpareCapacityPercent,
                MaxBranches = config.MaxBranchesPerPS
            };
        }
        
        private bool CanAddBranchToPowerSupply(
            PowerSupply ps, 
            CircuitBranch branch, 
            List<AmplifierRequirement> amplifierReqs,
            PowerSupplyConfiguration config)
        {
            if (ps.Branches.Count >= ps.MaxBranches)
                return false;
            
            var projectedAlarmLoad = ps.TotalAlarmLoad + branch.TotalAlarmCurrent;
            var projectedStandbyLoad = ps.TotalStandbyLoad + branch.TotalStandbyCurrent;
            
            var amplifierLoad = CalculateAmplifierLoad(amplifierReqs, GetBranchLevel(branch));
            projectedAlarmLoad += amplifierLoad;
            projectedStandbyLoad += amplifierLoad;
            
            return projectedAlarmLoad <= ps.UsableCapacity && 
                   projectedStandbyLoad <= ps.UsableCapacity;
        }
        
        private void AddBranchToPowerSupply(PowerSupply ps, CircuitBranch branch)
        {
            branch.PowerSupplyId = ps.Id;
            branch.BranchNumber = ps.Branches.Count + 1;
            ps.Branches.Add(branch);
        }
        
        private void AssignAmplifiersToPS(PowerSupply ps, List<AmplifierRequirement> amplifierReqs)
        {
            var totalAmplifierCurrent = 0.0;
            var totalAmplifierBlocks = 0;
            
            foreach (var req in amplifierReqs)
            {
                totalAmplifierCurrent += req.AmplifierCurrent;
                totalAmplifierBlocks += req.BlocksRequired;
            }
            
            // AmplifierCurrent and AmplifierBlocks are computed properties based on TotalAlarmLoad
        }
        
        private double CalculateAmplifierLoad(List<AmplifierRequirement> amplifierReqs, string level)
        {
            return amplifierReqs
                .Where(req => req.ServingLevels?.Contains(level) == true)
                .Sum(req => req.AmplifierCurrent);
        }
        
        private string GetBranchLevel(CircuitBranch branch)
        {
            return branch.Devices.FirstOrDefault()?.Level ?? "Unknown";
        }
        
        private async Task ValidateAndOptimizePowerSupply(
            PowerSupply ps, 
            PowerSupplyConfiguration config,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var validation = _validationService.ValidatePowerSupply(ps);
            ps.Validation = validation;
            
            if (!validation.IsValid && ps.Branches.Count > 1)
            {
                TryRebalancePowerSupply(ps, config, cancellationToken);
            }
        }
        
        private void TryRebalancePowerSupply(
            PowerSupply ps,
            PowerSupplyConfiguration config,
            CancellationToken cancellationToken)
        {
            var sortedBranches = ps.Branches
                .OrderBy(b => b.TotalAlarmCurrent)
                .ToList();
            
            var lightestBranch = sortedBranches.FirstOrDefault();
            if (lightestBranch != null && ps.Branches.Count > 1)
            {
                ps.Branches.Remove(lightestBranch);
                lightestBranch.PowerSupplyId = null;
                lightestBranch.BranchNumber = 0;
            }
        }
        
        private List<CabinetConfiguration> CalculateCabinetConfigurations(
            List<PowerSupply> powerSupplies,
            PowerSupplyConfiguration config)
        {
            var configurations = new List<CabinetConfiguration>();
            
            var totalPowerSupplies = powerSupplies.Count;
            var totalAmplifierBlocks = powerSupplies.Sum(ps => ps.AmplifierBlocks);
            var totalIDNACs = powerSupplies.Sum(ps => ps.Branches.Count);
            
            var cabinetConfig = DetermineCabinetType(totalPowerSupplies, totalAmplifierBlocks, totalIDNACs, config);
            configurations.Add(cabinetConfig);
            
            return configurations;
        }
        
        private CabinetConfiguration DetermineCabinetType(
            int powerSupplies,
            int amplifierBlocks,
            int idnacs,
            PowerSupplyConfiguration config)
        {
            var totalBlocksNeeded = amplifierBlocks + (idnacs / 3);
            
            string cabinetType;
            int availableBlocks;
            
            if (totalBlocksNeeded <= config.SingleBayBlocks)
            {
                cabinetType = "Single Bay";
                availableBlocks = config.SingleBayBlocks;
            }
            else if (totalBlocksNeeded <= config.TwoBayBlocks)
            {
                cabinetType = "Two Bay";
                availableBlocks = config.TwoBayBlocks;
            }
            else
            {
                cabinetType = "Three Bay";
                availableBlocks = config.ThreeBayBlocks;
            }
            
            var totalCapacity = powerSupplies * config.ESPSCapacity;
            var totalLoad = powerSupplies * config.ESPSCapacity * 0.8;
            var margin = (totalCapacity - totalLoad) / totalCapacity * 100;
            
            return new CabinetConfiguration
            {
                CabinetType = cabinetType,
                PowerSupplies = powerSupplies,
                TotalIdnacs = idnacs,
                AvailableBlocks = availableBlocks,
                AmplifierBlocksUsed = amplifierBlocks,
                RemainingBlocks = availableBlocks - totalBlocksNeeded,
                EsPsCapacity = totalCapacity,
                PowerMargin = margin,
                BatteryChargerAvailable = availableBlocks > totalBlocksNeeded + 2,
                ModelConfig = GenerateModelConfiguration(cabinetType, powerSupplies, amplifierBlocks)
            };
        }
        
        private List<string> GenerateModelConfiguration(string cabinetType, int powerSupplies, int amplifierBlocks)
        {
            var config = new List<string>();
            
            config.Add($"Cabinet: {cabinetType}");
            config.Add($"Power Supplies: {powerSupplies}x ES-PS");
            
            if (amplifierBlocks > 0)
            {
                config.Add($"Amplifier Blocks: {amplifierBlocks}");
            }
            
            return config;
        }
        
        private PowerSupplySystemSummary CalculateSystemSummary(
            List<PowerSupply> powerSupplies,
            List<CabinetConfiguration> cabinetConfigs)
        {
            var summary = new PowerSupplySystemSummary
            {
                TotalPowerSupplies = powerSupplies.Count,
                TotalIDNACs = powerSupplies.Sum(ps => ps.Branches.Count),
                TotalCapacity = powerSupplies.Sum(ps => ps.TotalCapacity),
                TotalAlarmLoad = powerSupplies.Sum(ps => ps.TotalAlarmLoad),
                TotalStandbyLoad = powerSupplies.Sum(ps => ps.TotalStandbyLoad),
                TotalAmplifierBlocks = powerSupplies.Sum(ps => ps.AmplifierBlocks),
                AverageUtilization = powerSupplies.Average(ps => (ps.TotalAlarmLoad / ps.TotalCapacity) * 100),
                CabinetType = cabinetConfigs.FirstOrDefault()?.CabinetType ?? "Unknown"
            };
            
            summary.SpareCapacityPercent = ((summary.TotalCapacity - summary.TotalAlarmLoad) / summary.TotalCapacity) * 100;
            summary.EfficiencyRating = CalculateEfficiencyRating(summary);
            
            return summary;
        }
        
        private string CalculateEfficiencyRating(PowerSupplySystemSummary summary)
        {
            var utilization = summary.AverageUtilization;
            
            if (utilization >= 75 && utilization <= 85)
                return "EXCELLENT";
            else if (utilization >= 65 && utilization <= 90)
                return "GOOD";
            else if (utilization < 50)
                return "UNDERUTILIZED";
            else if (utilization > 95)
                return "OVERLOADED";
            else
                return "ADEQUATE";
        }
        
        private List<PowerSupplyRecommendation> GeneratePowerSupplyRecommendations(PowerSupplyAnalysisResult result)
        {
            var recommendations = new List<PowerSupplyRecommendation>();
            
            var summary = result.SystemSummary;
            
            if (summary.AverageUtilization > 90)
            {
                recommendations.Add(new PowerSupplyRecommendation
                {
                    Priority = "HIGH",
                    Category = "CAPACITY",
                    Message = $"High system utilization ({summary.AverageUtilization:F1}%) - consider additional power supplies",
                    Impact = "System may not handle peak loads safely",
                    Action = "Add additional ES-PS or redistribute loads"
                });
            }
            
            if (summary.SpareCapacityPercent < 20)
            {
                recommendations.Add(new PowerSupplyRecommendation
                {
                    Priority = "MEDIUM",
                    Category = "SPARE_CAPACITY",
                    Message = $"Spare capacity below recommended 20% ({summary.SpareCapacityPercent:F1}%)",
                    Impact = "Limited capacity for future expansion",
                    Action = "Consider increasing power supply capacity or reducing loads"
                });
            }
            
            var unbalancedPS = result.PowerSupplies.Where(ps => 
                Math.Abs(ps.TotalAlarmLoad - summary.TotalAlarmLoad / result.PowerSupplies.Count) > 
                summary.TotalAlarmLoad * 0.3 / result.PowerSupplies.Count).ToList();
            
            if (unbalancedPS.Any())
            {
                recommendations.Add(new PowerSupplyRecommendation
                {
                    Priority = "MEDIUM",
                    Category = "LOAD_BALANCE",
                    Message = $"{unbalancedPS.Count} power supplies have unbalanced loads",
                    Impact = "Reduced system efficiency and reliability",
                    Action = "Redistribute circuit branches for better load balancing"
                });
            }
            
            var cabinetConfig = result.CabinetConfigurations.FirstOrDefault();
            if (cabinetConfig != null && cabinetConfig.RemainingBlocks < 2)
            {
                recommendations.Add(new PowerSupplyRecommendation
                {
                    Priority = "HIGH",
                    Category = "CABINET_SPACE",
                    Message = $"Cabinet space nearly full ({cabinetConfig.RemainingBlocks} blocks remaining)",
                    Impact = "No room for future expansion or battery charger",
                    Action = "Consider upgrading to larger cabinet or additional panels"
                });
            }
            
            return recommendations;
        }
    }
    
    public class PowerSupplyConfiguration
    {
        public double ESPSCapacity { get; set; } = 9.5;
        public double SpareCapacityPercent { get; set; } = 20.0;
        public int MaxBranchesPerPS { get; set; } = 3;
        
        public int SingleBayBlocks { get; set; } = 4;
        public int TwoBayBlocks { get; set; } = 14;
        public int ThreeBayBlocks { get; set; } = 22;
        
        public bool RequireBatteryCharger { get; set; } = true;
        public double LoadBalanceThreshold { get; set; } = 0.3;
    }
    
    public class CabinetConfiguration
    {
        public string CabinetType { get; set; }
        public int PowerSupplies { get; set; }
        public int TotalIdnacs { get; set; }
        public int AvailableBlocks { get; set; }
        public int AmplifierBlocksUsed { get; set; }
        public int RemainingBlocks { get; set; }
        public double AmplifierCurrent { get; set; }
        public double EsPsCapacity { get; set; }
        public double PowerMargin { get; set; }
        public bool BatteryChargerAvailable { get; set; }
        public List<string> ModelConfig { get; set; } = new List<string>();
    }
    
    public class AmplifierRequirement
    {
        public string AmplifierType { get; set; }
        public int BlocksRequired { get; set; }
        public double AmplifierCurrent { get; set; }
        public int SpeakerCount { get; set; }
        public List<string> ServingLevels { get; set; } = new List<string>();
    }
    
    public class PowerSupplyAnalysisResult
    {
        public List<PowerSupply> PowerSupplies { get; set; } = new List<PowerSupply>();
        public List<CabinetConfiguration> CabinetConfigurations { get; set; } = new List<CabinetConfiguration>();
        public PowerSupplySystemSummary SystemSummary { get; set; }
        public List<PowerSupplyRecommendation> Recommendations { get; set; } = new List<PowerSupplyRecommendation>();
        public string Error { get; set; }
    }
    
    public class PowerSupplySystemSummary
    {
        public int TotalPowerSupplies { get; set; }
        public int TotalIDNACs { get; set; }
        public double TotalCapacity { get; set; }
        public double TotalAlarmLoad { get; set; }
        public double TotalStandbyLoad { get; set; }
        public int TotalAmplifierBlocks { get; set; }
        public double AverageUtilization { get; set; }
        public double SpareCapacityPercent { get; set; }
        public string EfficiencyRating { get; set; }
        public string CabinetType { get; set; }
    }
    
    public class PowerSupplyRecommendation
    {
        public string Priority { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string Impact { get; set; }
        public string Action { get; set; }
    }
}