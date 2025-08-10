using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    public class CircuitOrganizationService
    {
        private readonly double IDNAC_ALARM_CURRENT_LIMIT = 3.0;
        private readonly int IDNAC_UNIT_LOAD_LIMIT = 139;
        private readonly double DEFAULT_SPARE_PERCENT = 20.0;
        
        private double _sparePercent;
        
        public CircuitOrganizationService(double sparePercent = 20.0)
        {
            _sparePercent = Math.Max(0, Math.Min(50, sparePercent));
        }
        
        public double UsableAlarmCurrent => IDNAC_ALARM_CURRENT_LIMIT * (1 - _sparePercent / 100.0);
        public int UsableUnitLoads => (int)(IDNAC_UNIT_LOAD_LIMIT * (1 - _sparePercent / 100.0));
        
        public Task<CircuitOrganizationResult> OrganizeDevicesIntoBranches(
            List<DeviceSnapshot> devices,
            CircuitBalancingOptions options,
            CancellationToken cancellationToken = default,
            IProgress<AnalysisProgress> progress = null)
        {
            var result = new CircuitOrganizationResult();
            var startTime = DateTime.Now;
            
            try
            {
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Organizing devices into circuits",
                    Current = 0,
                    Total = devices.Count,
                    Message = "Starting circuit organization..."
                });
                
                var devicesByLevel = GroupDevicesByLevel(devices, options.ExcludedLevels);
                
                var branches = new List<CircuitBranch>();
                var powerSupplies = new List<PowerSupply>();
                var currentPS = CreateNewPowerSupply();
                powerSupplies.Add(currentPS);
                
                int processedDevices = 0;
                
                foreach (var levelGroup in devicesByLevel.OrderBy(g => GetLevelSortOrder(g.Key)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var levelDevices = levelGroup.Value;
                    var levelBranches = CreateBranchesForLevel(levelGroup.Key, levelDevices, options);
                    
                    foreach (var branch in levelBranches)
                    {
                        if (!TryAddBranchToPowerSupply(currentPS, branch))
                        {
                            currentPS = CreateNewPowerSupply();
                            powerSupplies.Add(currentPS);
                            TryAddBranchToPowerSupply(currentPS, branch);
                        }
                        
                        branches.Add(branch);
                        processedDevices += branch.Devices.Count;
                        
                        progress?.Report(new AnalysisProgress
                        {
                            Operation = "Organizing devices into circuits",
                            Current = processedDevices,
                            Total = devices.Count,
                            Message = $"Processing {levelGroup.Key}",
                            ElapsedTime = DateTime.Now - startTime,
                            EstimatedTimeRemaining = EstimateTimeRemaining(processedDevices, devices.Count, DateTime.Now - startTime)
                        });
                    }
                }
                
                result.Branches = branches;
                result.PowerSupplies = powerSupplies;
                result.TotalDevices = devices.Count;
                result.TotalBranches = branches.Count;
                result.TotalPowerSupplies = powerSupplies.Count;
                result.ValidationResult = ValidateCircuitOrganization(result);
                
                progress?.Report(new AnalysisProgress
                {
                    Operation = "Organizing devices into circuits",
                    Current = devices.Count,
                    Total = devices.Count,
                    Message = "Circuit organization complete",
                    ElapsedTime = DateTime.Now - startTime
                });
                
                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                result.ValidationResult.AddError("Operation cancelled by user");
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                result.ValidationResult.AddError($"Circuit organization failed: {ex.Message}");
                return Task.FromResult(result);
            }
        }
        
        private Dictionary<string, List<DeviceSnapshot>> GroupDevicesByLevel(
            List<DeviceSnapshot> devices, 
            HashSet<string> excludedLevels)
        {
            var groups = new Dictionary<string, List<DeviceSnapshot>>();
            
            foreach (var device in devices)
            {
                if (excludedLevels != null && excludedLevels.Contains(device.Level))
                    continue;
                
                if (!groups.ContainsKey(device.Level))
                    groups[device.Level] = new List<DeviceSnapshot>();
                
                groups[device.Level].Add(device);
            }
            
            return groups;
        }
        
        private List<CircuitBranch> CreateBranchesForLevel(
            string level, 
            List<DeviceSnapshot> devices,
            CircuitBalancingOptions options)
        {
            var branches = new List<CircuitBranch>();
            var sortedDevices = devices.OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
            
            var currentBranch = new CircuitBranch
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{level} - Branch 1"
            };
            branches.Add(currentBranch);
            
            foreach (var device in sortedDevices)
            {
                if (!CanAddDeviceToBranch(currentBranch, device))
                {
                    currentBranch = new CircuitBranch
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = $"{level} - Branch {branches.Count + 1}"
                    };
                    branches.Add(currentBranch);
                }
                
                currentBranch.Devices.Add(device);
            }
            
            if (options.BalanceLoads)
            {
                branches = BalanceBranchLoads(branches);
            }
            
            return branches;
        }
        
        private bool CanAddDeviceToBranch(CircuitBranch branch, DeviceSnapshot device)
        {
            var newAlarmCurrent = branch.TotalAlarmCurrent + device.AlarmCurrent;
            var newUnitLoads = branch.TotalUnitLoads + device.UnitLoads;
            
            return newAlarmCurrent <= UsableAlarmCurrent && newUnitLoads <= UsableUnitLoads;
        }
        
        private List<CircuitBranch> BalanceBranchLoads(List<CircuitBranch> branches)
        {
            if (branches.Count <= 1)
                return branches;
            
            bool improved = true;
            while (improved)
            {
                improved = false;
                
                for (int i = 0; i < branches.Count - 1; i++)
                {
                    for (int j = i + 1; j < branches.Count; j++)
                    {
                        var branch1 = branches[i];
                        var branch2 = branches[j];
                        
                        var variance = CalculateLoadVariance(branches);
                        
                        if (TrySwapDevices(branch1, branch2))
                        {
                            var newVariance = CalculateLoadVariance(branches);
                            if (newVariance < variance)
                            {
                                improved = true;
                            }
                            else
                            {
                                TrySwapDevices(branch1, branch2);
                            }
                        }
                    }
                }
            }
            
            return branches;
        }
        
        private bool TrySwapDevices(CircuitBranch branch1, CircuitBranch branch2)
        {
            if (branch1.Devices.Count == 0 || branch2.Devices.Count == 0)
                return false;
            
            var device1 = branch1.Devices.Last();
            var device2 = branch2.Devices.Last();
            
            branch1.Devices.RemoveAt(branch1.Devices.Count - 1);
            branch2.Devices.RemoveAt(branch2.Devices.Count - 1);
            
            if (CanAddDeviceToBranch(branch1, device2) && CanAddDeviceToBranch(branch2, device1))
            {
                branch1.Devices.Add(device2);
                branch2.Devices.Add(device1);
                return true;
            }
            else
            {
                branch1.Devices.Add(device1);
                branch2.Devices.Add(device2);
                return false;
            }
        }
        
        private double CalculateLoadVariance(List<CircuitBranch> branches)
        {
            var loads = branches.Select(b => b.TotalAlarmCurrent).ToList();
            var mean = loads.Average();
            return loads.Sum(l => Math.Pow(l - mean, 2)) / loads.Count;
        }
        
        private PowerSupply CreateNewPowerSupply()
        {
            return new PowerSupply
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"ES-PS-{DateTime.Now.Ticks}",
                SparePercent = _sparePercent
            };
        }
        
        private bool TryAddBranchToPowerSupply(PowerSupply ps, CircuitBranch branch)
        {
            if (ps.Branches.Count >= ps.MaxBranches)
                return false;
            
            branch.PowerSupplyId = ps.Id;
            branch.BranchNumber = ps.Branches.Count + 1;
            ps.Branches.Add(branch);
            
            return ps.TotalAlarmLoad <= ps.UsableCapacity;
        }
        
        private Revit_FA_Tools.Models.ValidationResult ValidateCircuitOrganization(CircuitOrganizationResult result)
        {
            var validation = new Revit_FA_Tools.Models.ValidationResult();
            
            foreach (var branch in result.Branches)
            {
                ValidateBranch(branch, validation);
            }
            
            foreach (var ps in result.PowerSupplies)
            {
                ValidatePowerSupply(ps, validation);
            }
            
            return validation;
        }
        
        private void ValidateBranch(CircuitBranch branch, Revit_FA_Tools.Models.ValidationResult validation)
        {
            if (branch.TotalAlarmCurrent > IDNAC_ALARM_CURRENT_LIMIT)
            {
                validation.AddError($"Branch {branch.Name} exceeds alarm current limit: {branch.TotalAlarmCurrent:F2}A > {IDNAC_ALARM_CURRENT_LIMIT}A");
            }
            
            if (branch.TotalUnitLoads > IDNAC_UNIT_LOAD_LIMIT)
            {
                validation.AddError($"Branch {branch.Name} exceeds unit load limit: {branch.TotalUnitLoads} > {IDNAC_UNIT_LOAD_LIMIT}");
            }
            
            if (branch.TotalAlarmCurrent > UsableAlarmCurrent)
            {
                validation.AddWarning($"Branch {branch.Name} exceeds usable capacity (with {_sparePercent}% spare): {branch.TotalAlarmCurrent:F2}A > {UsableAlarmCurrent:F2}A");
            }
        }
        
        private void ValidatePowerSupply(PowerSupply ps, Revit_FA_Tools.Models.ValidationResult validation)
        {
            if (ps.TotalAlarmLoad > ps.TotalCapacity)
            {
                validation.AddError($"Power supply {ps.Name} exceeds total capacity: {ps.TotalAlarmLoad:F2}A > {ps.TotalCapacity}A");
            }
            
            if (ps.TotalAlarmLoad > ps.UsableCapacity)
            {
                validation.AddWarning($"Power supply {ps.Name} exceeds usable capacity (with {ps.SparePercent}% spare): {ps.TotalAlarmLoad:F2}A > {ps.UsableCapacity:F2}A");
            }
        }
        
        private int GetLevelSortOrder(string level)
        {
            if (string.IsNullOrEmpty(level))
                return 9999;
            
            var upper = level.ToUpper();
            
            if (upper.Contains("BASEMENT") || upper.Contains("B1") || upper.Contains("B2"))
                return -1000;
            if (upper.Contains("PARKING") || upper.Contains("P1") || upper.Contains("P2"))
                return -500;
            if (upper.Contains("GROUND") || upper.Contains("LOBBY"))
                return 0;
            if (upper.Contains("LEVEL"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(upper, @"LEVEL\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int levelNum))
                    return levelNum;
            }
            if (upper.Contains("ROOF") || upper.Contains("MECH"))
                return 9000;
            
            return 1000;
        }
        
        private TimeSpan EstimateTimeRemaining(int current, int total, TimeSpan elapsed)
        {
            if (current == 0)
                return TimeSpan.Zero;
            
            var rate = elapsed.TotalSeconds / current;
            var remaining = (total - current) * rate;
            return TimeSpan.FromSeconds(remaining);
        }
    }
    
    public class CircuitOrganizationResult
    {
        public List<CircuitBranch> Branches { get; set; } = new List<CircuitBranch>();
        public List<PowerSupply> PowerSupplies { get; set; } = new List<PowerSupply>();
        public int TotalDevices { get; set; }
        public int TotalBranches { get; set; }
        public int TotalPowerSupplies { get; set; }
        public Revit_FA_Tools.Models.ValidationResult ValidationResult { get; set; } = new Revit_FA_Tools.Models.ValidationResult();
    }
    
    public class CircuitBalancingOptions
    {
        public bool BalanceLoads { get; set; } = true;
        public HashSet<string> ExcludedLevels { get; set; } = new HashSet<string>();
        public double TargetUtilization { get; set; } = 75.0;
        public bool AllowCrossLevelBalancing { get; set; } = false;
    }
}