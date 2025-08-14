using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Services.Analysis.Pipeline;

namespace Revit_FA_Tools.Core.Services.Analysis.Pipeline.Stages
{
    /// <summary>
    /// Pipeline stage that converts FamilyInstance objects to DeviceSpecification objects
    /// </summary>
    public class DeviceSpecificationStage : IAnalysisStage<List<FamilyInstance>, List<DeviceSpecification>>
    {
        private readonly object _logger;

        public string StageName => "Device Specification";

        public DeviceSpecificationStage(object logger = null)
        {
            _logger = logger;
        }

        public async Task<List<DeviceSpecification>> ExecuteAsync(List<FamilyInstance> input, AnalysisContext context)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            context.ReportProgress(StageName, $"Converting {input.Count} devices to specifications...", 50);

            var specifications = new List<DeviceSpecification>();

            foreach (var device in input)
            {
                try
                {
                    var specification = ConvertToDeviceSpecification(device, context);
                    if (specification != null)
                    {
                        specifications.Add(specification);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other devices
                    System.Diagnostics.Debug.WriteLine($"Error converting device {device.Id}: {ex.Message}");
                }
            }

            context.ReportProgress(StageName, $"Created {specifications.Count} device specifications", 60);

            // Store specifications in context for debugging
            context.SetSharedData("DeviceSpecifications", specifications);
            context.SetSharedData("ConversionSuccess", (double)specifications.Count / input.Count * 100);

            return await Task.FromResult(specifications);
        }

        public bool CanExecute(List<FamilyInstance> input, AnalysisContext context)
        {
            return input != null;
        }

        /// <summary>
        /// Converts a FamilyInstance to a DeviceSpecification
        /// </summary>
        private DeviceSpecification ConvertToDeviceSpecification(FamilyInstance device, AnalysisContext context)
        {
            if (device?.Symbol?.Family == null)
                return null;

            var specification = new DeviceSpecification
            {
                ElementId = device.Id,
                FamilyName = device.Symbol.Family.Name,
                TypeName = device.Symbol.Name,
                LevelName = GetLevelName(device),
                Location = GetDeviceLocation(device)
            };

            // Extract electrical parameters
            ExtractElectricalParameters(device, specification);

            // Extract device characteristics
            ExtractDeviceCharacteristics(device, specification);

            // Extract addressing information
            ExtractAddressingInformation(device, specification);

            // Set analysis timestamp
            specification.AnalysisTimestamp = DateTime.Now;

            return specification;
        }

        /// <summary>
        /// Gets the level name for the device
        /// </summary>
        private string GetLevelName(FamilyInstance device)
        {
            try
            {
                var level = device.Document.GetElement(device.LevelId) as Level;
                return level?.Name ?? "Unknown Level";
            }
            catch
            {
                return "Unknown Level";
            }
        }

        /// <summary>
        /// Gets the device location
        /// </summary>
        private DeviceLocation GetDeviceLocation(FamilyInstance device)
        {
            try
            {
                var location = device.Location;
                if (location is LocationPoint locationPoint)
                {
                    var point = locationPoint.Point;
                    return new DeviceLocation
                    {
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    };
                }
                
                return new DeviceLocation();
            }
            catch
            {
                return new DeviceLocation();
            }
        }

        /// <summary>
        /// Extracts electrical parameters from the device
        /// </summary>
        private void ExtractElectricalParameters(FamilyInstance device, DeviceSpecification specification)
        {
            try
            {
                // Check for current draw parameters
                var currentParams = new[] { "CURRENT DRAW", "Current", "Amps" };
                foreach (var paramName in currentParams)
                {
                    var param = device.LookupParameter(paramName) ?? device.Symbol.LookupParameter(paramName);
                    if (param != null && param.HasValue && param.StorageType == StorageType.Double)
                    {
                        specification.CurrentDraw = param.AsDouble();
                        break;
                    }
                }

                // Check for wattage parameters
                var wattageParams = new[] { "Wattage", "Power", "Watts" };
                foreach (var paramName in wattageParams)
                {
                    var param = device.LookupParameter(paramName) ?? device.Symbol.LookupParameter(paramName);
                    if (param != null && param.HasValue && param.StorageType == StorageType.Double)
                    {
                        specification.PowerConsumption = param.AsDouble();
                        break;
                    }
                }

                // Calculate unit loads based on current (typical formula)
                if (specification.CurrentDraw > 0)
                {
                    specification.UnitLoads = (int)Math.Ceiling(specification.CurrentDraw / 0.0008); // 0.8mA per unit load
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting electrical parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts device characteristics (strobe, speaker, etc.)
        /// </summary>
        private void ExtractDeviceCharacteristics(FamilyInstance device, DeviceSpecification specification)
        {
            try
            {
                var familyName = specification.FamilyName.ToUpperInvariant();
                var typeName = specification.TypeName.ToUpperInvariant();
                var combined = $"{familyName} {typeName}";

                // Detect strobe devices
                specification.HasStrobe = combined.Contains("STROBE");

                // Detect speaker devices
                specification.HasSpeaker = combined.Contains("SPEAKER");

                // Detect isolators
                specification.IsIsolator = combined.Contains("ISOLATOR") || combined.Contains("ISO-");

                // Detect repeaters
                specification.IsRepeater = combined.Contains("REPEATER");

                // Extract candela rating for strobes
                if (specification.HasStrobe)
                {
                    var candelaParam = device.LookupParameter("CANDELA") ?? device.Symbol.LookupParameter("CANDELA");
                    if (candelaParam != null && candelaParam.HasValue)
                    {
                        if (candelaParam.StorageType == StorageType.Double)
                        {
                            specification.CandelaRating = (int)candelaParam.AsDouble();
                        }
                        else if (candelaParam.StorageType == StorageType.Integer)
                        {
                            specification.CandelaRating = candelaParam.AsInteger();
                        }
                    }
                }

                // Extract environmental rating
                var envParam = device.LookupParameter("Environmental_Rating") ?? device.Symbol.LookupParameter("Environmental_Rating");
                if (envParam != null && envParam.HasValue)
                {
                    specification.EnvironmentalRating = envParam.AsString() ?? envParam.AsValueString();
                }

                // Detect environmental rating from name patterns
                if (string.IsNullOrEmpty(specification.EnvironmentalRating))
                {
                    if (combined.Contains("WPHC"))
                        specification.EnvironmentalRating = "WPHC";
                    else if (combined.Contains("WP"))
                        specification.EnvironmentalRating = "WP";
                    else
                        specification.EnvironmentalRating = "Standard";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting device characteristics: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts addressing information
        /// </summary>
        private void ExtractAddressingInformation(FamilyInstance device, DeviceSpecification specification)
        {
            try
            {
                // Extract device address
                var addressParam = device.LookupParameter("FA_Address") ?? device.LookupParameter("Address");
                if (addressParam != null && addressParam.HasValue)
                {
                    if (addressParam.StorageType == StorageType.Integer)
                    {
                        var address = addressParam.AsInteger();
                        if (address > 0)
                            specification.Address = address;
                    }
                }

                // Extract loop/branch information
                var loopParam = device.LookupParameter("FA_Loop") ?? device.LookupParameter("Loop");
                if (loopParam != null && loopParam.HasValue)
                {
                    specification.LoopId = loopParam.AsString() ?? loopParam.AsValueString();
                }

                // Extract zone information
                var zoneParam = device.LookupParameter("FA_Zone") ?? device.LookupParameter("Zone");
                if (zoneParam != null && zoneParam.HasValue)
                {
                    specification.Zone = zoneParam.AsString() ?? zoneParam.AsValueString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting addressing information: {ex.Message}");
            }
        }
    }
}