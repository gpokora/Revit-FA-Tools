using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Advanced circuit balancing service using bin-packing and optimization algorithms
    /// </summary>
    public class CircuitBalancer
    {
        public class CircuitCapacity
        {
            public double MaxCurrent { get; set; } = 3.0;
            public int MaxUnitLoads { get; set; } = 139;
            public int MaxDevices { get; set; } = 127;
            public double SpareCapacityFraction { get; set; } = 0.20;

            public double UsableCurrent => MaxCurrent * (1 - SpareCapacityFraction);
            public int UsableUnitLoads => (int)(MaxUnitLoads * (1 - SpareCapacityFraction));
            public int UsableDevices => (int)(MaxDevices * (1 - SpareCapacityFraction));
        }

        public class DeviceLoad
        {
            public int DeviceId { get; set; }
            public string DeviceName { get; set; }
            public double Current { get; set; }
            public int UnitLoads { get; set; }
            public string Level { get; set; }
            public string Zone { get; set; }
            public bool IsHighPriority { get; set; } // Speakers, strobes
            public DeviceSnapshot SourceDevice { get; set; }

            public double LoadScore => Current * 100 + UnitLoads; // Combined load metric
        }

        public class CircuitAllocation
        {
            public int CircuitId { get; set; }
            public List<DeviceLoad> Devices { get; set; } = new List<DeviceLoad>();
            public double TotalCurrent => Devices.Sum(d => d.Current);
            public int TotalUnitLoads => Devices.Sum(d => d.UnitLoads);
            public int DeviceCount => Devices.Count;
            public string PrimaryLevel { get; set; }
            public string CircuitType { get; set; } = "IDNAC";

            public double CurrentUtilization(CircuitCapacity capacity) => TotalCurrent / capacity.UsableCurrent;
            public double UnitLoadUtilization(CircuitCapacity capacity) => (double)TotalUnitLoads / capacity.UsableUnitLoads;
            public double DeviceUtilization(CircuitCapacity capacity) => (double)DeviceCount / capacity.UsableDevices;
            
            public double MaxUtilization(CircuitCapacity capacity) => Math.Max(
                Math.Max(CurrentUtilization(capacity), UnitLoadUtilization(capacity)),
                DeviceUtilization(capacity));

            public bool CanFit(DeviceLoad device, CircuitCapacity capacity)
            {
                return (TotalCurrent + device.Current) <= capacity.UsableCurrent &&
                       (TotalUnitLoads + device.UnitLoads) <= capacity.UsableUnitLoads &&
                       (DeviceCount + 1) <= capacity.UsableDevices;
            }
        }

        public class BalancingOptions
        {
            public bool UseOptimizedBalancing { get; set; } = true;
            public bool GroupByLevel { get; set; } = true;
            public bool PrioritizeHighLoads { get; set; } = true;
            public double TargetUtilization { get; set; } = 0.75; // 75% target utilization
            public bool AllowMixedLevels { get; set; } = false;
            public List<string> ExcludedLevels { get; set; } = new List<string>();
            public bool BalanceCurrentFirst { get; set; } = true; // vs UL first
        }

        public class BalancingResult
        {
            public List<CircuitAllocation> Circuits { get; set; } = new List<CircuitAllocation>();
            public int TotalCircuitsUsed => Circuits.Count;
            public double AverageUtilization { get; set; }
            public double UtilizationVariance { get; set; }
            public int UnallocatedDevices { get; set; }
            public TimeSpan CalculationTime { get; set; }
            public string AlgorithmUsed { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();

            public double EfficiencyScore => TotalCircuitsUsed > 0 ? AverageUtilization / TotalCircuitsUsed * 100 : 0;
        }

        /// <summary>
        /// Balance devices across circuits using optimized bin-packing algorithm
        /// </summary>
        public BalancingResult BalanceDevices(List<DeviceSnapshot> devices, CircuitCapacity capacity, BalancingOptions options)
        {
            var startTime = DateTime.Now;
            var result = new BalancingResult();

            try
            {
                // Convert device snapshots to load objects
                var deviceLoads = ConvertToDeviceLoads(devices);

                // Apply level exclusions
                if (options.ExcludedLevels.Any())
                {
                    deviceLoads = deviceLoads.Where(d => !options.ExcludedLevels.Contains(d.Level)).ToList();
                }

                // Choose balancing algorithm
                if (options.UseOptimizedBalancing && deviceLoads.Count > 50)
                {
                    result = OptimizedBinPackingBalance(deviceLoads, capacity, options);
                    result.AlgorithmUsed = "Optimized Bin-Packing";
                }
                else if (options.UseOptimizedBalancing)
                {
                    result = FirstFitDecreasingBalance(deviceLoads, capacity, options);
                    result.AlgorithmUsed = "First-Fit Decreasing";
                }
                else
                {
                    result = SequentialFillBalance(deviceLoads, capacity, options);
                    result.AlgorithmUsed = "Sequential Fill (Legacy)";
                }

                // Calculate metrics
                CalculateBalancingMetrics(result, capacity);
                result.CalculationTime = DateTime.Now - startTime;

                return result;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Balancing failed: {ex.Message}");
                result.CalculationTime = DateTime.Now - startTime;
                return result;
            }
        }

        /// <summary>
        /// Advanced bin-packing with load balancing optimization
        /// </summary>
        private BalancingResult OptimizedBinPackingBalance(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options)
        {
            var result = new BalancingResult();
            var circuits = new List<CircuitAllocation>();
            var unallocated = new List<DeviceLoad>();

            // Group devices by level if required
            var deviceGroups = options.GroupByLevel 
                ? devices.GroupBy(d => d.Level).ToList()
                : new[] { devices.GroupBy(d => "ALL").First() }.ToList();

            foreach (var levelGroup in deviceGroups)
            {
                var levelDevices = levelGroup.OrderByDescending(d => d.LoadScore).ToList();
                
                // Use multiple passes with different strategies
                var remaining = BalanceWithMultipleStrategies(levelDevices, capacity, options, circuits);
                unallocated.AddRange(remaining);
            }

            result.Circuits = circuits;
            result.UnallocatedDevices = unallocated.Count;
            
            if (unallocated.Any())
            {
                result.Warnings.Add($"{unallocated.Count} devices could not be allocated");
            }

            return result;
        }

        /// <summary>
        /// Multiple strategy bin-packing for optimal load distribution
        /// </summary>
        private List<DeviceLoad> BalanceWithMultipleStrategies(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options, List<CircuitAllocation> circuits)
        {
            var remaining = new List<DeviceLoad>(devices);
            
            // Strategy 1: Best-fit for high-load devices
            remaining = ApplyBestFitStrategy(remaining, capacity, options, circuits);
            
            // Strategy 2: First-fit for medium loads
            remaining = ApplyFirstFitStrategy(remaining, capacity, options, circuits);
            
            // Strategy 3: Load balancing for remaining devices
            remaining = ApplyLoadBalancingStrategy(remaining, capacity, options, circuits);
            
            return remaining;
        }

        private List<DeviceLoad> ApplyBestFitStrategy(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options, List<CircuitAllocation> circuits)
        {
            var remaining = new List<DeviceLoad>();
            
            // Focus on high-load devices (top 25% by load score)
            var threshold = devices.Any() ? devices.OrderByDescending(d => d.LoadScore).Take(devices.Count / 4).Min(d => d.LoadScore) : 0;
            var highLoadDevices = devices.Where(d => d.LoadScore >= threshold).OrderByDescending(d => d.LoadScore).ToList();
            var otherDevices = devices.Where(d => d.LoadScore < threshold).ToList();

            foreach (var device in highLoadDevices)
            {
                // Find best-fit circuit (highest utilization that can still fit the device)
                var bestCircuit = circuits
                    .Where(c => c.CanFit(device, capacity))
                    .OrderByDescending(c => c.MaxUtilization(capacity))
                    .FirstOrDefault();

                if (bestCircuit != null)
                {
                    bestCircuit.Devices.Add(device);
                }
                else
                {
                    // Create new circuit
                    var newCircuit = new CircuitAllocation
                    {
                        CircuitId = circuits.Count + 1,
                        PrimaryLevel = device.Level
                    };
                    newCircuit.Devices.Add(device);
                    circuits.Add(newCircuit);
                }
            }

            remaining.AddRange(otherDevices);
            return remaining;
        }

        private List<DeviceLoad> ApplyFirstFitStrategy(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options, List<CircuitAllocation> circuits)
        {
            var remaining = new List<DeviceLoad>();

            foreach (var device in devices.OrderByDescending(d => d.LoadScore))
            {
                var targetCircuit = circuits
                    .Where(c => c.CanFit(device, capacity))
                    .FirstOrDefault();

                if (targetCircuit != null)
                {
                    targetCircuit.Devices.Add(device);
                }
                else
                {
                    // Try to create new circuit
                    if (circuits.Count < 50) // Reasonable circuit limit
                    {
                        var newCircuit = new CircuitAllocation
                        {
                            CircuitId = circuits.Count + 1,
                            PrimaryLevel = device.Level
                        };
                        newCircuit.Devices.Add(device);
                        circuits.Add(newCircuit);
                    }
                    else
                    {
                        remaining.Add(device);
                    }
                }
            }

            return remaining;
        }

        private List<DeviceLoad> ApplyLoadBalancingStrategy(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options, List<CircuitAllocation> circuits)
        {
            var remaining = new List<DeviceLoad>();

            foreach (var device in devices.OrderBy(d => d.LoadScore)) // Start with smaller loads
            {
                // Find circuit with lowest utilization that can fit the device
                var leastLoadedCircuit = circuits
                    .Where(c => c.CanFit(device, capacity))
                    .OrderBy(c => c.MaxUtilization(capacity))
                    .FirstOrDefault();

                if (leastLoadedCircuit != null)
                {
                    leastLoadedCircuit.Devices.Add(device);
                }
                else
                {
                    remaining.Add(device);
                }
            }

            return remaining;
        }

        /// <summary>
        /// First-fit decreasing algorithm for medium-sized problems
        /// </summary>
        private BalancingResult FirstFitDecreasingBalance(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options)
        {
            var result = new BalancingResult();
            var circuits = new List<CircuitAllocation>();
            var unallocated = new List<DeviceLoad>();

            // Sort devices by load score (decreasing)
            var sortedDevices = devices.OrderByDescending(d => d.LoadScore).ToList();

            foreach (var device in sortedDevices)
            {
                bool allocated = false;

                // Try to fit in existing circuits
                foreach (var circuit in circuits)
                {
                    if (circuit.CanFit(device, capacity))
                    {
                        circuit.Devices.Add(device);
                        allocated = true;
                        break;
                    }
                }

                // Create new circuit if needed
                if (!allocated)
                {
                    var newCircuit = new CircuitAllocation
                    {
                        CircuitId = circuits.Count + 1,
                        PrimaryLevel = device.Level
                    };
                    newCircuit.Devices.Add(device);
                    circuits.Add(newCircuit);
                }
            }

            result.Circuits = circuits;
            result.UnallocatedDevices = unallocated.Count;
            return result;
        }

        /// <summary>
        /// Legacy sequential fill algorithm as fallback
        /// </summary>
        private BalancingResult SequentialFillBalance(List<DeviceLoad> devices, CircuitCapacity capacity, BalancingOptions options)
        {
            var result = new BalancingResult();
            var circuits = new List<CircuitAllocation>();
            var currentCircuit = new CircuitAllocation { CircuitId = 1 };
            circuits.Add(currentCircuit);

            foreach (var device in devices)
            {
                if (currentCircuit.CanFit(device, capacity))
                {
                    currentCircuit.Devices.Add(device);
                }
                else
                {
                    // Start new circuit
                    currentCircuit = new CircuitAllocation 
                    { 
                        CircuitId = circuits.Count + 1,
                        PrimaryLevel = device.Level
                    };
                    currentCircuit.Devices.Add(device);
                    circuits.Add(currentCircuit);
                }
            }

            result.Circuits = circuits.Where(c => c.Devices.Any()).ToList();
            return result;
        }

        private List<DeviceLoad> ConvertToDeviceLoads(List<DeviceSnapshot> devices)
        {
            return devices.Select((device, index) => new DeviceLoad
            {
                DeviceId = index + 1,
                DeviceName = $"{device.FamilyName} - {device.TypeName}",
                Current = device.Amps,
                UnitLoads = device.UnitLoads,
                Level = device.LevelName,
                Zone = device.Zone ?? device.LevelName,
                IsHighPriority = device.HasStrobe || device.HasSpeaker,
                SourceDevice = device
            }).ToList();
        }

        private void CalculateBalancingMetrics(BalancingResult result, CircuitCapacity capacity)
        {
            if (!result.Circuits.Any()) return;

            var utilizations = result.Circuits.Select(c => c.MaxUtilization(capacity)).ToList();
            result.AverageUtilization = utilizations.Average();
            
            var variance = utilizations.Select(u => Math.Pow(u - result.AverageUtilization, 2)).Average();
            result.UtilizationVariance = Math.Sqrt(variance);

            // Add warnings for poor balancing
            if (result.UtilizationVariance > 0.2)
            {
                result.Warnings.Add("High utilization variance - circuits are poorly balanced");
            }

            if (result.AverageUtilization < 0.5)
            {
                result.Warnings.Add("Low average utilization - consider consolidating circuits");
            }

            if (utilizations.Any(u => u > 0.95))
            {
                result.Warnings.Add("Some circuits are near capacity limits");
            }
        }

        /// <summary>
        /// Validate and optimize existing circuit allocation
        /// </summary>
        public BalancingResult OptimizeExistingAllocation(List<CircuitAllocation> existingCircuits, CircuitCapacity capacity)
        {
            var result = new BalancingResult();
            
            // Extract all devices from existing circuits
            var allDevices = existingCircuits.SelectMany(c => c.Devices).ToList();
            
            // Re-balance using optimized algorithm
            var options = new BalancingOptions { UseOptimizedBalancing = true };
            var deviceSnapshots = allDevices.Select(d => d.SourceDevice).ToList();
            
            return BalanceDevices(deviceSnapshots, capacity, options);
        }
    }
}