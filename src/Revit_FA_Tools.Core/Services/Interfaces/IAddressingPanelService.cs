using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Core.Models.Devices;

namespace Revit_FA_Tools.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for addressing panel business logic
    /// </summary>
    public interface IAddressingPanelService
    {
        /// <summary>
        /// Initializes the addressing panel with devices
        /// </summary>
        Task<AddressingPanelData> InitializePanelAsync(IEnumerable<DeviceSnapshot> devices);

        /// <summary>
        /// Processes device assignment to a circuit
        /// </summary>
        Task<AssignmentResult> AssignDeviceToCircuitAsync(string deviceId, string circuitId, AssignmentOptions options);

        /// <summary>
        /// Removes device from circuit
        /// </summary>
        Task<bool> RemoveDeviceFromCircuitAsync(string deviceId, string circuitId);

        /// <summary>
        /// Updates device address
        /// </summary>
        Task<UpdateResult> UpdateDeviceAddressAsync(string deviceId, string newAddress, bool validateFirst = true);

        /// <summary>
        /// Auto-assigns addresses for all unaddressed devices
        /// </summary>
        Task<AutoAssignmentResult> AutoAssignAllAsync(AutoAssignmentOptions options);

        /// <summary>
        /// Validates current panel configuration
        /// </summary>
        Task<PanelValidationResult> ValidatePanelAsync();

        /// <summary>
        /// Gets circuit utilization information
        /// </summary>
        Task<CircuitUtilization> GetCircuitUtilizationAsync(string circuitId);

        /// <summary>
        /// Balances devices across circuits
        /// </summary>
        Task<BalancingResult> BalanceCircuitsAsync(BalancingOptions options);

        /// <summary>
        /// Exports addressing data
        /// </summary>
        Task<byte[]> ExportAddressingDataAsync(ExportFormat format);

        /// <summary>
        /// Imports addressing data
        /// </summary>
        Task<ImportResult> ImportAddressingDataAsync(byte[] data, ImportOptions options);

        /// <summary>
        /// Gets addressing statistics
        /// </summary>
        Task<AddressingStatistics> GetStatisticsAsync();

        /// <summary>
        /// Applies pending changes to the model
        /// </summary>
        Task<ApplyChangesResult> ApplyChangesAsync();

        /// <summary>
        /// Reverts all pending changes
        /// </summary>
        Task RevertChangesAsync();
    }

    /// <summary>
    /// Addressing panel data
    /// </summary>
    public class AddressingPanelData
    {
        public List<AddressingPanel> Panels { get; set; } = new List<AddressingPanel>();
        public List<SmartDeviceNode> UnassignedDevices { get; set; } = new List<SmartDeviceNode>();
        public int TotalDevices { get; set; }
        public int AddressedDevices { get; set; }
        public int UnaddressedDevices { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Assignment result
    /// </summary>
    public class AssignmentResult
    {
        public bool Success { get; set; }
        public string DeviceId { get; set; }
        public string CircuitId { get; set; }
        public string AssignedAddress { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Assignment options
    /// </summary>
    public class AssignmentOptions
    {
        public bool AutoAssignAddress { get; set; } = true;
        public bool ValidateElectrical { get; set; } = true;
        public bool ValidateCapacity { get; set; } = true;
        public bool PreserveExistingAddress { get; set; } = false;
    }

    /// <summary>
    /// Update result
    /// </summary>
    public class UpdateResult
    {
        public bool Success { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ErrorMessage { get; set; }
        public ValidationResult ValidationResult { get; set; }
    }

    /// <summary>
    /// Auto-assignment result
    /// </summary>
    public class AutoAssignmentResult
    {
        public bool Success { get; set; }
        public int DevicesProcessed { get; set; }
        public int DevicesAssigned { get; set; }
        public int DevicesSkipped { get; set; }
        public int DevicesFailed { get; set; }
        public List<AssignmentResult> Results { get; set; } = new List<AssignmentResult>();
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Auto-assignment options
    /// </summary>
    public class AutoAssignmentOptions
    {
        public bool RespectLocks { get; set; } = true;
        public bool OptimizeByLocation { get; set; } = true;
        public bool GroupByDeviceType { get; set; } = false;
        public AssignmentStrategy Strategy { get; set; } = AssignmentStrategy.Sequential;
        public int StartAddress { get; set; } = 1;
        public bool ValidateElectrical { get; set; } = true;
    }

    /// <summary>
    /// Assignment strategy
    /// </summary>
    public enum AssignmentStrategy
    {
        Sequential,
        ByFloor,
        ByZone,
        ByDeviceType,
        Optimized
    }

    /// <summary>
    /// Panel validation result
    /// </summary>
    public class PanelValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationMessage> Messages { get; set; } = new List<ValidationMessage>();
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
    }

    /// <summary>
    /// Circuit utilization
    /// </summary>
    public class CircuitUtilization
    {
        public string CircuitId { get; set; }
        public int DeviceCount { get; set; }
        public int MaxDevices { get; set; }
        public double DeviceUtilization { get; set; }
        public double CurrentDraw { get; set; }
        public double MaxCurrent { get; set; }
        public double CurrentUtilization { get; set; }
        public int UsedAddresses { get; set; }
        public int AvailableAddresses { get; set; }
        public double AddressUtilization { get; set; }
    }

    /// <summary>
    /// Circuit balancing result
    /// </summary>
    public class BalancingResult
    {
        public bool Success { get; set; }
        public int DevicesMoved { get; set; }
        public double ImbalanceBefore { get; set; }
        public double ImbalanceAfter { get; set; }
        public List<DeviceMove> Moves { get; set; } = new List<DeviceMove>();
    }

    /// <summary>
    /// Device move operation
    /// </summary>
    public class DeviceMove
    {
        public string DeviceId { get; set; }
        public string FromCircuit { get; set; }
        public string ToCircuit { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Balancing options
    /// </summary>
    public class BalancingOptions
    {
        public double TargetUtilization { get; set; } = 0.8;
        public bool MaintainLocationGrouping { get; set; } = true;
        public bool MinimizeMoves { get; set; } = true;
    }

    /// <summary>
    /// Export format
    /// </summary>
    public enum ExportFormat
    {
        CSV,
        JSON,
        Excel,
        XML
    }

    /// <summary>
    /// Import result
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsImported { get; set; }
        public int RecordsFailed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Import options
    /// </summary>
    public class ImportOptions
    {
        public bool OverwriteExisting { get; set; } = false;
        public bool ValidateBeforeImport { get; set; } = true;
        public bool CreateMissingCircuits { get; set; } = false;
    }

    /// <summary>
    /// Addressing statistics
    /// </summary>
    public class AddressingStatistics
    {
        public int TotalPanels { get; set; }
        public int TotalCircuits { get; set; }
        public int TotalDevices { get; set; }
        public int AddressedDevices { get; set; }
        public int UnaddressedDevices { get; set; }
        public double AverageCircuitUtilization { get; set; }
        public double AverageCurrentDraw { get; set; }
        public Dictionary<string, int> DevicesByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DevicesByFloor { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Apply changes result
    /// </summary>
    public class ApplyChangesResult
    {
        public bool Success { get; set; }
        public int ChangesApplied { get; set; }
        public int ChangesFailed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan ProcessingTime { get; set; }
    }
}