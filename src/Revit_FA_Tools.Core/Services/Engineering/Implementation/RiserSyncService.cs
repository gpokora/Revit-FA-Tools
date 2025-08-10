using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revit_FA_Tools.Models;

namespace Revit_FA_Tools.Services
{
    /// <summary>
    /// Riser diagram synchronization service for maintaining panel/circuit/device relationships
    /// Provides shared data model and bi-directional sync between floor plans and riser diagrams
    /// </summary>
    public class RiserSyncService
    {
        public class RiserSystemLayout
        {
            public string SystemId { get; set; } = Guid.NewGuid().ToString();
            public string SystemName { get; set; } = "Fire Alarm System";
            public DateTime LastModified { get; set; } = DateTime.Now;
            public string ProjectName { get; set; }
            public List<RiserPanel> Panels { get; set; } = new List<RiserPanel>();
            public List<RiserRepeater> Repeaters { get; set; } = new List<RiserRepeater>();
            public RiserSystemMetrics Metrics { get; set; } = new RiserSystemMetrics();
            public List<RiserConnection> InterPanelConnections { get; set; } = new List<RiserConnection>();
            public RiserSyncStatus SyncStatus { get; set; } = new RiserSyncStatus();
        }

        public class RiserPanel
        {
            public string PanelId { get; set; } = Guid.NewGuid().ToString();
            public string PanelName { get; set; }
            public string PanelType { get; set; } // "FACP", "EXPANSION", "REPEATER"
            public string Location { get; set; }
            public List<string> ServedLevels { get; set; } = new List<string>();
            public List<RiserCircuit> Circuits { get; set; } = new List<RiserCircuit>();
            public RiserPowerSupply PowerSupply { get; set; }
            public List<RiserAuxiliaryDevice> AuxiliaryDevices { get; set; } = new List<RiserAuxiliaryDevice>();
            public RiserCapacityStatus CapacityStatus { get; set; } = new RiserCapacityStatus();
            public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
        }

        public class RiserCircuit
        {
            public string CircuitId { get; set; } = Guid.NewGuid().ToString();
            public string CircuitName { get; set; }
            public string CircuitType { get; set; } // "IDNAC", "IDNET", "SIGNALING"
            public int CircuitNumber { get; set; }
            public string PrimaryLevel { get; set; }
            public List<string> ServedZones { get; set; } = new List<string>();
            public List<RiserDevice> Devices { get; set; } = new List<RiserDevice>();
            public RiserCircuitLoads Loads { get; set; } = new RiserCircuitLoads();
            public RiserCircuitWiring Wiring { get; set; } = new RiserCircuitWiring();
            public List<RiserDeviceGroup> DeviceGroups { get; set; } = new List<RiserDeviceGroup>();
            public bool IsActive { get; set; } = true;
        }

        public class RiserDevice
        {
            public string DeviceId { get; set; } = Guid.NewGuid().ToString();
            public int RevitElementId { get; set; }
            public string DeviceName { get; set; }
            public string DeviceType { get; set; }
            public string? FamilyName { get; set; }
            public string TypeName { get; set; }
            public int Address { get; set; }
            public string Level { get; set; }
            public string Zone { get; set; }
            public string Room { get; set; }
            public RiserDeviceLoads Loads { get; set; } = new RiserDeviceLoads();
            public RiserDeviceStatus Status { get; set; } = new RiserDeviceStatus();
            public RiserDeviceLocation Location { get; set; } = new RiserDeviceLocation();
            public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
            public DateTime LastModified { get; set; } = DateTime.Now;
        }

        public class RiserDeviceLoads
        {
            public double AlarmCurrent { get; set; }
            public double StandbyCurrent { get; set; }
            public int UnitLoads { get; set; }
            public double Wattage { get; set; }
            public bool HasStrobe { get; set; }
            public bool HasSpeaker { get; set; }
            public bool IsIsolator { get; set; }
            public bool IsRepeater { get; set; }
        }

        public class RiserDeviceStatus
        {
            public bool IsAssigned { get; set; } = true;
            public bool IsAddressLocked { get; set; } = false;
            public bool HasConflicts { get; set; } = false;
            public List<string> ValidationIssues { get; set; } = new List<string>();
            public string AssignmentSource { get; set; } = "AUTO"; // AUTO, MANUAL, IMPORTED
        }

        public class RiserDeviceLocation
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public string Workset { get; set; }
            public string Phase { get; set; }
            public bool IsPlaced { get; set; } = true;
        }

        public class RiserCircuitLoads
        {
            public double TotalAlarmCurrent { get; set; }
            public double TotalStandbyCurrent { get; set; }
            public int TotalUnitLoads { get; set; }
            public double TotalWattage { get; set; }
            public int DeviceCount { get; set; }
            public double CurrentUtilization { get; set; }
            public double UnitLoadUtilization { get; set; }
            public double DeviceUtilization { get; set; }
            public string LimitingFactor { get; set; } // "CURRENT", "UNIT_LOADS", "DEVICES"
        }

        public class RiserCircuitWiring
        {
            public string WireType { get; set; } = "FPLR";
            public string WireGauge { get; set; } = "12 AWG";
            public double EstimatedLength { get; set; }
            public double VoltageDropPercent { get; set; }
            public bool RequiresRelay { get; set; }
            public List<RiserWiringSegment> Segments { get; set; } = new List<RiserWiringSegment>();
        }

        public class RiserWiringSegment
        {
            public string FromDevice { get; set; }
            public string ToDevice { get; set; }
            public double Length { get; set; }
            public string WireType { get; set; }
            public string RouteType { get; set; } // "CONDUIT", "CABLE_TRAY", "OPEN"
        }

        public class RiserDeviceGroup
        {
            public string GroupId { get; set; } = Guid.NewGuid().ToString();
            public string GroupName { get; set; }
            public string GroupType { get; set; } // "ZONE", "FUNCTION", "LEVEL"
            public List<string> DeviceIds { get; set; } = new List<string>();
            public Dictionary<string, object> GroupProperties { get; set; } = new Dictionary<string, object>();
        }

        public class RiserPowerSupply
        {
            public string PowerSupplyType { get; set; } = "ES-PS";
            public double Capacity { get; set; } = 9.5; // Amps
            public double LoadCurrent { get; set; }
            public double Utilization => Capacity > 0 ? LoadCurrent / Capacity : 0;
            public List<RiserPowerSupplyBranch> Branches { get; set; } = new List<RiserPowerSupplyBranch>();
            public RiserBatteryBank BatteryBank { get; set; } = new RiserBatteryBank();
        }

        public class RiserPowerSupplyBranch
        {
            public int BranchNumber { get; set; }
            public double BranchCurrent { get; set; }
            public List<string> ConnectedCircuits { get; set; } = new List<string>();
            public bool IsActive { get; set; } = true;
        }

        public class RiserBatteryBank
        {
            public string Configuration { get; set; }
            public double CapacityAH { get; set; }
            public int BatteryCount { get; set; }
            public string BatteryType { get; set; }
            public double EstimatedCost { get; set; }
        }

        public class RiserAuxiliaryDevice
        {
            public string DeviceId { get; set; } = Guid.NewGuid().ToString();
            public string DeviceType { get; set; } // "COMMUNICATOR", "RELAY", "INTERFACE"
            public string DeviceName { get; set; }
            public double Current { get; set; }
            public string Location { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        }

        public class RiserRepeater
        {
            public string RepeaterId { get; set; } = Guid.NewGuid().ToString();
            public string RepeaterName { get; set; }
            public string Location { get; set; }
            public List<string> ServedLevels { get; set; } = new List<string>();
            public List<RiserCircuit> Circuits { get; set; } = new List<RiserCircuit>();
            public bool IsEnabled { get; set; } = true;
            public double FreshCapacityCurrent { get; set; } = 3.0;
            public int FreshCapacityUnitLoads { get; set; } = 139;
        }

        public class RiserConnection
        {
            public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
            public string FromPanelId { get; set; }
            public string ToPanelId { get; set; }
            public string ConnectionType { get; set; } // "SLC", "NAC", "NETWORK"
            public List<RiserWiringSegment> Wiring { get; set; } = new List<RiserWiringSegment>();
            public bool IsActive { get; set; } = true;
        }

        public class RiserCapacityStatus
        {
            public int UsedCircuits { get; set; }
            public int MaxCircuits { get; set; }
            public double UsedCurrent { get; set; }
            public double MaxCurrent { get; set; }
            public int UsedDevices { get; set; }
            public int MaxDevices { get; set; }
            public List<string> CapacityWarnings { get; set; } = new List<string>();
        }

        public class RiserSystemMetrics
        {
            public int TotalPanels { get; set; }
            public int TotalCircuits { get; set; }
            public int TotalDevices { get; set; }
            public double TotalSystemCurrent { get; set; }
            public int TotalUnitLoads { get; set; }
            public double AverageCircuitUtilization { get; set; }
            public List<string> SystemWarnings { get; set; } = new List<string>();
            public DateTime LastCalculated { get; set; } = DateTime.Now;
        }

        public class RiserSyncStatus
        {
            public DateTime LastFloorPlanSync { get; set; }
            public DateTime LastRiserSync { get; set; }
            public List<RiserSyncConflict> Conflicts { get; set; } = new List<RiserSyncConflict>();
            public bool HasPendingChanges { get; set; }
            public string SyncMode { get; set; } = "BIDIRECTIONAL"; // BIDIRECTIONAL, FLOOR_TO_RISER, RISER_TO_FLOOR
        }

        public class RiserSyncConflict
        {
            public string ConflictId { get; set; } = Guid.NewGuid().ToString();
            public string ConflictType { get; set; } // "DEVICE_MOVED", "CIRCUIT_CHANGED", "ADDRESS_CONFLICT"
            public string Description { get; set; }
            public string FloorPlanValue { get; set; }
            public string RiserValue { get; set; }
            public string RecommendedAction { get; set; }
            public DateTime DetectedAt { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Create riser system layout from device snapshots
        /// </summary>
        public async Task<RiserSystemLayout> CreateSystemLayoutFromDevices(List<DeviceSnapshot> devices, string? projectName = null)
        {
            return await Task.Run(() =>
            {
                var layout = new RiserSystemLayout
                {
                    ProjectName = projectName ?? "Fire Alarm System",
                    LastModified = DateTime.Now
                };

                // Group devices by level for panel assignment
                var devicesByLevel = devices.GroupBy(d => d.LevelName).ToList();

                // Create panels based on system size and device distribution
                var panelStrategy = DeterminePanelStrategy(devices.Count, devicesByLevel.Count());
                CreatePanelsForLayout(layout, devicesByLevel, panelStrategy);

                // Assign devices to circuits with optimization
                AssignDevicesToCircuits(layout, devices);

                // Calculate system metrics
                CalculateSystemMetrics(layout);

                return layout;
            });
        }

        /// <summary>
        /// Sync changes from floor plan to riser diagram
        /// </summary>
        public async Task<RiserSyncResult> SyncFromFloorPlan(RiserSystemLayout currentLayout, List<DeviceSnapshot> updatedDevices)
        {
            var result = new RiserSyncResult();

            try
            {
                // Detect changes in device locations, assignments, and properties
                var changes = await DetectFloorPlanChanges(currentLayout, updatedDevices);
                
                // Apply changes to riser layout
                await ApplyFloorPlanChanges(currentLayout, changes);

                // Recalculate affected circuits and panels
                await RecalculateAffectedElements(currentLayout, changes);

                // Update sync status
                currentLayout.SyncStatus.LastFloorPlanSync = DateTime.Now;
                currentLayout.LastModified = DateTime.Now;

                result.Success = true;
                result.Message = $"Floor plan sync completed: {changes.Count} changes applied";
                result.ChangesApplied = changes.Count;
                result.UpdatedLayout = currentLayout;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Floor plan sync failed: {ex.Message}";
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        /// <summary>
        /// Sync changes from riser diagram to floor plan assignments
        /// </summary>
        public async Task<RiserSyncResult> SyncToFloorPlan(RiserSystemLayout riserLayout)
        {
            var result = new RiserSyncResult();

            try
            {
                // Generate floor plan assignments from riser layout
                var assignments = await GenerateFloorPlanAssignments(riserLayout);

                // Validate assignments against floor plan constraints
                var validationResult = await ValidateFloorPlanAssignments(assignments);

                if (validationResult.IsValid)
                {
                    // Apply assignments (this would integrate with Revit API in real implementation)
                    await ApplyFloorPlanAssignments(assignments);

                    riserLayout.SyncStatus.LastRiserSync = DateTime.Now;
                    result.Success = true;
                    result.Message = $"Riser to floor plan sync completed: {assignments.Count} devices updated";
                }
                else
                {
                    result.Success = false;
                    result.Message = "Riser to floor plan sync failed validation";
                    result.Errors.AddRange(validationResult.Errors);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Riser sync failed: {ex.Message}";
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        private string DeterminePanelStrategy(int deviceCount, int levelCount)
        {
            if (deviceCount <= 127 && levelCount <= 3)
                return "SINGLE_PANEL";
            else if (deviceCount <= 500 && levelCount <= 10)
                return "MAIN_WITH_REPEATERS";
            else
                return "DISTRIBUTED_PANELS";
        }

        private void CreatePanelsForLayout(RiserSystemLayout layout, IEnumerable<IGrouping<string, DeviceSnapshot>> devicesByLevel, string strategy)
        {
            switch (strategy)
            {
                case "SINGLE_PANEL":
                    var mainPanel = new RiserPanel
                    {
                        PanelName = "FACP-1",
                        PanelType = "FACP",
                        Location = "Main Electrical Room",
                        ServedLevels = devicesByLevel.Select(g => g.Key).ToList()
                    };
                    layout.Panels.Add(mainPanel);
                    break;

                case "MAIN_WITH_REPEATERS":
                    // Create main panel
                    var facpPanel = new RiserPanel
                    {
                        PanelName = "FACP-1",
                        PanelType = "FACP",
                        Location = "Main Electrical Room",
                        ServedLevels = devicesByLevel.Take(3).Select(g => g.Key).ToList()
                    };
                    layout.Panels.Add(facpPanel);

                    // Create repeaters for additional levels
                    var additionalLevels = devicesByLevel.Skip(3).ToList();
                    for (int i = 0; i < additionalLevels.Count; i++)
                    {
                        var repeater = new RiserRepeater
                        {
                            RepeaterName = $"REP-{i + 1}",
                            Location = $"Level {additionalLevels[i].Key}",
                            ServedLevels = new List<string> { additionalLevels[i].Key }
                        };
                        layout.Repeaters.Add(repeater);
                    }
                    break;

                case "DISTRIBUTED_PANELS":
                    // Create multiple panels based on geographic distribution
                    var panelCount = Math.Min(devicesByLevel.Count(), 6); // Max 6 panels
                    var levelsPerPanel = Math.Ceiling((double)devicesByLevel.Count() / panelCount);

                    var levelGroups = devicesByLevel.Select((level, index) => new { level, index })
                        .GroupBy(x => x.index / (int)levelsPerPanel)
                        .ToList();

                    for (int p = 0; p < levelGroups.Count; p++)
                    {
                        var panel = new RiserPanel
                        {
                            PanelName = p == 0 ? "FACP-1" : $"EXP-{p}",
                            PanelType = p == 0 ? "FACP" : "EXPANSION",
                            Location = p == 0 ? "Main Electrical Room" : $"Level {levelGroups[p].First().level.Key}",
                            ServedLevels = levelGroups[p].Select(x => x.level.Key).ToList()
                        };
                        layout.Panels.Add(panel);
                    }
                    break;
            }
        }

        private void AssignDevicesToCircuits(RiserSystemLayout layout, List<DeviceSnapshot> devices)
        {
            // Use circuit balancer for optimal assignment
            var balancer = new CircuitBalancer();
            var capacity = new CircuitBalancer.CircuitCapacity();
            var options = new CircuitBalancer.BalancingOptions { UseOptimizedBalancing = true };

            foreach (var panel in layout.Panels)
            {
                var panelDevices = devices.Where(d => panel.ServedLevels.Contains(d.LevelName)).ToList();
                if (!panelDevices.Any()) continue;

                var balancingResult = balancer.BalanceDevices(panelDevices, capacity, options);

                foreach (var circuitAllocation in balancingResult.Circuits)
                {
                    var riserCircuit = new RiserCircuit
                    {
                        CircuitName = $"IDNAC-{circuitAllocation.CircuitId}",
                        CircuitType = "IDNAC",
                        CircuitNumber = circuitAllocation.CircuitId,
                        PrimaryLevel = circuitAllocation.PrimaryLevel
                    };

                    foreach (var deviceLoad in circuitAllocation.Devices)
                    {
                        var riserDevice = ConvertToRiserDevice(deviceLoad.SourceDevice);
                        riserCircuit.Devices.Add(riserDevice);
                    }

                    // Calculate circuit loads
                    riserCircuit.Loads = new RiserCircuitLoads
                    {
                        TotalAlarmCurrent = circuitAllocation.TotalCurrent,
                        TotalUnitLoads = circuitAllocation.TotalUnitLoads,
                        DeviceCount = circuitAllocation.DeviceCount,
                        CurrentUtilization = circuitAllocation.CurrentUtilization(capacity),
                        UnitLoadUtilization = circuitAllocation.UnitLoadUtilization(capacity)
                    };

                    panel.Circuits.Add(riserCircuit);
                }
            }
        }

        private RiserDevice ConvertToRiserDevice(DeviceSnapshot snapshot)
        {
            return new RiserDevice
            {
                RevitElementId = snapshot.ElementId,
                DeviceName = $"{snapshot.FamilyName} - {snapshot.TypeName}",
                DeviceType = DetermineDeviceType(snapshot),
                FamilyName = snapshot.FamilyName,
                TypeName = snapshot.TypeName,
                Level = snapshot.LevelName,
                Zone = snapshot.Zone ?? snapshot.LevelName,
                Loads = new RiserDeviceLoads
                {
                    AlarmCurrent = snapshot.Amps,
                    UnitLoads = snapshot.UnitLoads,
                    Wattage = snapshot.Watts,
                    HasStrobe = snapshot.HasStrobe,
                    HasSpeaker = snapshot.HasSpeaker,
                    IsIsolator = snapshot.IsIsolator,
                    IsRepeater = snapshot.IsRepeater
                }
            };
        }

        private string DetermineDeviceType(DeviceSnapshot snapshot)
        {
            if (snapshot.HasSpeaker && snapshot.HasStrobe) return "SPEAKER_STROBE";
            if (snapshot.HasSpeaker) return "SPEAKER";
            if (snapshot.HasStrobe) return "STROBE";
            if (snapshot.IsIsolator) return "ISOLATOR";
            if (snapshot.IsRepeater) return "REPEATER";
            
            return "NOTIFICATION_DEVICE";
        }

        private void CalculateSystemMetrics(RiserSystemLayout layout)
        {
            layout.Metrics = new RiserSystemMetrics
            {
                TotalPanels = layout.Panels.Count + layout.Repeaters.Count,
                TotalCircuits = layout.Panels.Sum(p => p.Circuits.Count) + layout.Repeaters.Sum(r => r.Circuits.Count),
                TotalDevices = layout.Panels.SelectMany(p => p.Circuits).Sum(c => c.Devices.Count) +
                              layout.Repeaters.SelectMany(r => r.Circuits).Sum(c => c.Devices.Count),
                TotalSystemCurrent = layout.Panels.SelectMany(p => p.Circuits).Sum(c => c.Loads.TotalAlarmCurrent) +
                                   layout.Repeaters.SelectMany(r => r.Circuits).Sum(c => c.Loads.TotalAlarmCurrent),
                TotalUnitLoads = layout.Panels.SelectMany(p => p.Circuits).Sum(c => c.Loads.TotalUnitLoads) +
                               layout.Repeaters.SelectMany(r => r.Circuits).Sum(c => c.Loads.TotalUnitLoads),
                LastCalculated = DateTime.Now
            };

            var allCircuits = layout.Panels.SelectMany(p => p.Circuits).Concat(layout.Repeaters.SelectMany(r => r.Circuits)).ToList();
            if (allCircuits.Any())
            {
                layout.Metrics.AverageCircuitUtilization = allCircuits.Average(c => Math.Max(c.Loads.CurrentUtilization, c.Loads.UnitLoadUtilization));
            }
        }

        private async Task<List<FloorPlanChange>> DetectFloorPlanChanges(RiserSystemLayout currentLayout, List<DeviceSnapshot> updatedDevices)
        {
            // Implementation would detect differences between current layout and updated device snapshots
            // For now, return empty list as placeholder
            return await Task.FromResult(new List<FloorPlanChange>());
        }

        private async Task ApplyFloorPlanChanges(RiserSystemLayout layout, List<FloorPlanChange> changes)
        {
            // Implementation would apply detected changes to the riser layout
            await Task.CompletedTask;
        }

        private async Task RecalculateAffectedElements(RiserSystemLayout layout, List<FloorPlanChange> changes)
        {
            // Implementation would recalculate circuits and panels affected by changes
            await Task.CompletedTask;
        }

        private async Task<List<FloorPlanAssignment>> GenerateFloorPlanAssignments(RiserSystemLayout riserLayout)
        {
            // Implementation would generate device assignments for floor plan from riser layout
            return await Task.FromResult(new List<FloorPlanAssignment>());
        }

        private async Task<ValidationResult> ValidateFloorPlanAssignments(List<FloorPlanAssignment> assignments)
        {
            // Implementation would validate assignments against floor plan constraints
            return await Task.FromResult(new ValidationResult { IsValid = true });
        }

        private async Task ApplyFloorPlanAssignments(List<FloorPlanAssignment> assignments)
        {
            // Implementation would apply assignments to floor plan (Revit integration)
            await Task.CompletedTask;
        }

        public class RiserSyncResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int ChangesApplied { get; set; }
            public RiserSystemLayout UpdatedLayout { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        public class FloorPlanChange
        {
            public string ChangeType { get; set; }
            public string DeviceId { get; set; }
            public object OldValue { get; set; }
            public object NewValue { get; set; }
        }

        public class FloorPlanAssignment
        {
            public int ElementId { get; set; }
            public string PanelId { get; set; }
            public string CircuitId { get; set; }
            public int Address { get; set; }
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }
    }
}