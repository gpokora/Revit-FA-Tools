using System;
using System.Collections.Generic;
using System.Linq;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Voltage drop calculator service for fire alarm circuits
    /// Implements standard voltage drop calculations per fire alarm specifications
    /// </summary>
    public class VoltageDropCalculator
    {
        /// <summary>
        /// Standard wire resistances (ohms per 1000 feet) at 75°C for common fire alarm wire gauges
        /// </summary>
        private static readonly Dictionary<string, double> WireResistances = new Dictionary<string, double>
        {
            ["18 AWG"] = 6.385,   // 18 AWG solid copper
            ["16 AWG"] = 4.016,   // 16 AWG solid copper  
            ["14 AWG"] = 2.525,   // 14 AWG solid copper
            ["12 AWG"] = 1.588,   // 12 AWG solid copper
            ["10 AWG"] = 0.999,   // 10 AWG solid copper
            ["8 AWG"] = 0.628,    // 8 AWG stranded copper
            ["6 AWG"] = 0.395,    // 6 AWG stranded copper
            ["4 AWG"] = 0.249,    // 4 AWG stranded copper
            ["2 AWG"] = 0.156,    // 2 AWG stranded copper
            ["1/0 AWG"] = 0.098   // 1/0 AWG stranded copper
        };

        /// <summary>
        /// Calculate voltage drop for a circuit segment
        /// </summary>
        /// <param name="current">Current in circuit (Amps)</param>
        /// <param name="wireLength">One-way wire length in feet</param>
        /// <param name="wireGauge">Wire gauge specification</param>
        /// <param name="systemVoltage">System voltage (typically 24V DC)</param>
        /// <returns>Voltage drop calculation result</returns>
        public VoltageDropResult CalculateVoltageDrop(double current, double wireLength, string wireGauge, double systemVoltage = 24.0)
        {
            try
            {
                if (current <= 0 || wireLength <= 0 || systemVoltage <= 0)
                {
                    return new VoltageDropResult(0, 0);
                }

                // Get wire resistance
                var resistance = GetWireResistance(wireGauge);
                if (resistance <= 0)
                {
                    throw new ArgumentException($"Unknown or invalid wire gauge: {wireGauge}");
                }

                // Calculate total circuit resistance (round trip)
                var totalResistance = (resistance * wireLength * 2) / 1000.0; // 2-way path, convert to actual feet

                // Calculate voltage drop
                var voltageDrop = current * totalResistance;
                var voltageDropPercent = (voltageDrop / systemVoltage) * 100.0;

                return new VoltageDropResult(voltageDrop, voltageDropPercent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error calculating voltage drop: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calculate voltage drop for multiple circuit segments in series
        /// </summary>
        public VoltageDropResult CalculateSeriesVoltageDrop(List<CircuitSegment> segments, double systemVoltage = 24.0)
        {
            if (segments == null || !segments.Any())
            {
                return new VoltageDropResult(0, 0);
            }

            var totalVoltageDrop = 0.0;

            foreach (var segment in segments)
            {
                var segmentResult = CalculateVoltageDrop(segment.Current, segment.WireLength, segment.WireGauge, systemVoltage);
                totalVoltageDrop += segmentResult.Vdrop;
            }

            var totalPercent = (totalVoltageDrop / systemVoltage) * 100.0;
            return new VoltageDropResult(totalVoltageDrop, totalPercent);
        }

        /// <summary>
        /// Calculate maximum allowable wire length for a given current and voltage drop limit
        /// </summary>
        public double CalculateMaxWireLength(double current, string wireGauge, double maxVoltageDropPercent = 10.0, double systemVoltage = 24.0)
        {
            try
            {
                if (current <= 0 || maxVoltageDropPercent <= 0 || systemVoltage <= 0)
                {
                    return 0;
                }

                var resistance = GetWireResistance(wireGauge);
                if (resistance <= 0)
                {
                    return 0;
                }

                // Calculate maximum allowable voltage drop
                var maxVoltageDrop = (maxVoltageDropPercent / 100.0) * systemVoltage;

                // Solve for wire length: Vdrop = I * R * (2L/1000)
                // L = (Vdrop * 1000) / (I * R * 2)
                var maxLength = (maxVoltageDrop * 1000.0) / (current * resistance * 2.0);

                return Math.Max(0, maxLength);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Recommend optimal wire gauge for a circuit
        /// </summary>
        public WireGaugeRecommendation RecommendWireGauge(double current, double wireLength, double maxVoltageDropPercent = 10.0, double systemVoltage = 24.0)
        {
            var recommendations = new List<(string gauge, VoltageDropResult result)>();

            // Test common fire alarm wire gauges
            var commonGauges = new[] { "18 AWG", "16 AWG", "14 AWG", "12 AWG", "10 AWG", "8 AWG" };

            foreach (var gauge in commonGauges)
            {
                var result = CalculateVoltageDrop(current, wireLength, gauge, systemVoltage);
                recommendations.Add((gauge, result));

                // Stop at first gauge that meets requirements
                if (result.Percent <= maxVoltageDropPercent)
                {
                    break;
                }
            }

            var bestGauge = recommendations.FirstOrDefault(r => r.result.Percent <= maxVoltageDropPercent);
            
            return new WireGaugeRecommendation
            {
                RecommendedGauge = bestGauge.gauge ?? "Larger than 8 AWG required",
                VoltageDrop = bestGauge.result ?? new VoltageDropResult(999, 999),
                MeetsRequirement = bestGauge.gauge != null,
                MaxAllowableLength = bestGauge.gauge != null ? 
                    CalculateMaxWireLength(current, bestGauge.gauge, maxVoltageDropPercent, systemVoltage) : 0,
                AlternativeGauges = recommendations.Where(r => r.gauge != bestGauge.gauge)
                    .ToDictionary(r => r.gauge, r => r.result)
            };
        }

        /// <summary>
        /// Analyze voltage drop for an entire IDNAC circuit with multiple devices
        /// </summary>
        public CircuitVoltageAnalysis AnalyzeIDNACCircuit(List<DeviceSnapshot> devices, string wireGauge, double systemVoltage = 24.0)
        {
            if (devices == null || !devices.Any())
            {
                return new CircuitVoltageAnalysis
                {
                    IsValid = false,
                    Message = "No devices provided for analysis"
                };
            }

            var config = ConfigurationService.Current;
            var totalCurrent = devices.Sum(d => d.Amps);
            var maxVoltageDropPercent = config.Capacity.MaxVoltageDropPct * 100;

            // Estimate circuit layout (simplified - assumes devices spread along circuit)
            var deviceCount = devices.Count;
            var estimatedTotalLength = EstimateCircuitLength(deviceCount);
            var averageDeviceDistance = estimatedTotalLength / Math.Max(1, deviceCount);

            var results = new List<DeviceVoltageResult>();
            var runningCurrent = totalCurrent;

            // Calculate voltage drop to each device (simplified end-to-end analysis)
            foreach (var device in devices.OrderBy(d => d.ElementId)) // Simplified ordering
            {
                var voltageDropToDevice = CalculateVoltageDrop(runningCurrent, averageDeviceDistance, wireGauge, systemVoltage);
                
                results.Add(new DeviceVoltageResult
                {
                    DeviceId = device.ElementId,
                    DeviceName = $"{device.FamilyName} - {device.TypeName}",
                    Current = device.Amps,
                    EstimatedDistance = averageDeviceDistance,
                    VoltageDropToDevice = voltageDropToDevice,
                    VoltageAtDevice = systemVoltage - voltageDropToDevice.Vdrop
                });

                // Reduce current for next device (simplified)
                runningCurrent -= device.Amps;
            }

            var maxVoltageDrop = results.Max(r => r.VoltageDropToDevice.Vdrop);
            var maxVoltageDropPercent_Actual = results.Max(r => r.VoltageDropToDevice.Percent);
            var minVoltageAtDevice = results.Min(r => r.VoltageAtDevice);

            return new CircuitVoltageAnalysis
            {
                TotalCurrent = totalCurrent,
                WireGauge = wireGauge,
                EstimatedCircuitLength = estimatedTotalLength,
                MaxVoltageDrop = maxVoltageDrop,
                MaxVoltageDropPercent = maxVoltageDropPercent_Actual,
                MinVoltageAtDevice = minVoltageAtDevice,
                MeetsVoltageDropRequirement = maxVoltageDropPercent_Actual <= maxVoltageDropPercent,
                IsValid = true,
                Message = $"Circuit analysis complete: {deviceCount} devices, {maxVoltageDropPercent_Actual:F1}% max voltage drop",
                DeviceResults = results,
                WireGaugeRecommendation = RecommendWireGauge(totalCurrent, estimatedTotalLength, maxVoltageDropPercent, systemVoltage)
            };
        }

        /// <summary>
        /// Get wire resistance for specified gauge
        /// </summary>
        private double GetWireResistance(string wireGauge)
        {
            if (string.IsNullOrEmpty(wireGauge))
            {
                return WireResistances["16 AWG"]; // Default to 16 AWG
            }

            var normalizedGauge = wireGauge.ToUpper().Trim();
            
            // Handle common variations
            if (normalizedGauge.Contains("18"))
                return WireResistances["18 AWG"];
            if (normalizedGauge.Contains("16"))
                return WireResistances["16 AWG"];
            if (normalizedGauge.Contains("14"))
                return WireResistances["14 AWG"];
            if (normalizedGauge.Contains("12"))
                return WireResistances["12 AWG"];
            if (normalizedGauge.Contains("10"))
                return WireResistances["10 AWG"];
            if (normalizedGauge.Contains("8"))
                return WireResistances["8 AWG"];

            // Try direct lookup
            if (WireResistances.ContainsKey(wireGauge))
            {
                return WireResistances[wireGauge];
            }

            // Default to 16 AWG if unknown
            return WireResistances["16 AWG"];
        }

        /// <summary>
        /// Estimate circuit length based on device count (simplified heuristic)
        /// </summary>
        private double EstimateCircuitLength(int deviceCount)
        {
            // Simple heuristic: assume devices are spaced ~50 feet apart on average
            // Real implementation would use actual device locations from Revit model
            var baseLength = 100; // Base circuit length
            var deviceSpacing = 50; // Average spacing between devices
            
            return baseLength + (deviceCount * deviceSpacing);
        }
    }

    /// <summary>
    /// Circuit segment for voltage drop calculations
    /// </summary>
    public class CircuitSegment
    {
        public double Current { get; set; }
        public double WireLength { get; set; }
        public string WireGauge { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Wire gauge recommendation result
    /// </summary>
    public class WireGaugeRecommendation
    {
        public string RecommendedGauge { get; set; } = string.Empty;
        public VoltageDropResult VoltageDrop { get; set; } = new VoltageDropResult(0, 0);
        public bool MeetsRequirement { get; set; }
        public double MaxAllowableLength { get; set; }
        public Dictionary<string, VoltageDropResult> AlternativeGauges { get; set; } = new Dictionary<string, VoltageDropResult>();
    }

    /// <summary>
    /// Complete circuit voltage analysis result
    /// </summary>
    public class CircuitVoltageAnalysis
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public double TotalCurrent { get; set; }
        public string WireGauge { get; set; } = string.Empty;
        public double EstimatedCircuitLength { get; set; }
        public double MaxVoltageDrop { get; set; }
        public double MaxVoltageDropPercent { get; set; }
        public double MinVoltageAtDevice { get; set; }
        public bool MeetsVoltageDropRequirement { get; set; }
        public List<DeviceVoltageResult> DeviceResults { get; set; } = new List<DeviceVoltageResult>();
        public WireGaugeRecommendation WireGaugeRecommendation { get; set; }
    }

    /// <summary>
    /// Voltage drop result for individual device
    /// </summary>
    public class DeviceVoltageResult
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public double Current { get; set; }
        public double EstimatedDistance { get; set; }
        public VoltageDropResult VoltageDropToDevice { get; set; } = new VoltageDropResult(0, 0);
        public double VoltageAtDevice { get; set; }
    }
}