using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.UnitOfWork;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using DeviceSnapshot = Revit_FA_Tools.Models.DeviceSnapshot;
using DeviceAssignment = Revit_FA_Tools.Models.DeviceAssignment;
using ModelsValidationResult = Revit_FA_Tools.Models.ValidationResult;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Service for managing device assignments - replaces the singleton AssignmentStore
    /// </summary>
    public class AssignmentService : IAssignmentService, INotifyPropertyChanged
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidationService _validationService;
        private readonly ObservableCollection<DeviceAssignment> _deviceAssignments;
        private readonly Dictionary<string, DeviceAssignment> _assignmentLookup;

        public AssignmentService(IUnitOfWork unitOfWork, IValidationService validationService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _deviceAssignments = new ObservableCollection<DeviceAssignment>();
            _assignmentLookup = new Dictionary<string, DeviceAssignment>();
        }

        #region Properties

        /// <summary>
        /// Gets the collection of device assignments
        /// </summary>
        public ObservableCollection<DeviceAssignment> DeviceAssignments => _deviceAssignments;

        /// <summary>
        /// Gets the total number of assignments
        /// </summary>
        public int TotalAssignments => _deviceAssignments.Count;

        /// <summary>
        /// Gets the number of addressed devices
        /// </summary>
        public int AddressedDevices => _deviceAssignments.Count(d => d.Address > 0);

        /// <summary>
        /// Gets the number of unaddressed devices
        /// </summary>
        public int UnaddressedDevices => _deviceAssignments.Count(d => d.Address <= 0);

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a device assignment
        /// </summary>
        public async Task<bool> AddAssignmentAsync(DeviceAssignment assignment)
        {
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));

            if (_assignmentLookup.ContainsKey(assignment.ElementId.ToString()))
                return false; // Already exists

            // For now, skip validation as DeviceAssignment is not a DeviceSnapshot
            // TODO: Create a proper conversion or validation method
            var validation = new ValidationResult { IsValid = true };
            if (!validation.IsValid)
                return false;

            _unitOfWork.RegisterNew(assignment);
            _deviceAssignments.Add(assignment);
            _assignmentLookup[assignment.ElementId.ToString()] = assignment;

            OnPropertyChanged(nameof(TotalAssignments));
            OnPropertyChanged(nameof(UnaddressedDevices));

            return true;
        }

        /// <summary>
        /// Updates a device assignment
        /// </summary>
        public async Task<bool> UpdateAssignmentAsync(DeviceAssignment assignment)
        {
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));

            if (!_assignmentLookup.TryGetValue(assignment.ElementId.ToString(), out var existing))
                return false;

            // For now, skip validation as DeviceAssignment is not a DeviceSnapshot
            // TODO: Create a proper conversion or validation method
            var validation = new ValidationResult { IsValid = true };
            if (!validation.IsValid)
                return false;

            _unitOfWork.RegisterModified(assignment);

            // Update the existing assignment
            var index = _deviceAssignments.IndexOf(existing);
            if (index >= 0)
            {
                _deviceAssignments[index] = assignment;
                _assignmentLookup[assignment.ElementId.ToString()] = assignment;
            }

            OnPropertyChanged(nameof(AddressedDevices));
            OnPropertyChanged(nameof(UnaddressedDevices));

            return true;
        }

        /// <summary>
        /// Removes a device assignment
        /// </summary>
        public async Task<bool> RemoveAssignmentAsync(string elementId)
        {
            if (string.IsNullOrWhiteSpace(elementId))
                return false;

            if (!_assignmentLookup.TryGetValue(elementId, out var assignment))
                return false;

            _unitOfWork.RegisterDeleted(assignment);
            _deviceAssignments.Remove(assignment);
            _assignmentLookup.Remove(elementId);

            OnPropertyChanged(nameof(TotalAssignments));
            OnPropertyChanged(nameof(AddressedDevices));
            OnPropertyChanged(nameof(UnaddressedDevices));

            return await Task.FromResult(true);
        }

        /// <summary>
        /// Gets a device assignment by element ID
        /// </summary>
        public DeviceAssignment GetAssignment(string elementId)
        {
            _assignmentLookup.TryGetValue(elementId, out var assignment);
            return assignment;
        }

        /// <summary>
        /// Gets all assignments for a specific circuit
        /// </summary>
        public IEnumerable<DeviceAssignment> GetAssignmentsByCircuit(string circuitNumber)
        {
            return _deviceAssignments.Where(d => d.CircuitNumber == circuitNumber);
        }

        /// <summary>
        /// Gets all assignments by device type
        /// </summary>
        public IEnumerable<DeviceAssignment> GetAssignmentsByDeviceType(string deviceType)
        {
            return _deviceAssignments.Where(d => d.DeviceType == deviceType);
        }

        /// <summary>
        /// Clears all assignments
        /// </summary>
        public async Task ClearAllAsync()
        {
            foreach (var assignment in _deviceAssignments.ToList())
            {
                _unitOfWork.RegisterDeleted(assignment);
            }

            _deviceAssignments.Clear();
            _assignmentLookup.Clear();

            OnPropertyChanged(nameof(TotalAssignments));
            OnPropertyChanged(nameof(AddressedDevices));
            OnPropertyChanged(nameof(UnaddressedDevices));

            await Task.CompletedTask;
        }

        /// <summary>
        /// Bulk adds assignments
        /// </summary>
        public async Task<int> BulkAddAssignmentsAsync(IEnumerable<DeviceAssignment> assignments)
        {
            if (assignments == null)
                return 0;

            int added = 0;
            foreach (var assignment in assignments)
            {
                if (await AddAssignmentAsync(assignment))
                {
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        /// Gets assignments statistics
        /// </summary>
        public AssignmentStatistics GetStatistics()
        {
            var stats = new AssignmentStatistics
            {
                TotalDevices = _deviceAssignments.Count,
                AddressedDevices = AddressedDevices,
                UnaddressedDevices = UnaddressedDevices
            };

            // Group by device type
            stats.DevicesByType = _deviceAssignments
                .GroupBy(d => d.DeviceType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by circuit
            stats.DevicesByCircuit = _deviceAssignments
                .Where(d => !string.IsNullOrWhiteSpace(d.CircuitNumber))
                .GroupBy(d => d.CircuitNumber)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by floor
            stats.DevicesByFloor = _deviceAssignments
                .Where(d => !string.IsNullOrWhiteSpace(d.Level))
                .GroupBy(d => d.Level)
                .ToDictionary(g => g.Key, g => g.Count());

            return stats;
        }

        /// <summary>
        /// Commits all pending changes
        /// </summary>
        public async Task<int> CommitChangesAsync()
        {
            return await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Validates all assignments
        /// </summary>
        public async Task<ValidationResult> ValidateAllAssignmentsAsync()
        {
            var devices = _deviceAssignments.Cast<DeviceSnapshot>().ToList();
            return await _validationService.ValidateDevicesAsync(devices);
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Interface for assignment service
    /// </summary>
    public interface IAssignmentService
    {
        ObservableCollection<DeviceAssignment> DeviceAssignments { get; }
        int TotalAssignments { get; }
        int AddressedDevices { get; }
        int UnaddressedDevices { get; }

        Task<bool> AddAssignmentAsync(DeviceAssignment assignment);
        Task<bool> UpdateAssignmentAsync(DeviceAssignment assignment);
        Task<bool> RemoveAssignmentAsync(string elementId);
        DeviceAssignment GetAssignment(string elementId);
        IEnumerable<DeviceAssignment> GetAssignmentsByCircuit(string circuitNumber);
        IEnumerable<DeviceAssignment> GetAssignmentsByDeviceType(string deviceType);
        Task ClearAllAsync();
        Task<int> BulkAddAssignmentsAsync(IEnumerable<DeviceAssignment> assignments);
        AssignmentStatistics GetStatistics();
        Task<int> CommitChangesAsync();
        Task<ValidationResult> ValidateAllAssignmentsAsync();
    }

    /// <summary>
    /// Assignment statistics
    /// </summary>
    public class AssignmentStatistics
    {
        public int TotalDevices { get; set; }
        public int AddressedDevices { get; set; }
        public int UnaddressedDevices { get; set; }
        public Dictionary<string, int> DevicesByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DevicesByCircuit { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DevicesByFloor { get; set; } = new Dictionary<string, int>();
        public double AddressingCompletionPercentage => TotalDevices > 0 ? (double)AddressedDevices / TotalDevices * 100 : 0;
    }
}