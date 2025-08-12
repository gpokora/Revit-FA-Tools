using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Revit_FA_Tools.Core.Models.Analysis;

namespace Revit_FA_Tools
{
    public class IDNETAnalyzer
    {
        private const int MAX_DEVICES_PER_CHANNEL = 250;
        private const int USABLE_DEVICES_PER_CHANNEL = 200; // 20% spare capacity
        private const double MAX_WIRE_LENGTH = 12500; // feet per fire alarm specs
        private const double DEVICE_STANDBY_CURRENT = 0.8; // mA per unit load
        private const int UNIT_LOADS_PER_DEVICE = 1;

        // Compiled regex patterns for better performance
        private static readonly RegexOptions RegexOpts = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        
        // Multi-Criteria Detectors (handles typo "CRITERA" and correct "CRITERIA")
        private static readonly Regex MultiCriteriaPattern = new Regex(
            @"\b(DETECTORS\s*-\s*MULTI\s+(CRIT[EI]RA|CRITERIA)|MULTI[-\s]*(CRIT[EI]RA|CRITERIA)|MULTICRIT[EI]RA)\b", RegexOpts);
            
        // Specific Detector Patterns
        private static readonly Regex AddressableDetectorPattern = new Regex(@"\bDETECTORS\s*-\s*ADDRESSABLE\b", RegexOpts);
        private static readonly Regex ConventionalDetectorPattern = new Regex(@"\bDETECTORS\s*-\s*CONVENTIONAL\b", RegexOpts);
        
        // Smoke Detection Patterns
        private static readonly Regex SmokeDetectorPattern = new Regex(
            @"\b(SMOKE|SMK|PHOTOELECTRIC|IONIZATION|OPTICAL|CHAMBER|BEAM|ASPIRATING|VESDA|ASD|LASER)\b", RegexOpts);
            
        // Heat Detection Patterns  
        private static readonly Regex HeatDetectorPattern = new Regex(
            @"\b(HEAT|THERMAL|FIXED\s+TEMP|RATE|ROR|TEMPERATURE|THERMISTOR|LINEAR\s+HEAT)\b", RegexOpts);
            
        // Gas Detection Patterns
        private static readonly Regex GasDetectorPattern = new Regex(
            @"\b(GAS|CARBON|METHANE|PROPANE|HYDROGEN|TOXIC|COMBUSTIBLE)\b|(?<!\w)CO(?!\w)", RegexOpts);
            
        // Flame Detection Patterns
        private static readonly Regex FlameDetectorPattern = new Regex(
            @"\b(FLAME|INFRARED|ULTRAVIOLET|FIRE\s+EYE)\b|(?<!\w)IR(?!\w)|(?<!\w)UV(?!\w)|TRIPLE\s+IR", RegexOpts);
            
        // Module Patterns
        private static readonly Regex AddressableModulePattern = new Regex(@"\bMODULES\s*-\s*ADDRESSABLE\b", RegexOpts);
        private static readonly Regex IoModulePattern = new Regex(@"\b(IO|I/O)\b", RegexOpts);
        private static readonly Regex SingleModulePattern = new Regex(@"\bSINGLE\b", RegexOpts);
        private static readonly Regex DualModulePattern = new Regex(@"\bDUAL\b", RegexOpts);
        
        // Communication Patterns
        private static readonly Regex TwoWayCommPattern = new Regex(@"\bFA_TWO\s+WAY\s+COMMUNICATION\b|\bTWO\s+WAY\s+COMMUNICATION\b", RegexOpts);
        private static readonly Regex FiremanPhonePattern = new Regex(@"\b(FIREMAN|PHONE\s+JACK)\b", RegexOpts);
        private static readonly Regex RefugePhonePattern = new Regex(@"\b(AREA\s+OF\s+REFUGE|REFUGE)\b", RegexOpts);
        
        // Manual Station Patterns
        private static readonly Regex ManualStationPattern = new Regex(
            @"\b(MANUAL|PULL|STATION|MPS|EMERGENCY|CALL\s+POINT)\b|BREAK\s+GLASS", RegexOpts);
            
        // Monitor/Input Module Patterns
        private static readonly Regex MonitorModulePattern = new Regex(
            @"\b(MONITOR|MON|INPUT|SUPERVISED|INTERFACE\s+INPUT)\b|(?<!\w)MI(?=\s|$)|CONVENTIONAL\s+ZONE", RegexOpts);
            
        // Control/Output Module Patterns
        private static readonly Regex ControlModulePattern = new Regex(
            @"\b(CONTROL|CTRL|RELAY|OUTPUT|SOLENOID|DAMPER|MAGNETIC|VALVE|ACTUATOR)\b|DOOR\s+CONTROL|CO\s+MODULE", RegexOpts);
            
        // Notification Patterns (IDNET-compatible)
        private static readonly Regex NotificationPattern = new Regex(
            @"\b(SPEAKER|STROBE|HORN|CHIME|BELL|BEACON|VOICE|EVACUATION)\b|MASS\s+NOTIFICATION|ADDRESSABLE\s+NOTIFICATION", RegexOpts);
            
        // Duct Detector Patterns
        private static readonly Regex DuctDetectorPattern = new Regex(
            @"\b(DUCT|AHU|HVAC|SAMPLING)\b|AIR\s+HANDLER|RETURN\s+AIR", RegexOpts);
            
        // Sprinkler System Patterns
        private static readonly Regex SprinklerSystemPattern = new Regex(
            @"\b(WATERFLOW|TAMPER|PRESSURE|SPRINKLER)\b|WATER\s+FLOW|(?<!\w)WF(?=\s|$)|FLOW\s+SWITCH", RegexOpts);
            
        // Wireless/Network Patterns
        private static readonly Regex WirelessNetworkPattern = new Regex(
            @"\b(WIRELESS|RADIO|MESH|TRANSLATOR|REPEATER|GATEWAY|BRIDGE)\b|NETWORK\s+NODE", RegexOpts);
            
        // System Device Patterns
        private static readonly Regex SystemDevicePattern = new Regex(
            @"\b(ISOLATOR|SCI|BOOSTER)\b|SHORT\s+CIRCUIT|FAULT\s+ISOLATION|LOOP\s+DRIVER", RegexOpts);
            
        // Emergency Communication Patterns
        private static readonly Regex EmergencyCommPattern = new Regex(
            @"\b(TELEPHONE|PHONE|WARDEN|INTERCOM|COMMUNICATION|HANDSET)\b|EMERGENCY\s+PHONE", RegexOpts);
            
        // Access Control Patterns
        private static readonly Regex AccessControlPattern = new Regex(
            @"\b(ACCESS|BADGE|PROXIMITY|KEYPAD|EGRESS|LOCKDOWN)\b|CARD\s+READER", RegexOpts);
            
        // Generic Detector Patterns (fallback)
        private static readonly Regex GenericDetectorPattern = new Regex(@"\b(DETECTOR|SENSOR|SENSING)\b", RegexOpts);
        
        // Fire Alarm Device Patterns (fallback)
        private static readonly Regex FireAlarmPattern = new Regex(@"\bFIRE\b.*\b(ALARM|DETECT|MONITOR)\b", RegexOpts);
        
        // Model Number Patterns (fallback)
        private static readonly Regex ModelNumberPattern = new Regex(@"\b(FA-|FD-|FS-|FM-|SD-|HD-|MS-)", RegexOpts);
        
        // Addressable Device Patterns (fallback)
        private static readonly Regex AddressableDevicePattern = new Regex(
            @"\b(ADDRESSABLE|ANALOG|INTELLIGENT)\b.*\b(DEVICE|MODULE|UNIT|POINT)\b", RegexOpts);
            
        // Manufacturer Patterns (fallback)
        private static readonly Regex ManufacturerPattern = new Regex(
            @"\b(NOTIFIER|EDWARDS|SIMPLEX|HONEYWELL|APOLLO|GAMEWELL|VIGILANT|MIRCOM|SIEMENS)\b", RegexOpts);

        public IDNETSystemResults AnalyzeIDNET(ElectricalResults electricalData)
    {
        try
        {
            var idnetDevices = DetectIDNETDevices(electricalData);

            if (!idnetDevices.Any())
                return CreateEmptyResults();

            var levelAnalysis = AnalyzeDevicesByLevel(idnetDevices);
            var networkSegments = PlanNetworkTopology(idnetDevices);
            var systemSummary = CreateSystemSummary(idnetDevices, networkSegments);

            return new IDNETSystemResults
            {
                LevelAnalysis = levelAnalysis,
                AllDevices = idnetDevices,
                TotalDevices = idnetDevices.Count,
                TotalPowerConsumption = idnetDevices.Count * DEVICE_STANDBY_CURRENT,
                TotalUnitLoads = idnetDevices.Sum(d => d.UnitLoads),
                NetworkSegments = networkSegments,
                SystemSummary = systemSummary,
                AnalysisTimestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IDNET Analysis Error: {ex.Message}");
            return CreateEmptyResults();
        }
    }

    private List<IDNETDevice> DetectIDNETDevices(ElectricalResults electricalData)
    {
        var idnetDevices = new List<IDNETDevice>();
        if (electricalData?.Elements == null) 
        {
            System.Diagnostics.Debug.WriteLine("IDNET Analysis: No electrical data elements found");
            return idnetDevices;
        }

        System.Diagnostics.Debug.WriteLine($"IDNET Analysis: Analyzing {electricalData.Elements.Count} elements for IDNET devices");
        
        // TEST: Run regex pattern tests
        TestRegexPatterns();
        
        var sampleDeviceNames = new List<string>();
        var detectedCount = 0;
        
        foreach (var elem in electricalData.Elements)
        {
            // Collect sample device names for debugging (first 20)
            if (sampleDeviceNames.Count < 20)
            {
                sampleDeviceNames.Add(elem.FamilyName ?? "Unknown");
            }
            
            // Use enhanced categorization with family name, type name, and description
            var deviceType = CategorizeIDNETDeviceEnhanced(elem);
            if (deviceType != null)
            {
                detectedCount++;
                if (detectedCount <= 10) // Show first 10 matches for debugging
                {
                    System.Diagnostics.Debug.WriteLine($"IDNET Device Found: '{elem.FamilyName}' → {deviceType}");
                }
                var idnetDevice = new IDNETDevice
                {
                    DeviceId = elem.Id.ToString(),
                    FamilyName = elem.FamilyName,
                    DeviceType = deviceType,
                    Location = "N/A", // Extend if needed
                    Level = elem.LevelName,
                    PowerConsumption = DEVICE_STANDBY_CURRENT,
                    UnitLoads = UNIT_LOADS_PER_DEVICE,
                    Zone = elem.LevelName ?? "Zone 1",
                    Position = null, // If coordinates are needed, map from elem
                    SuggestedAddress = 0,
                    NetworkSegment = "TBD"
                };

                // Extract ADDRESS, FUNCTION, and AREA parameters from Revit element
                ExtractIDNETParameters(elem, idnetDevice);

                idnetDevices.Add(idnetDevice);
            }
        }
        
        // Show sample of all family names for debugging
        System.Diagnostics.Debug.WriteLine($"\nIDNET Analysis - Sample Family Names (first 20):");
        foreach (var name in sampleDeviceNames.Take(20))
        {
            System.Diagnostics.Debug.WriteLine($"  - '{name}'");
        }
        
        System.Diagnostics.Debug.WriteLine($"\nIDNET Analysis Complete: Found {idnetDevices.Count} IDNET devices out of {electricalData.Elements.Count} total elements");
        
        if (idnetDevices.Count == 0 && electricalData.Elements.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine("\nNo IDNET devices detected. This could mean:");
            System.Diagnostics.Debug.WriteLine("1. Device family names don't match expected patterns");
            System.Diagnostics.Debug.WriteLine("2. Only notification devices exist (not detection devices)");
            System.Diagnostics.Debug.WriteLine("3. Custom naming conventions are used");
        }
        
        return idnetDevices;
    }

    /// <summary>
    /// Extracts parameters containing ADDRESS, FUNCTION, and AREA from Revit element
    /// Enhanced to detect model-specific parameters:
    /// - ADDRESS: 1_ADDRESS, 2_ADDRESS, PSADDRESS
    /// - FUNCTION: 1_FUNCTION_DESCRIPTION, 1_NOTE, 2_NOTE
    /// - AREA: AREA_DESCRIPTION
    /// </summary>
    private void ExtractIDNETParameters(ElementData element, IDNETDevice idnetDevice)
    {
        if (element?.Element == null) return;

        try
        {
            var revitElement = element.Element;
            
            // Get all parameters from the element
            var parameters = revitElement.Parameters;
            if (parameters == null) return;

            foreach (var param in parameters.Cast<Autodesk.Revit.DB.Parameter>())
            {
                try
                {
                    var paramName = param?.Definition?.Name;
                    if (string.IsNullOrEmpty(paramName)) continue;

                    var paramNameUpper = paramName.ToUpperInvariant();
                    string paramValue = null;

                    // Get parameter value as string
                    if (param.HasValue)
                    {
                        switch (param.StorageType)
                        {
                            case Autodesk.Revit.DB.StorageType.String:
                                paramValue = param.AsString();
                                break;
                            case Autodesk.Revit.DB.StorageType.Integer:
                                paramValue = param.AsInteger().ToString();
                                break;
                            case Autodesk.Revit.DB.StorageType.Double:
                                paramValue = param.AsDouble().ToString("F3");
                                break;
                            case Autodesk.Revit.DB.StorageType.ElementId:
                                paramValue = param.AsElementId()?.ToString();
                                break;
                        }
                    }

                    if (string.IsNullOrEmpty(paramValue)) continue;

                    // Check for ADDRESS parameters (1_ADDRESS, 2_ADDRESS, PSADDRESS)
                    if (paramNameUpper.Contains("ADDRESS"))
                    {
                        idnetDevice.AddressParameters[paramName] = paramValue;
                        // Set primary Address if not already set (prioritize 1_ADDRESS)
                        if (string.IsNullOrEmpty(idnetDevice.Address) || paramName == "1_ADDRESS")
                        {
                            idnetDevice.Address = paramValue;
                        }
                    }

                    // Check for FUNCTION parameters (1_FUNCTION_DESCRIPTION)
                    if (paramNameUpper.Contains("FUNCTION"))
                    {
                        idnetDevice.FunctionParameters[paramName] = paramValue;
                        // Set primary Function if not already set
                        if (string.IsNullOrEmpty(idnetDevice.Function))
                        {
                            idnetDevice.Function = paramValue;
                        }
                    }

                    // Check for AREA parameters (AREA_DESCRIPTION)
                    if (paramNameUpper.Contains("AREA"))
                    {
                        idnetDevice.AreaParameters[paramName] = paramValue;
                        // Set primary Area if not already set
                        if (string.IsNullOrEmpty(idnetDevice.Area))
                        {
                            idnetDevice.Area = paramValue;
                        }
                    }
                    
                    // Check for NOTE parameters (1_NOTE, 2_NOTE) which contain function information
                    if (paramNameUpper.Contains("NOTE") && !string.IsNullOrEmpty(paramValue))
                    {
                        // Add notes to function parameters as they contain functional descriptions
                        idnetDevice.FunctionParameters[paramName] = paramValue;
                        // If no function set and this is 1_NOTE, use it as primary function
                        if (string.IsNullOrEmpty(idnetDevice.Function) && paramName == "1_NOTE")
                        {
                            idnetDevice.Function = paramValue;
                        }
                    }

                    // Track all extracted parameters for debugging
                    if (paramNameUpper.Contains("ADDRESS") || paramNameUpper.Contains("FUNCTION") || 
                        paramNameUpper.Contains("AREA") || paramNameUpper.Contains("NOTE"))
                    {
                        idnetDevice.ExtractedParameters.Add($"{paramName}: {paramValue}");
                    }
                }
                catch (Exception paramEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error extracting parameter {param?.Definition?.Name}: {paramEx.Message}");
                }
            }

            // Log extraction results for debugging
            if (idnetDevice.ExtractedParameters.Any())
            {
                System.Diagnostics.Debug.WriteLine($"IDNET Device {idnetDevice.DeviceId} extracted parameters: {string.Join(", ", idnetDevice.ExtractedParameters)}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting IDNET parameters for device {idnetDevice.DeviceId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Enhanced device categorization using family name, type name, and description
    /// Optimized for model-specific naming patterns:
    /// - DETECTORS - MULTI CRITERA 2 ADDRESSABLE
    /// - DETECTORS - ADDRESSABLE (Smoke Photo, Heat)
    /// - DETECTORS - CONVENTIONAL
    /// - MODULES - ADDRESSABLE (IO, SINGLE, DUAL)
    /// - FA_TWO WAY COMMUNICATION (Fireman Phone, Area of Refuge)
    /// </summary>
    private string CategorizeIDNETDeviceEnhanced(ElementData element)
    {
        if (element == null) return null;
        
        // Combine all available information for better detection
        var familyName = element.FamilyName?.ToUpperInvariant() ?? "";
        var typeName = element.TypeName?.ToUpperInvariant() ?? "";
        var description = element.Description?.ToUpperInvariant() ?? "";
        
        // Create combined search string
        string combinedInfo = $"{familyName} {typeName} {description}";
        
        // Use the original categorization logic with the combined information
        return CategorizeIDNETDevice(combinedInfo);
    }

    private string CategorizeIDNETDevice(string combinedInfo)
    {
        if (string.IsNullOrEmpty(combinedInfo)) return null;
        
        // DEBUG: Show that we're using regex-based detection
        System.Diagnostics.Debug.WriteLine($"IDNET Regex Detection: Processing '{combinedInfo.Substring(0, Math.Min(50, combinedInfo.Length))}{(combinedInfo.Length > 50 ? "..." : "")}'");
        
        // ENHANCED REGEX-BASED DETECTION WITH WORD BOUNDARIES
        // Multi-Criteria Detectors (highest priority - check first)
        if (MultiCriteriaPattern.IsMatch(combinedInfo))
        {
            System.Diagnostics.Debug.WriteLine("IDNET Regex: Matched Multi-Criteria pattern");
            return "Multi-Criteria Detector";
        }
            
        // Addressable Detectors with specific types
        if (AddressableDetectorPattern.IsMatch(combinedInfo))
        {
            System.Diagnostics.Debug.WriteLine("IDNET Regex: Matched Addressable Detector pattern");
            if (SmokeDetectorPattern.IsMatch(combinedInfo))
                return "Addressable Smoke Detector";
            else if (HeatDetectorPattern.IsMatch(combinedInfo))
                return "Addressable Heat Detector";
            else
                return "Addressable Detector";
        }
        
        // Conventional Detectors
        if (ConventionalDetectorPattern.IsMatch(combinedInfo))
        {
            if (HeatDetectorPattern.IsMatch(combinedInfo))
                return "Conventional Heat Detector";
            else if (SmokeDetectorPattern.IsMatch(combinedInfo))
                return "Conventional Smoke Detector";
            else
                return "Conventional Detector";
        }
        
        // General Smoke Detection Devices
        if (SmokeDetectorPattern.IsMatch(combinedInfo))
            return "Smoke Detector";
            
        // Heat Detection Devices
        if (HeatDetectorPattern.IsMatch(combinedInfo))
            return "Heat Detector";
            
        // Combination Detectors (smoke AND heat patterns, or combo keywords)
        if ((SmokeDetectorPattern.IsMatch(combinedInfo) && HeatDetectorPattern.IsMatch(combinedInfo)) ||
            Regex.IsMatch(combinedInfo, @"\b(COMBO|COMBINATION|INTELLISENSE|ACCLIMATE)\b", RegexOptions.IgnoreCase))
            return "Combination Detector";
            
        // Gas Detection
        if (GasDetectorPattern.IsMatch(combinedInfo) && !Regex.IsMatch(combinedInfo, @"\bCOMBO\b", RegexOptions.IgnoreCase))
            return "Gas Detector";
            
        // Flame Detection
        if (FlameDetectorPattern.IsMatch(combinedInfo))
            return "Flame Detector";
            
        // ENHANCED MODULE DETECTION BASED ON MODEL ANALYSIS
        // Addressable Modules with specific types
        if (AddressableModulePattern.IsMatch(combinedInfo))
        {
            System.Diagnostics.Debug.WriteLine("IDNET Regex: Matched Addressable Module pattern");
            if (IoModulePattern.IsMatch(combinedInfo))
                return "Addressable I/O Module";
            else if (SingleModulePattern.IsMatch(combinedInfo))
                return "Addressable Single Module";
            else if (DualModulePattern.IsMatch(combinedInfo))
                return "Addressable Dual Module";
            else
                return "Addressable Module";
        }
        
        // Two-Way Communication Devices (Manual Stations/Phones)
        if (TwoWayCommPattern.IsMatch(combinedInfo))
        {
            System.Diagnostics.Debug.WriteLine("IDNET Regex: Matched Two-Way Communication pattern");
            if (FiremanPhonePattern.IsMatch(combinedInfo))
                return "Fireman Phone Jack";
            else if (RefugePhonePattern.IsMatch(combinedInfo))
                return "Area of Refuge Phone";
            else
                return "Emergency Communication";
        }
        
        // Manual Stations and Controls
        if (ManualStationPattern.IsMatch(combinedInfo))
            return "Manual Station";
            
        // Input/Monitor Modules
        if (MonitorModulePattern.IsMatch(combinedInfo))
            return "Monitor Module";
            
        // Control/Output Modules
        if (ControlModulePattern.IsMatch(combinedInfo))
            return "Control Module";
            
        // Notification Appliances (IDNET-compatible speakers/strobes)
        if (NotificationPattern.IsMatch(combinedInfo))
            return "IDNET Notification";
            
        // Duct Detectors
        if (DuctDetectorPattern.IsMatch(combinedInfo))
            return "Duct Detector";
            
        // Waterflow and Sprinkler System Devices
        if (SprinklerSystemPattern.IsMatch(combinedInfo))
            return "Sprinkler System";
            
        // Special Application Devices
        if (WirelessNetworkPattern.IsMatch(combinedInfo))
            return "Wireless/Network";
            
        // Power and System Devices
        if (SystemDevicePattern.IsMatch(combinedInfo))
            return "System Device";
            
        // Emergency Communication
        if (EmergencyCommPattern.IsMatch(combinedInfo))
            return "Emergency Communication";
            
        // Access Control Integration
        if (AccessControlPattern.IsMatch(combinedInfo))
            return "Access Control";
            
        // FALLBACK DETECTION - More permissive patterns for common fire alarm devices
        // Try broader patterns that might catch devices with different naming conventions
        
        // General detector patterns
        if (GenericDetectorPattern.IsMatch(combinedInfo))
            return "Generic Detector";
            
        // Fire alarm related patterns  
        if (FireAlarmPattern.IsMatch(combinedInfo))
            return "Fire Alarm Device";
            
        // Common abbreviations and model numbers
        if (ModelNumberPattern.IsMatch(combinedInfo))
            return "Fire Alarm Device";
            
        // Device categories that might be fire alarm related
        if (AddressableDevicePattern.IsMatch(combinedInfo))
            return "Addressable Device";
        
        // Common fire alarm manufacturer patterns
        if (ManufacturerPattern.IsMatch(combinedInfo))
            return "Fire Alarm Device";
            
        System.Diagnostics.Debug.WriteLine($"IDNET Regex: No pattern matched for '{combinedInfo}'");
        return null;
    }

    /// <summary>
    /// Test method to validate regex patterns (for debugging)
    /// </summary>
    private void TestRegexPatterns()
    {
        var testCases = new[]
        {
            "DETECTORS - MULTI CRITERA 2 ADDRESSABLE", // Test typo handling
            "DETECTORS - MULTI CRITERIA ADDRESSABLE",  // Test correct spelling
            "MULTI-CRITERIA DETECTOR",                 // Test hyphenated version
            "DETECTORS - ADDRESSABLE SMOKE",           // Test addressable smoke
            "MODULES - ADDRESSABLE IO",                // Test addressable I/O module
            "FA_TWO WAY COMMUNICATION FIREMAN",        // Test two-way communication
            "MANUAL PULL STATION",                     // Test manual station
            "SPEAKER WALL MOUNT",                      // Test notification (should be caught)
            "HEAT DETECTOR CONVENTIONAL"               // Test heat detector
        };

        System.Diagnostics.Debug.WriteLine("IDNET Regex Pattern Test Results:");
        foreach (var testCase in testCases)
        {
            var result = CategorizeIDNETDevice(testCase);
            System.Diagnostics.Debug.WriteLine($"  '{testCase}' → '{result ?? "NO MATCH"}'");
        }
    }

    private Dictionary<string, IDNETLevelAnalysis> AnalyzeDevicesByLevel(List<IDNETDevice> devices)
    {
        var result = new Dictionary<string, IDNETLevelAnalysis>();
        var grouped = devices.GroupBy(d => d.Level ?? "Unknown Level");
        foreach (var group in grouped)
        {
            var deviceTypeCounts = group.GroupBy(x => x.DeviceType).ToDictionary(g => g.Key, g => g.Count());
            result[group.Key] = new IDNETLevelAnalysis
            {
                LevelName = group.Key,
                TotalDevices = group.Count(),
                DeviceTypeCount = deviceTypeCounts,
                TotalPowerConsumption = group.Count() * DEVICE_STANDBY_CURRENT,
                TotalUnitLoads = group.Count(),
                SuggestedNetworkSegments = (int)Math.Ceiling(group.Count() / (double)USABLE_DEVICES_PER_CHANNEL),
                Devices = group.ToList()
            };
        }
        return result;
    }

    private List<IDNETNetworkSegment> PlanNetworkTopology(List<IDNETDevice> devices)
    {
        var segments = new List<IDNETNetworkSegment>();
        var devicesToAssign = devices.ToList();
        int segId = 1;
        while (devicesToAssign.Any())
        {
            var segmentDevices = devicesToAssign.Take(USABLE_DEVICES_PER_CHANNEL).ToList();
            devicesToAssign.RemoveRange(0, segmentDevices.Count);

            var seg = new IDNETNetworkSegment
            {
                SegmentId = $"IDNET-{segId:D2}",
                Devices = segmentDevices,
                EstimatedWireLength = EstimateWireLength(segmentDevices),
                DeviceCount = segmentDevices.Count,
                RequiresRepeater = false, // Update with length check if needed
                StartingAddress = $"{segId:D2}001",
                EndingAddress = $"{segId:D2}{segmentDevices.Count:D3}",
                CoveredLevels = segmentDevices.Select(x => x.Level).Distinct().ToList()
            };
            segments.Add(seg);
            segId++;
        }
        return segments;
    }

    private double EstimateWireLength(List<IDNETDevice> devices)
    {
        if (!devices.Any()) return 0;
        int levels = devices.Select(x => x.Level).Distinct().Count();
        return levels * 100 + devices.Count * 20;
    }

    private IDNETSystemSummary CreateSystemSummary(List<IDNETDevice> devices, List<IDNETNetworkSegment> segments)
    {
        var recommendations = new List<string>();
        int repeatersRequired = 0;
        
        // Analyze wire length requirements
        foreach (var seg in segments)
        {
            if (seg.EstimatedWireLength > MAX_WIRE_LENGTH)
            {
                repeatersRequired++;
                recommendations.Add($"Segment {seg.SegmentId} requires repeater (est. {seg.EstimatedWireLength:F0} ft)");
            }
        }
        
        // Power consumption analysis
        var totalPower = devices.Count * DEVICE_STANDBY_CURRENT;
        recommendations.Add($"Total IDNET power consumption: {totalPower:F1} mA");
        
        // Device type analysis
        var deviceTypes = devices.GroupBy(d => d.DeviceType).ToDictionary(g => g.Key, g => g.Count());
        var criticalDevices = deviceTypes.Where(kvp => kvp.Key.Contains("Smoke") || kvp.Key.Contains("Heat") || kvp.Key.Contains("Manual")).Sum(kvp => kvp.Value);
        var supportDevices = devices.Count - criticalDevices;
        
        if (criticalDevices > 0)
            recommendations.Add($"Life safety devices: {criticalDevices} detection/manual devices");
        if (supportDevices > 0)
            recommendations.Add($"Support devices: {supportDevices} modules and ancillary devices");
            
        // Network topology recommendations
        if (segments.Count > 1)
        {
            recommendations.Add($"Multi-segment system: {segments.Count} IDNET channels required");
            if (segments.Count > 3)
                recommendations.Add("Large system: Consider multiple IDNET hubs for redundancy");
        }
        
        // Address capacity analysis
        var maxDevicesPerSegment = segments.Max(s => s.DeviceCount);
        if (maxDevicesPerSegment > USABLE_DEVICES_PER_CHANNEL * 0.8)
            recommendations.Add($"High device density: Max {maxDevicesPerSegment} devices on single segment (80%+ capacity)");
            
        // Integration recommendations
        var hasNotificationDevices = devices.Any(d => d.DeviceType == "IDNET Notification");
        var hasControlModules = devices.Any(d => d.DeviceType == "Control Module");
        
        if (hasNotificationDevices)
            recommendations.Add("IDNET notification devices detected - verify integration with IDNAC system");
        if (hasControlModules)
            recommendations.Add("Control modules present - ensure proper supervision and monitoring");
            
        // System complexity assessment
        var uniqueDeviceTypes = deviceTypes.Count;
        if (uniqueDeviceTypes > 5)
            recommendations.Add($"Complex system: {uniqueDeviceTypes} different device types - comprehensive testing required");
            
        // Maintenance recommendations
        if (devices.Count > 100)
            recommendations.Add("Large system: Implement regular maintenance schedule and device testing protocol");
            
        return new IDNETSystemSummary
        {
            RecommendedNetworkChannels = segments.Count,
            RepeatersRequired = repeatersRequired,
            TotalWireLength = segments.Sum(s => s.EstimatedWireLength),
            SystemRecommendations = recommendations,
            IntegrationWithIDNAC = hasNotificationDevices,
            PowerSupplyRequirements = segments.Count <= 3 ? 
                $"Single ES-PS power supply supports {segments.Count} IDNET channel{(segments.Count > 1 ? "s" : "")}" :
                $"Multiple power supplies required: {Math.Ceiling(segments.Count / 3.0)} ES-PS units for {segments.Count} channels"
        };
    }

    private IDNETSystemResults CreateEmptyResults()
    {
        return new IDNETSystemResults
        {
            LevelAnalysis = new Dictionary<string, IDNETLevelAnalysis>(),
            AllDevices = new List<IDNETDevice>(),
            TotalDevices = 0,
            TotalPowerConsumption = 0,
            TotalUnitLoads = 0,
            NetworkSegments = new List<IDNETNetworkSegment>(),
            SystemSummary = new IDNETSystemSummary
            {
                RecommendedNetworkChannels = 0,
                RepeatersRequired = 0,
                TotalWireLength = 0,
                SystemRecommendations = new List<string> { "No IDNET devices detected" },
                IntegrationWithIDNAC = false,
                PowerSupplyRequirements = "No IDNET power requirements"
            },
            AnalysisTimestamp = DateTime.Now
        };
        }
    }
}