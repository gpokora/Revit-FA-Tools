using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools.Models;
using ValidationSeverity = Revit_FA_Tools.Core.Services.Interfaces.ValidationSeverity;

namespace Revit_FA_Tools.Core.Services.Interfaces
{
    /// <summary>
    /// Unified interface for device addressing operations
    /// </summary>
    public interface IAddressingService
    {
        /// <summary>
        /// Assigns addresses to a collection of devices
        /// </summary>
        Task<AddressingResult> AssignAddressesAsync(IEnumerable<SmartDeviceNode> devices, AddressingOptions options);

        /// <summary>
        /// Validates addressing for a circuit
        /// </summary>
        Task<AddressingValidationResult> ValidateAddressingAsync(AddressingCircuit circuit);

        /// <summary>
        /// Gets available addresses for a circuit
        /// </summary>
        IEnumerable<int> GetAvailableAddresses(AddressingCircuit circuit);

        /// <summary>
        /// Releases an address back to the pool
        /// </summary>
        void ReleaseAddress(AddressingCircuit circuit, int address);

        /// <summary>
        /// Reserves an address from the pool
        /// </summary>
        bool ReserveAddress(AddressingCircuit circuit, int address);

        /// <summary>
        /// Auto-assigns addresses to all unaddressed devices
        /// </summary>
        Task<AddressingResult> AutoAssignAsync(AddressingCircuit circuit, AutoAssignOptions options);

        /// <summary>
        /// Clears all address assignments for a circuit
        /// </summary>
        void ClearAddresses(AddressingCircuit circuit);

        /// <summary>
        /// Gets the current address allocation status
        /// </summary>
        AddressAllocationStatus GetAllocationStatus(AddressingCircuit circuit);
    }

    /// <summary>
    /// Result of addressing operations
    /// </summary>
    public class AddressingResult
    {
        public bool Success { get; set; }
        public int DevicesAddressed { get; set; }
        public int DevicesSkipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Options for addressing operations
    /// </summary>
    public class AddressingOptions
    {
        public bool RespectLocks { get; set; } = true;
        public bool OverwriteExisting { get; set; } = false;
        public bool ValidateElectrical { get; set; } = true;
        public int StartAddress { get; set; } = 1;
    }

    /// <summary>
    /// Options for auto-assignment
    /// </summary>
    public class AutoAssignOptions : AddressingOptions
    {
        public AddressingStrategy Strategy { get; set; } = AddressingStrategy.Sequential;
        public bool OptimizeByLocation { get; set; } = false;
        public bool GroupByDeviceType { get; set; } = false;
    }

    /// <summary>
    /// Addressing strategies
    /// </summary>
    public enum AddressingStrategy
    {
        Sequential,
        ByFloor,
        ByZone,
        ByDeviceType,
        Optimized
    }

    /// <summary>
    /// Address allocation status
    /// </summary>
    public class AddressAllocationStatus
    {
        public int TotalAddresses { get; set; }
        public int UsedAddresses { get; set; }
        public int AvailableAddresses { get; set; }
        public double UtilizationPercentage { get; set; }
        public List<int> AllocatedAddresses { get; set; } = new List<int>();
        public List<int> AvailableAddressList { get; set; } = new List<int>();
    }

    /// <summary>
    /// Validation result for addressing
    /// </summary>
    public class AddressingValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
        public ValidationSeverity HighestSeverity { get; set; } = ValidationSeverity.None;
    }

    /// <summary>
    /// Validation issue
    /// </summary>
    public class ValidationIssue
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string DeviceId { get; set; }
        public string CircuitId { get; set; }
    }
}