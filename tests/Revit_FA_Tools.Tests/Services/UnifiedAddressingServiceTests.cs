using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Revit_FA_Tools.Core.Infrastructure.UnitOfWork;
using Revit_FA_Tools.Core.Models.Addressing;
using Revit_FA_Tools;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Core.Services.Implementation;
using Revit_FA_Tools.Core.Services.Interfaces;
using ValidationResult = Revit_FA_Tools.Core.Services.Interfaces.ValidationResult;
using ValidationSeverity = Revit_FA_Tools.Core.Services.Interfaces.ValidationSeverity;
using AddressLockState = Revit_FA_Tools.Models.AddressLockState;

namespace Revit_FA_Tools.Tests.Services
{
    [TestClass]
    public class UnifiedAddressingServiceTests
    {
        private UnifiedAddressingService _addressingService;
        private Mock<IValidationService> _mockValidationService;
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private FireAlarmConfiguration _mockConfiguration;

        [TestInitialize]
        public void Setup()
        {
            _mockValidationService = new Mock<IValidationService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockConfiguration = CreateMockConfiguration();

            _addressingService = new UnifiedAddressingService(
                _mockValidationService.Object,
                _mockUnitOfWork.Object,
                _mockConfiguration);

            SetupMockDefaults();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _addressingService = null;
        }

        #region Address Assignment Tests

        [TestMethod]
        public async Task AssignAddressesAsync_ValidDevices_ReturnsSuccess()
        {
            // Arrange
            var devices = CreateTestDevices(3);
            var options = new AddressingOptions
            {
                RespectLocks = true,
                ValidateElectrical = true,
                StartAddress = 1
            };

            // Act
            var result = await _addressingService.AssignAddressesAsync(devices, options);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.DevicesAddressed);
            Assert.AreEqual(0, result.DevicesSkipped);
            Assert.AreEqual(0, result.Errors.Count);
            
            _mockUnitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [TestMethod]
        public async Task AssignAddressesAsync_NullDevices_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => _addressingService.AssignAddressesAsync(null, new AddressingOptions()));
        }

        [TestMethod]
        public async Task AssignAddressesAsync_LockedDevices_SkipsLockedDevices()
        {
            // Arrange
            var devices = CreateTestDevices(3);
            devices[1].LockState = AddressLockState.Locked; // Lock the second device
            devices[1].Address = "5"; // Already has address

            var options = new AddressingOptions { RespectLocks = true };

            // Act
            var result = await _addressingService.AssignAddressesAsync(devices, options);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.DevicesAddressed); // Only 2 devices should be addressed
            Assert.AreEqual(1, result.DevicesSkipped);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("locked")));
        }

        [TestMethod]
        public async Task AssignAddressesAsync_ValidationFailure_ReturnsFailure()
        {
            // Arrange
            var devices = CreateTestDevices(1);
            var options = new AddressingOptions { ValidateElectrical = true };

            // Setup validation to fail
            _mockValidationService
                .Setup(v => v.ValidateElectricalParametersAsync(It.IsAny<ElectricalParameters>()))
                .ReturnsAsync(new ValidationResult
                {
                    IsValid = false,
                    Messages = new List<ValidationMessage>
                    {
                        new ValidationMessage { Message = "Electrical validation failed", Severity = ValidationSeverity.Error }
                    }
                });

            // Act
            var result = await _addressingService.AssignAddressesAsync(devices, options);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Electrical validation failed")));
            _mockUnitOfWork.Verify(u => u.Rollback(), Times.Once);
        }

        #endregion

        #region Address Validation Tests

        [TestMethod]
        public async Task ValidateAddressingAsync_ValidCircuit_ReturnsSuccess()
        {
            // Arrange
            var circuit = CreateTestCircuit();

            // Act
            var result = await _addressingService.ValidateAddressingAsync(circuit);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(ValidationSeverity.None, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateAddressingAsync_ExceedsDeviceCapacity_ReturnsFailure()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            // Add more devices than allowed
            for (int i = 0; i < 30; i++) // Exceeds max of 25
            {
                circuit.Devices.Add(CreateTestDevice($"ExtraDevice{i}", "10"));
            }

            // Act
            var result = await _addressingService.ValidateAddressingAsync(circuit);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "ADDR_001"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateAddressingAsync_InvalidAddressRange_ReturnsFailure()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            circuit.Devices.Add(CreateTestDevice("InvalidDevice", "300")); // Address > 250

            // Act
            var result = await _addressingService.ValidateAddressingAsync(circuit);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "ADDR_002"));
        }

        [TestMethod]
        public async Task ValidateAddressingAsync_DuplicateAddresses_ReturnsFailure()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            circuit.Devices.Add(CreateTestDevice("Device1", "1"));
            circuit.Devices.Add(CreateTestDevice("Device2", "1")); // Duplicate address

            // Act
            var result = await _addressingService.ValidateAddressingAsync(circuit);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "ADDR_003"));
        }

        [TestMethod]
        public async Task ValidateAddressingAsync_ExceedsCurrentCapacity_ReturnsFailure()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            // Add devices that exceed current capacity
            for (int i = 0; i < 5; i++)
            {
                var device = CreateTestDevice($"HighCurrentDevice{i}", (i + 1).ToString());
                device.CurrentDraw = 2.0m; // Total will be 10A > 7A max
                circuit.Devices.Add(device);
            }

            // Act
            var result = await _addressingService.ValidateAddressingAsync(circuit);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "ADDR_004"));
        }

        #endregion

        #region Address Management Tests

        [TestMethod]
        public void GetAvailableAddresses_EmptyCircuit_ReturnsAllAddresses()
        {
            // Arrange
            var circuit = CreateTestCircuit();

            // Act
            var availableAddresses = _addressingService.GetAvailableAddresses(circuit).Take(10).ToList();

            // Assert
            Assert.AreEqual(10, availableAddresses.Count);
            Assert.AreEqual(1, availableAddresses.First());
            Assert.AreEqual(10, availableAddresses.Last());
        }

        [TestMethod]
        public void GetAvailableAddresses_PartiallyFilled_ReturnsRemainingAddresses()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            circuit.Devices.Add(CreateTestDevice("Device1", "1"));
            circuit.Devices.Add(CreateTestDevice("Device2", "3"));

            // Act
            var availableAddresses = _addressingService.GetAvailableAddresses(circuit).Take(5).ToList();

            // Assert
            Assert.IsFalse(availableAddresses.Contains(1));
            Assert.IsTrue(availableAddresses.Contains(2));
            Assert.IsFalse(availableAddresses.Contains(3));
            Assert.IsTrue(availableAddresses.Contains(4));
        }

        [TestMethod]
        public void ReleaseAddress_ValidAddress_ReleasesSuccessfully()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            var device = CreateTestDevice("Device1", "5");
            circuit.Devices.Add(device);

            // Act
            _addressingService.ReleaseAddress(circuit, 5);

            // Assert
            Assert.AreEqual("", device.Address);
            Assert.AreEqual(AddressLockState.Unlocked, device.LockState);
            _mockUnitOfWork.Verify(u => u.RegisterModified(device), Times.Once);
        }

        [TestMethod]
        public void ReserveAddress_ValidAddress_ReturnsTrue()
        {
            // Arrange
            var circuit = CreateTestCircuit();

            // Act
            var result = _addressingService.ReserveAddress(circuit, 10);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ReserveAddress_UsedAddress_ReturnsFalse()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            circuit.Devices.Add(CreateTestDevice("Device1", "10"));

            // Act
            var result = _addressingService.ReserveAddress(circuit, 10);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ReserveAddress_InvalidRange_ReturnsFalse()
        {
            // Arrange
            var circuit = CreateTestCircuit();

            // Act
            var resultTooLow = _addressingService.ReserveAddress(circuit, 0);
            var resultTooHigh = _addressingService.ReserveAddress(circuit, 251);

            // Assert
            Assert.IsFalse(resultTooLow);
            Assert.IsFalse(resultTooHigh);
        }

        #endregion

        #region Auto Assignment Tests

        [TestMethod]
        public async Task AutoAssignAsync_SequentialStrategy_AssignsInOrder()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            var devices = CreateTestDevices(3);
            foreach (var device in devices)
            {
                device.Circuit = circuit;
                device.Address = ""; // Ensure they're unaddressed
                circuit.Devices.Add(device);
            }

            var options = new AutoAssignOptions
            {
                Strategy = AddressingStrategy.Sequential,
                StartAddress = 1
            };

            // Act
            var result = await _addressingService.AutoAssignAsync(circuit, options);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.DevicesAddressed);
        }

        [TestMethod]
        public async Task AutoAssignAsync_OptimizeByLocation_OrdersByLocation()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            var devices = CreateTestDevices(3);
            devices[0].Level = "Second Floor";
            devices[1].Level = "First Floor";
            devices[2].Level = "First Floor";

            foreach (var device in devices)
            {
                device.Circuit = circuit;
                circuit.Devices.Add(device);
            }

            var options = new AutoAssignOptions
            {
                OptimizeByLocation = true,
                Strategy = AddressingStrategy.ByFloor
            };

            // Act
            var result = await _addressingService.AutoAssignAsync(circuit, options);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.DevicesAddressed);
        }

        #endregion

        #region Clear Addresses Tests

        [TestMethod]
        public void ClearAddresses_MultipleDevices_ClearsUnlockedAddresses()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            var device1 = CreateTestDevice("Device1", "1");
            var device2 = CreateTestDevice("Device2", "2");
            device2.LockState = AddressLockState.Locked; // This one should not be cleared
            var device3 = CreateTestDevice("Device3", "3");

            foreach (var device in new[] { device1, device2, device3 })
            {
                circuit.Devices.Add(device);
            }

            // Act
            _addressingService.ClearAddresses(circuit);

            // Assert
            Assert.AreEqual("", device1.Address);
            Assert.AreEqual("2", device2.Address); // Should remain locked
            Assert.AreEqual("", device3.Address);
            
            _mockUnitOfWork.Verify(u => u.RegisterModified(device1), Times.Once);
            _mockUnitOfWork.Verify(u => u.RegisterModified(device2), Times.Never);
            _mockUnitOfWork.Verify(u => u.RegisterModified(device3), Times.Once);
        }

        #endregion

        #region Address Allocation Status Tests

        [TestMethod]
        public void GetAllocationStatus_MixedCircuit_ReturnsCorrectStatus()
        {
            // Arrange
            var circuit = CreateTestCircuit();
            circuit.Devices.Add(CreateTestDevice("Device1", "1"));
            circuit.Devices.Add(CreateTestDevice("Device2", "5"));
            circuit.Devices.Add(CreateTestDevice("Device3", "10"));

            // Act
            var status = _addressingService.GetAllocationStatus(circuit);

            // Assert
            Assert.AreEqual(250, status.TotalAddresses);
            Assert.AreEqual(3, status.UsedAddresses);
            Assert.AreEqual(247, status.AvailableAddresses);
            Assert.AreEqual(1.2, status.UtilizationPercentage, 0.1);
            Assert.IsTrue(status.AllocatedAddresses.Contains(1));
            Assert.IsTrue(status.AllocatedAddresses.Contains(5));
            Assert.IsTrue(status.AllocatedAddresses.Contains(10));
        }

        #endregion

        #region Helper Methods

        private List<SmartDeviceNode> CreateTestDevices(int count)
        {
            var devices = new List<SmartDeviceNode>();
            
            for (int i = 0; i < count; i++)
            {
                devices.Add(CreateTestDevice($"TestDevice{i + 1}", ""));
            }

            return devices;
        }

        private SmartDeviceNode CreateTestDevice(string elementId, string address)
        {
            return new SmartDeviceNode
            {
                ElementId = elementId,
                DeviceType = "Smoke Detector",
                DeviceFunction = "Smoke Detection",
                Level = "First Floor",
                Room = $"Room {elementId}",
                Address = address,
                CurrentDraw = 1.5m,
                LockState = AddressLockState.Unlocked,
                X = 10.0,
                Y = 20.0,
                Z = 30.0
            };
        }

        private AddressingCircuit CreateTestCircuit()
        {
            return new AddressingCircuit
            {
                CircuitNumber = "TestCircuit",
                MaxDevices = 25,
                MaxCurrent = 7.0m,
                Devices = new ObservableCollection<SmartDeviceNode>()
            };
        }

        private FireAlarmConfiguration CreateMockConfiguration()
        {
            return new FireAlarmConfiguration
            {
                CircuitConfiguration = new CircuitConfiguration
                {
                    MaxDevicesPerCircuit = 25,
                    MaxAddressPerCircuit = 250
                },
                ElectricalConfiguration = new ElectricalConfiguration
                {
                    MaxCircuitCurrent = 7.0
                }
            };
        }

        private void SetupMockDefaults()
        {
            // Setup default successful validation
            _mockValidationService
                .Setup(v => v.ValidateElectricalParametersAsync(It.IsAny<ElectricalParameters>()))
                .ReturnsAsync(new ValidationResult { IsValid = true });

            // Setup unit of work mocks
            _mockUnitOfWork.Setup(u => u.BeginTransaction());
            _mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.Rollback());
            _mockUnitOfWork.Setup(u => u.RegisterModified(It.IsAny<object>()));
        }

        #endregion
    }
}