using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Revit_FA_Tools;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Core.Services.Implementation;
using Revit_FA_Tools.Core.Services.Interfaces;
using DeviceSnapshot = Revit_FA_Tools.Models.DeviceSnapshot;

namespace Revit_FA_Tools.Tests.Services
{
    [TestClass]
    public class UnifiedValidationServiceTests
    {
        private UnifiedValidationService _validationService;
        private FireAlarmConfiguration _mockConfiguration;

        [TestInitialize]
        public void Setup()
        {
            _mockConfiguration = CreateMockConfiguration();
            _validationService = new UnifiedValidationService(_mockConfiguration);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _validationService = null;
        }

        #region Device Validation Tests

        [TestMethod]
        public async Task ValidateDeviceAsync_ValidDevice_ReturnsSuccess()
        {
            // Arrange
            var device = CreateValidDeviceSnapshot();

            // Act
            var result = await _validationService.ValidateDeviceAsync(device);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Messages.Count);
            Assert.AreEqual(ValidationSeverity.None, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDeviceAsync_NullDevice_ReturnsFailure()
        {
            // Act
            var result = await _validationService.ValidateDeviceAsync(null);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Device cannot be null", result.Messages.First().Message);
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDeviceAsync_DeviceWithMissingId_ReturnsFailure()
        {
            // Arrange
            var device = CreateValidDeviceSnapshot();
            device = device with { ElementId = 0 };

            // Act
            var result = await _validationService.ValidateDeviceAsync(device);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "DEV001"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDeviceAsync_DeviceWithNegativeCurrent_ReturnsFailure()
        {
            // Arrange
            var device = CreateValidDeviceSnapshot();
            var customProps = new Dictionary<string, object>(device.ActualCustomProperties);
            customProps["CurrentDraw"] = -5.0;
            device = device with { CustomProperties = customProps };

            // Act
            var result = await _validationService.ValidateDeviceAsync(device);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ELEC004"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDeviceAsync_NotificationDeviceWithoutCandela_ReturnsWarning()
        {
            // Arrange
            var device = CreateValidDeviceSnapshot();
            var customProps = new Dictionary<string, object>(device.ActualCustomProperties);
            customProps["IsNotificationDevice"] = true;
            customProps["Candela"] = 0;
            device = device with { CustomProperties = customProps };

            // Act
            var result = await _validationService.ValidateDeviceAsync(device);

            // Assert
            Assert.IsTrue(result.IsValid); // Warnings don't make it invalid
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ELEC005"));
            Assert.AreEqual(ValidationSeverity.Warning, result.HighestSeverity);
        }

        #endregion

        #region Batch Validation Tests

        [TestMethod]
        public async Task ValidateDevicesAsync_MultipleValidDevices_ReturnsSuccess()
        {
            // Arrange
            var devices = new List<DeviceSnapshot>
            {
                CreateValidDeviceSnapshot("Device1"),
                CreateValidDeviceSnapshot("Device2"),
                CreateValidDeviceSnapshot("Device3")
            };

            // Act
            var result = await _validationService.ValidateDevicesAsync(devices);

            // Assert
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task ValidateDevicesAsync_EmptyCollection_ReturnsWarning()
        {
            // Arrange
            var devices = new List<DeviceSnapshot>();

            // Act
            var result = await _validationService.ValidateDevicesAsync(devices);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("No devices to validate", result.Messages.First().Message);
            Assert.AreEqual(ValidationSeverity.Warning, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDevicesAsync_DuplicateAddresses_ReturnsFailure()
        {
            // Arrange
            var devices = new List<DeviceSnapshot>
            {
                CreateDeviceWithAddress("Device1", "1"),
                CreateDeviceWithAddress("Device2", "1"), // Duplicate address
                CreateDeviceWithAddress("Device3", "2")
            };

            // Act
            var result = await _validationService.ValidateDevicesAsync(devices);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ADDR001"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateDevicesAsync_CircuitOverCapacity_ReturnsFailure()
        {
            // Arrange
            var devices = CreateDevicesExceedingCircuitCapacity();

            // Act
            var result = await _validationService.ValidateDevicesAsync(devices);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "CIRC001" || m.Code == "CIRC002"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        #endregion

        #region Electrical Parameter Validation Tests

        [TestMethod]
        public async Task ValidateElectricalParametersAsync_ValidParameters_ReturnsSuccess()
        {
            // Arrange
            var parameters = new ElectricalParameters
            {
                Voltage = 24.0,
                Current = 2.5,
                Power = 60.0,
                CableLength = 100.0,
                CableType = "18AWG",
                AmbientTemperature = 25.0
            };

            // Act
            var result = await _validationService.ValidateElectricalParametersAsync(parameters);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Messages.Count(m => m.Severity >= ValidationSeverity.Error));
        }

        [TestMethod]
        public async Task ValidateElectricalParametersAsync_VoltageOutOfRange_ReturnsFailure()
        {
            // Arrange
            var parameters = new ElectricalParameters
            {
                Voltage = 120.0, // Outside acceptable range for fire alarm
                Current = 2.5,
                Power = 300.0
            };

            // Act
            var result = await _validationService.ValidateElectricalParametersAsync(parameters);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ELEC001"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateElectricalParametersAsync_ExcessiveCurrent_ReturnsFailure()
        {
            // Arrange
            var parameters = new ElectricalParameters
            {
                Voltage = 24.0,
                Current = 10.0, // Exceeds maximum circuit current
                Power = 240.0
            };

            // Act
            var result = await _validationService.ValidateElectricalParametersAsync(parameters);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ELEC002"));
            Assert.AreEqual(ValidationSeverity.Error, result.HighestSeverity);
        }

        [TestMethod]
        public async Task ValidateElectricalParametersAsync_PowerMismatch_ReturnsWarning()
        {
            // Arrange
            var parameters = new ElectricalParameters
            {
                Voltage = 24.0,
                Current = 2.5,
                Power = 50.0 // Should be 60W (24V * 2.5A)
            };

            // Act
            var result = await _validationService.ValidateElectricalParametersAsync(parameters);

            // Assert
            Assert.IsTrue(result.IsValid); // Warnings don't invalidate
            Assert.IsTrue(result.Messages.Any(m => m.Code == "ELEC003"));
            Assert.AreEqual(ValidationSeverity.Warning, result.HighestSeverity);
        }

        #endregion

        #region Parameter Mapping Validation Tests

        [TestMethod]
        public async Task ValidateParameterMappingsAsync_ValidMappings_ReturnsSuccess()
        {
            // Arrange
            var mappings = new Dictionary<string, object>
            {
                ["DeviceType"] = "Smoke Detector",
                ["CurrentDraw"] = 2.5,
                ["Voltage"] = 24.0,
                ["Level"] = "First Floor"
            };

            // Act
            var result = await _validationService.ValidateParameterMappingsAsync(mappings);

            // Assert
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task ValidateParameterMappingsAsync_EmptyKey_ReturnsFailure()
        {
            // Arrange
            var mappings = new Dictionary<string, object>
            {
                [""] = "Invalid Key",
                ["ValidKey"] = "Valid Value"
            };

            // Act
            var result = await _validationService.ValidateParameterMappingsAsync(mappings);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Messages.Any(m => m.Code == "MAP001"));
        }

        [TestMethod]
        public async Task ValidateParameterMappingsAsync_NullValue_ReturnsWarning()
        {
            // Arrange
            var mappings = new Dictionary<string, object>
            {
                ["DeviceType"] = "Smoke Detector",
                ["CurrentDraw"] = null // Null value
            };

            // Act
            var result = await _validationService.ValidateParameterMappingsAsync(mappings);

            // Assert
            Assert.IsTrue(result.IsValid); // Warnings don't invalidate
            Assert.IsTrue(result.Messages.Any(m => m.Code == "MAP002"));
            Assert.AreEqual(ValidationSeverity.Warning, result.HighestSeverity);
        }

        #endregion

        #region Pre-Analysis Validation Tests

        [TestMethod]
        public async Task PerformPreAnalysisAsync_SystemReady_ReturnsReady()
        {
            // Act
            var result = await _validationService.PerformPreAnalysisAsync();

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.CanProceedWithAnalysis);
            Assert.AreEqual(AnalysisReadinessStatus.Ready, result.ReadinessStatus);
        }

        #endregion

        #region Validation Rule Tests

        [TestMethod]
        public void RegisterValidationRule_NewRule_AddsSuccessfully()
        {
            // Arrange
            var rule = new Mock<IValidationRule>();
            rule.Setup(r => r.RuleId).Returns("TEST_RULE");
            rule.Setup(r => r.Context).Returns(ValidationContext.Device);

            // Act
            _validationService.RegisterValidationRule(rule.Object);
            var rules = _validationService.GetValidationRules(ValidationContext.Device);

            // Assert
            Assert.IsTrue(rules.Any(r => r.RuleId == "TEST_RULE"));
        }

        [TestMethod]
        public void GetValidationRules_ContextFilter_ReturnsCorrectRules()
        {
            // Arrange
            var deviceRule = new Mock<IValidationRule>();
            deviceRule.Setup(r => r.RuleId).Returns("DEVICE_RULE");
            deviceRule.Setup(r => r.Context).Returns(ValidationContext.Device);

            var circuitRule = new Mock<IValidationRule>();
            circuitRule.Setup(r => r.RuleId).Returns("CIRCUIT_RULE");
            circuitRule.Setup(r => r.Context).Returns(ValidationContext.Circuit);

            _validationService.RegisterValidationRule(deviceRule.Object);
            _validationService.RegisterValidationRule(circuitRule.Object);

            // Act
            var deviceRules = _validationService.GetValidationRules(ValidationContext.Device);
            var circuitRules = _validationService.GetValidationRules(ValidationContext.Circuit);

            // Assert
            Assert.IsTrue(deviceRules.Any(r => r.RuleId == "DEVICE_RULE"));
            Assert.IsFalse(deviceRules.Any(r => r.RuleId == "CIRCUIT_RULE"));
            Assert.IsTrue(circuitRules.Any(r => r.RuleId == "CIRCUIT_RULE"));
            Assert.IsFalse(circuitRules.Any(r => r.RuleId == "DEVICE_RULE"));
        }

        #endregion

        #region Helper Methods

        private DeviceSnapshot CreateValidDeviceSnapshot(string elementId = "TestDevice")
        {
            var customProperties = new Dictionary<string, object>
            {
                ["Address"] = "",
                ["CircuitNumber"] = "Circuit1",
                ["DeviceFunction"] = "Smoke Detection",
                ["Candela"] = 0,
                ["IsNotificationDevice"] = false,
                ["TypeComments"] = "Test device",
                ["Room"] = "Room 101"
            };

            return new DeviceSnapshot(
                ElementId: int.TryParse(elementId, out int id) ? id : 1,
                LevelName: "First Floor",
                FamilyName: "Test Family",
                TypeName: "Smoke Detector",
                Watts: 5.0,
                Amps: 2.5,
                UnitLoads: 1,
                HasStrobe: false,
                HasSpeaker: false,
                IsIsolator: false,
                IsRepeater: false,
                Zone: null,
                X: 10.0,
                Y: 20.0,
                Z: 30.0,
                StandbyCurrent: 0.0,
                HasOverride: false,
                CustomProperties: customProperties
            );
        }

        private DeviceSnapshot CreateDeviceWithAddress(string elementId, string address)
        {
            var device = CreateValidDeviceSnapshot(elementId);
            var customProps = new Dictionary<string, object>(device.ActualCustomProperties);
            customProps["Address"] = address;
            return device with { CustomProperties = customProps };
        }

        private List<DeviceSnapshot> CreateDevicesExceedingCircuitCapacity()
        {
            var devices = new List<DeviceSnapshot>();
            
            // Create more devices than the maximum allowed per circuit
            for (int i = 1; i <= 30; i++) // Exceeds max of 25 per circuit
            {
                var device = CreateValidDeviceSnapshot($"Device{i}");
                var customProps = new Dictionary<string, object>(device.ActualCustomProperties);
                customProps["CircuitNumber"] = "Circuit1";
                customProps["CurrentDraw"] = 0.5; // Total will exceed max current
                devices.Add(device with { CustomProperties = customProps });
            }

            return devices;
        }

        private FireAlarmConfiguration CreateMockConfiguration()
        {
            return new FireAlarmConfiguration
            {
                ElectricalConfiguration = new ElectricalConfiguration
                {
                    MinVoltage = 20.0,
                    MaxVoltage = 28.0,
                    MaxCircuitCurrent = 7.0
                },
                CircuitConfiguration = new CircuitConfiguration
                {
                    MaxDevicesPerCircuit = 25,
                    MaxAddressPerCircuit = 250
                },
                ParameterMappingConfiguration = new ParameterMappingConfiguration
                {
                    EnableAutoMapping = true,
                    CacheExpirationMinutes = 60
                }
            };
        }

        #endregion
    }
}