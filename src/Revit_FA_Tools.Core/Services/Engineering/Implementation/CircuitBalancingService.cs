using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class CircuitBalancingService
    {
        private readonly ValidationService _validationService;
        private readonly double _sparePercent;
        
        public CircuitBalancingService(double sparePercent = 20.0)
        {
            _validationService = new ValidationService();
            _sparePercent = sparePercent;
        }
        
        public async Task<CircuitBalancingResult> BalanceCircuits(
            List<DeviceSnapshot> devices,
            BalancingConfiguration config,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var result = new CircuitBalancingResult();
            var startTime = DateTime.Now;
            
            try
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Circuit Balancing",
                    Current = 0,
                    Total = 100,
                    Message = "Grouping devices by level..."
                });
                
                var levelGroups = GroupDevicesByLevel(devices, config);
                var allBranches = new List<CircuitBranch>();
                var processedLevels = 0;
                
                foreach (var levelGroup in levelGroups.OrderBy(g => GetLevelPriority(g.Key)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var levelBranches = await BalanceLevelDevices(
                        levelGroup.Key, 
                        levelGroup.Value, 
                        config,
                        cancellationToken);
                    
                    allBranches.AddRange(levelBranches);
                    processedLevels++;
                    
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Circuit Balancing",
                        Current = (processedLevels * 50) / levelGroups.Count,
                        Total = 100,
                        Message = $"Balanced {levelGroup.Key}"
                    });
                }
                
                if (!config.MergeLevelsFromExcludedCategories)
                {
                    progress?.Report(new AnalysisProgress
                    {
                        Operation = "Circuit Balancing",
                        Current = 60,
                        Total = 100,
                        Message = "Optimizing cross-level balance..."
                    });
                    
                    allBranches = await OptimizeCrossLevelBalance(allBranches, config, cancellationToken);
                }
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Circuit Balancing",
                    Current = 80,
                    Total = 100,
                    Message = "Organizing into power supplies..."
                });
                
                var powerSupplies = OrganizeIntoPowerSupplies(allBranches, config);
                
                result.Branches = allBranches;
                result.PowerSupplies = powerSupplies;
                result.Statistics = CalculateStatistics(allBranches, powerSupplies);
                result.Recommendations = GenerateRecommendations(result.Statistics);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Circuit Balancing",
                    Current = 100,
                    Total = 100,
                    Message = "Balancing complete",
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
        
        private Dictionary<string, List<DeviceSnapshot>> GroupDevicesByLevel(
            List<DeviceSnapshot> devices, 
            BalancingConfiguration config)
        {
            var groups = new Dictionary<string, List<DeviceSnapshot>>();
            
            foreach (var device in devices)
            {
                var level = device.Level ?? "Unknown";
                
                if (ShouldExcludeLevel(level, config))
                    continue;
                
                if (!groups.ContainsKey(level))
                    groups[level] = new List<DeviceSnapshot>();
                
                groups[level].Add(device);
            }
            
            return groups;
        }
        
        private bool ShouldExcludeLevel(string level, BalancingConfiguration config)
        {
            if (config.ExcludedLevels?.Contains(level) == true)
                return true;
            
            var levelUpper = level.ToUpper();
            
            if (config.ExcludeVillaLevels && levelUpper.Contains("VILLA"))
                return true;
            if (config.ExcludeGarageLevels && (levelUpper.Contains("GARAGE") || levelUpper.Contains("PARKING")))
                return true;
            if (config.ExcludeMechanicalLevels && (levelUpper.Contains("MECH") || levelUpper.Contains("ROOF")))
                return true;
            
            return false;
        }
        
        private async Task<List<CircuitBranch>> BalanceLevelDevices(
            string level,
            List<DeviceSnapshot> devices,
            BalancingConfiguration config,
            CancellationToken cancellationToken)
        {
            var branches = new List<CircuitBranch>();
            
            var sortedDevices = devices
                .OrderBy(d => d.Zone ?? "")
                .ThenBy(d => d.X)
                .ThenBy(d => d.Y)
                .ToList();
            
            var targetUtilization = config.TargetUtilizationPercent / 100.0;
            var maxAlarmCurrent = 3.0 * (1 - _sparePercent / 100.0) * targetUtilization;
            var maxUnitLoads = (int)(139 * (1 - _sparePercent / 100.0) * targetUtilization);
            
            var currentBranch = CreateNewBranch(level, branches.Count + 1);
            branches.Add(currentBranch);
            
            foreach (var device in sortedDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var projectedCurrent = currentBranch.TotalAlarmCurrent + device.AlarmCurrent;
                var projectedUnitLoads = currentBranch.TotalUnitLoads + device.UnitLoads;
                
                if (projectedCurrent > maxAlarmCurrent || projectedUnitLoads > maxUnitLoads)
                {
                    if (currentBranch.Devices.Count > 0)
                    {
                        currentBranch = CreateNewBranch(level, branches.Count + 1);
                        branches.Add(currentBranch);
                    }
                }
                
                currentBranch.Devices.Add(device);
            }
            
            branches = branches.Where(b => b.Devices.Count > 0).ToList();
            
            if (branches.Count > 1 && config.EnableIntraLevelBalancing)
            {
                branches = await BalanceBranchesWithinLevel(branches, cancellationToken);
            }
            
            return branches;
        }
        
        private CircuitBranch CreateNewBranch(string level, int branchNumber)
        {
            return new CircuitBranch
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{level} - Branch {branchNumber}"
            };
        }
        
        private Task<List<CircuitBranch>> BalanceBranchesWithinLevel(
            List<CircuitBranch> branches,
            CancellationToken cancellationToken)
        {
            const int MAX_ITERATIONS = 100;
            var iteration = 0;
            var improved = true;
            
            while (improved && iteration < MAX_ITERATIONS)
            {
                cancellationToken.ThrowIfCancellationRequested();
                improved = false;
                
                var currentVariance = CalculateLoadVariance(branches);
                
                for (int i = 0; i < branches.Count - 1; i++)
                {
                    for (int j = i + 1; j < branches.Count; j++)
                    {
                        if (TryImproveBalance(branches[i], branches[j], currentVariance))
                        {
                            improved = true;
                            currentVariance = CalculateLoadVariance(branches);
                        }
                    }
                }
                
                iteration++;
            }
            
            return Task.FromResult(branches);
        }
        
        private bool TryImproveBalance(CircuitBranch branch1, CircuitBranch branch2, double currentVariance)
        {
            var bestImprovement = 0.0;
            DeviceSnapshot bestDevice = null;
            CircuitBranch fromBranch = null;
            CircuitBranch toBranch = null;
            
            foreach (var device in branch1.Devices)
            {
                if (CanMoveDevice(branch1, branch2, device))
                {
                    var improvement = CalculateImprovementIfMoved(branch1, branch2, device, currentVariance);
                    if (improvement > bestImprovement)
                    {
                        bestImprovement = improvement;
                        bestDevice = device;
                        fromBranch = branch1;
                        toBranch = branch2;
                    }
                }
            }
            
            foreach (var device in branch2.Devices)
            {
                if (CanMoveDevice(branch2, branch1, device))
                {
                    var improvement = CalculateImprovementIfMoved(branch2, branch1, device, currentVariance);
                    if (improvement > bestImprovement)
                    {
                        bestImprovement = improvement;
                        bestDevice = device;
                        fromBranch = branch2;
                        toBranch = branch1;
                    }
                }
            }
            
            if (bestDevice != null && bestImprovement > 0.01)
            {
                fromBranch.Devices.Remove(bestDevice);
                toBranch.Devices.Add(bestDevice);
                return true;
            }
            
            return false;
        }
        
        private bool CanMoveDevice(CircuitBranch from, CircuitBranch to, DeviceSnapshot device)
        {
            if (from.Devices.Count <= 1)
                return false;
            
            var newToCurrent = to.TotalAlarmCurrent + device.AlarmCurrent;
            var newToUnitLoads = to.TotalUnitLoads + device.UnitLoads;
            
            var maxCurrent = 3.0 * (1 - _sparePercent / 100.0);
            var maxUnitLoads = (int)(139 * (1 - _sparePercent / 100.0));
            
            return newToCurrent <= maxCurrent && newToUnitLoads <= maxUnitLoads;
        }
        
        private double CalculateImprovementIfMoved(
            CircuitBranch from, 
            CircuitBranch to, 
            DeviceSnapshot device,
            double currentVariance)
        {
            var branches = new List<CircuitBranch> { from, to };
            var originalVariance = CalculateLoadVariance(branches);
            
            from.Devices.Remove(device);
            to.Devices.Add(device);
            
            var newVariance = CalculateLoadVariance(branches);
            
            from.Devices.Add(device);
            to.Devices.Remove(device);
            
            return originalVariance - newVariance;
        }
        
        private double CalculateLoadVariance(List<CircuitBranch> branches)
        {
            if (branches.Count <= 1)
                return 0;
            
            var loads = branches.Select(b => b.TotalAlarmCurrent).ToList();
            var mean = loads.Average();
            
            if (mean == 0)
                return 0;
            
            return loads.Sum(l => Math.Pow(l - mean, 2)) / loads.Count;
        }
        
        private async Task<List<CircuitBranch>> OptimizeCrossLevelBalance(
            List<CircuitBranch> branches,
            BalancingConfiguration config,
            CancellationToken cancellationToken)
        {
            if (!config.EnableCrossLevelOptimization)
                return branches;
            
            var underutilizedBranches = branches
                .Where(b => CalculateUtilization(b) < 0.6)
                .OrderBy(b => b.TotalAlarmCurrent)
                .ToList();
            
            var optimizedBranches = new List<CircuitBranch>();
            var processedBranches = new HashSet<string>();
            
            foreach (var branch in branches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (processedBranches.Contains(branch.Id))
                    continue;
                
                if (!underutilizedBranches.Contains(branch))
                {
                    optimizedBranches.Add(branch);
                    processedBranches.Add(branch.Id);
                    continue;
                }
                
                var merged = await TryMergeBranches(branch, underutilizedBranches, processedBranches, config);
                optimizedBranches.Add(merged);
            }
            
            return optimizedBranches;
        }
        
        private Task<CircuitBranch> TryMergeBranches(
            CircuitBranch primary,
            List<CircuitBranch> candidates,
            HashSet<string> processed,
            BalancingConfiguration config)
        {
            processed.Add(primary.Id);
            
            var mergedBranch = new CircuitBranch
            {
                Id = primary.Id,
                Name = primary.Name,
                Devices = new List<DeviceSnapshot>(primary.Devices)
            };
            
            foreach (var candidate in candidates)
            {
                if (processed.Contains(candidate.Id))
                    continue;
                
                if (!CanMergeBranches(mergedBranch, candidate, config))
                    continue;
                
                var projectedCurrent = mergedBranch.TotalAlarmCurrent + candidate.TotalAlarmCurrent;
                var projectedUnitLoads = mergedBranch.TotalUnitLoads + candidate.TotalUnitLoads;
                
                var maxCurrent = 3.0 * (1 - _sparePercent / 100.0);
                var maxUnitLoads = (int)(139 * (1 - _sparePercent / 100.0));
                
                if (projectedCurrent <= maxCurrent && projectedUnitLoads <= maxUnitLoads)
                {
                    mergedBranch.Devices.AddRange(candidate.Devices);
                    mergedBranch.Name = $"{GetLevelFromBranch(primary)} + {GetLevelFromBranch(candidate)}";
                    processed.Add(candidate.Id);
                }
            }
            
            return Task.FromResult(mergedBranch);
        }
        
        private bool CanMergeBranches(CircuitBranch branch1, CircuitBranch branch2, BalancingConfiguration config)
        {
            var level1 = GetLevelFromBranch(branch1);
            var level2 = GetLevelFromBranch(branch2);
            
            if (GetLevelCategory(level1) != GetLevelCategory(level2))
                return false;
            
            var levelDistance = Math.Abs(GetLevelPriority(level1) - GetLevelPriority(level2));
            return levelDistance <= config.MaxLevelMergeDistance;
        }
        
        private string GetLevelFromBranch(CircuitBranch branch)
        {
            return branch.Devices.FirstOrDefault()?.Level ?? "Unknown";
        }
        
        private string GetLevelCategory(string level)
        {
            var upper = level.ToUpper();
            
            if (upper.Contains("VILLA"))
                return "VILLA";
            if (upper.Contains("PARKING") || upper.Contains("GARAGE"))
                return "PARKING";
            if (upper.Contains("MECH") || upper.Contains("ROOF"))
                return "MECHANICAL";
            
            return "MAIN";
        }
        
        private int GetLevelPriority(string level)
        {
            var upper = level.ToUpper();
            
            if (upper.Contains("BASEMENT"))
                return -1000;
            if (upper.Contains("PARKING"))
                return -500;
            if (upper.Contains("GROUND") || upper.Contains("LOBBY"))
                return 0;
            if (upper.Contains("ROOF") || upper.Contains("MECH"))
                return 9000;
            
            var match = System.Text.RegularExpressions.Regex.Match(upper, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                return num;
            
            return 1000;
        }
        
        private double CalculateUtilization(CircuitBranch branch)
        {
            var currentUtil = branch.TotalAlarmCurrent / (3.0 * (1 - _sparePercent / 100.0));
            var unitLoadUtil = branch.TotalUnitLoads / (139.0 * (1 - _sparePercent / 100.0));
            return Math.Max(currentUtil, unitLoadUtil);
        }
        
        private List<PowerSupply> OrganizeIntoPowerSupplies(
            List<CircuitBranch> branches,
            BalancingConfiguration config)
        {
            var powerSupplies = new List<PowerSupply>();
            var currentPS = new PowerSupply
            {
                Id = Guid.NewGuid().ToString(),
                Name = "ES-PS-1",
                SparePercent = _sparePercent
            };
            powerSupplies.Add(currentPS);
            
            var psNumber = 1;
            
            foreach (var branch in branches.OrderByDescending(b => b.TotalAlarmCurrent))
            {
                if (!TryAddBranchToPowerSupply(currentPS, branch))
                {
                    psNumber++;
                    currentPS = new PowerSupply
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = $"ES-PS-{psNumber}",
                        SparePercent = _sparePercent
                    };
                    powerSupplies.Add(currentPS);
                    TryAddBranchToPowerSupply(currentPS, branch);
                }
            }
            
            return powerSupplies;
        }
        
        private bool TryAddBranchToPowerSupply(PowerSupply ps, CircuitBranch branch)
        {
            if (ps.Branches.Count >= ps.MaxBranches)
                return false;
            
            var projectedLoad = ps.TotalAlarmLoad + branch.TotalAlarmCurrent;
            if (projectedLoad > ps.UsableCapacity)
                return false;
            
            branch.PowerSupplyId = ps.Id;
            branch.BranchNumber = ps.Branches.Count + 1;
            ps.Branches.Add(branch);
            
            return true;
        }
        
        private BalancingStatistics CalculateStatistics(
            List<CircuitBranch> branches,
            List<PowerSupply> powerSupplies)
        {
            var stats = new BalancingStatistics
            {
                TotalBranches = branches.Count,
                TotalPowerSupplies = powerSupplies.Count,
                TotalDevices = branches.Sum(b => b.Devices.Count),
                TotalAlarmCurrent = branches.Sum(b => b.TotalAlarmCurrent),
                TotalUnitLoads = branches.Sum(b => b.TotalUnitLoads)
            };
            
            stats.AverageBranchUtilization = branches.Average(b => CalculateUtilization(b)) * 100;
            stats.MinBranchUtilization = branches.Min(b => CalculateUtilization(b)) * 100;
            stats.MaxBranchUtilization = branches.Max(b => CalculateUtilization(b)) * 100;
            
            stats.AveragePowerSupplyUtilization = powerSupplies.Average(ps => 
                ps.TotalAlarmLoad / ps.UsableCapacity) * 100;
            
            stats.LoadBalanceScore = CalculateLoadBalanceScore(branches);
            
            return stats;
        }
        
        private double CalculateLoadBalanceScore(List<CircuitBranch> branches)
        {
            if (branches.Count <= 1)
                return 100;
            
            var variance = CalculateLoadVariance(branches);
            var mean = branches.Average(b => b.TotalAlarmCurrent);
            
            if (mean == 0)
                return 100;
            
            var cv = Math.Sqrt(variance) / mean;
            return Math.Max(0, 100 * (1 - cv));
        }
        
        private List<string> GenerateRecommendations(BalancingStatistics stats)
        {
            var recommendations = new List<string>();
            
            if (stats.MinBranchUtilization < 50)
            {
                recommendations.Add($"Consider merging underutilized branches (lowest: {stats.MinBranchUtilization:F1}%)");
            }
            
            if (stats.MaxBranchUtilization > 90)
            {
                recommendations.Add($"High utilization branches detected ({stats.MaxBranchUtilization:F1}%) - consider redistribution");
            }
            
            if (stats.LoadBalanceScore < 80)
            {
                recommendations.Add($"Load balance score is {stats.LoadBalanceScore:F1}% - consider rebalancing");
            }
            
            if (stats.AveragePowerSupplyUtilization > 80)
            {
                recommendations.Add("Power supplies are highly utilized - ensure adequate spare capacity");
            }
            
            return recommendations;
        }
    }
    
    public class BalancingConfiguration
    {
        public double TargetUtilizationPercent { get; set; } = 75.0;
        public bool EnableIntraLevelBalancing { get; set; } = true;
        public bool EnableCrossLevelOptimization { get; set; } = true;
        public int MaxLevelMergeDistance { get; set; } = 1;
        
        public HashSet<string> ExcludedLevels { get; set; } = new HashSet<string>();
        public bool ExcludeVillaLevels { get; set; } = true;
        public bool ExcludeGarageLevels { get; set; } = true;
        public bool ExcludeMechanicalLevels { get; set; } = false;
        public bool MergeLevelsFromExcludedCategories { get; set; } = false;
    }
    
    public class CircuitBalancingResult
    {
        public List<CircuitBranch> Branches { get; set; } = new List<CircuitBranch>();
        public List<PowerSupply> PowerSupplies { get; set; } = new List<PowerSupply>();
        public BalancingStatistics Statistics { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public string Error { get; set; }
    }
    
    public class BalancingStatistics
    {
        public int TotalBranches { get; set; }
        public int TotalPowerSupplies { get; set; }
        public int TotalDevices { get; set; }
        public double TotalAlarmCurrent { get; set; }
        public int TotalUnitLoads { get; set; }
        
        public double AverageBranchUtilization { get; set; }
        public double MinBranchUtilization { get; set; }
        public double MaxBranchUtilization { get; set; }
        
        public double AveragePowerSupplyUtilization { get; set; }
        public double LoadBalanceScore { get; set; }
    }
}