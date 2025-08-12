using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// IDNET device information
    /// </summary>
    public class IDNETDevice
    {
        public string ElementId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        
        // IDNET specific properties
        public string NetworkSegment { get; set; } = string.Empty;
        public int Address { get; set; }
        public string Protocol { get; set; } = "IDNET";
        public string Status { get; set; } = "Active";
        
        // Performance metrics
        public double ResponseTime { get; set; }
        public double SignalStrength { get; set; }
        public DateTime LastCommunication { get; set; } = DateTime.Now;
        
        // Device capabilities
        public List<string> SupportedFeatures { get; set; } = new List<string>();
        public string FirmwareVersion { get; set; } = string.Empty;
        public Dictionary<string, object> DeviceConfiguration { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// IDNET level analysis results
    /// </summary>
    public class IDNETLevelAnalysis
    {
        public string Level { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public List<IDNETDevice> Devices { get; set; } = new List<IDNETDevice>();
        public int NetworkSegments { get; set; }
        public double AverageResponseTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// IDNET network segment information
    /// </summary>
    public class IDNETNetworkSegment
    {
        public string SegmentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<IDNETDevice> Devices { get; set; } = new List<IDNETDevice>();
        public int MaxDevices { get; set; } = 159; // IDNET maximum
        public double Utilization => MaxDevices > 0 ? (double)Devices.Count / MaxDevices : 0;
        
        // Network performance
        public double AverageResponseTime { get; set; }
        public double NetworkLoad { get; set; }
        public string Status { get; set; } = "Active";
        
        // Physical properties
        public double CableLength { get; set; }
        public string CableType { get; set; } = string.Empty;
        public List<string> IsolatorModules { get; set; } = new List<string>();
    }


    /// <summary>
    /// IDNET analysis grid display item
    /// </summary>
    public class IDNETAnalysisGridItem
    {
        public string Level { get; set; } = string.Empty;
        public int Devices { get; set; }
        public int Segments { get; set; }
        public double Utilization { get; set; }
        public string UtilizationDisplay => $"{Utilization:P1}";
        public double ResponseTime { get; set; }
        public string ResponseTimeDisplay => $"{ResponseTime:F2}ms";
        public string Status { get; set; } = string.Empty;
        
        // Visual properties
        public System.Windows.Media.Brush? StatusColor { get; set; }
        public string StatusIcon { get; set; } = string.Empty;
        public System.Windows.Media.Brush? UtilizationColor { get; set; }
    }
}