using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Model for IDNAC circuit analysis grid
    /// </summary>
    public class IdnacCircuitItem : INotifyPropertyChanged
    {
        private string _panel = string.Empty;
        private string _branch = string.Empty;
        private string _name = string.Empty;
        private int _devices;
        private double _totalCurrent;
        private double _totalWattage;
        private int _totalUnitLoads;
        private double _utilizationPercent;
        private double _voltageDropPercent;
        private string _limitingFactor = string.Empty;
        private double _length;
        private string _wireGauge = string.Empty;
        private string _eol = string.Empty;
        private bool _hasIsolator;
        private bool _hasRepeater;
        private string _validation = string.Empty;

        public string Panel
        {
            get => _panel;
            set { _panel = value; OnPropertyChanged(); }
        }

        public string Branch
        {
            get => _branch;
            set { _branch = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int Devices
        {
            get => _devices;
            set { _devices = value; OnPropertyChanged(); }
        }

        public double TotalCurrent
        {
            get => _totalCurrent;
            set { _totalCurrent = value; OnPropertyChanged(); UpdateUtilization(); }
        }

        public double TotalWattage
        {
            get => _totalWattage;
            set { _totalWattage = value; OnPropertyChanged(); }
        }

        public int TotalUnitLoads
        {
            get => _totalUnitLoads;
            set { _totalUnitLoads = value; OnPropertyChanged(); }
        }

        public double UtilizationPercent
        {
            get => _utilizationPercent;
            set { _utilizationPercent = value; OnPropertyChanged(); }
        }

        public double VoltageDropPercent
        {
            get => _voltageDropPercent;
            set { _voltageDropPercent = value; OnPropertyChanged(); }
        }

        public string LimitingFactor
        {
            get => _limitingFactor;
            set { _limitingFactor = value; OnPropertyChanged(); }
        }

        public double Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); UpdateVoltageDropPercent(); }
        }

        public string WireGauge
        {
            get => _wireGauge;
            set { _wireGauge = value; OnPropertyChanged(); UpdateVoltageDropPercent(); }
        }

        public string EOL
        {
            get => _eol;
            set { _eol = value; OnPropertyChanged(); }
        }

        public bool HasIsolator
        {
            get => _hasIsolator;
            set { _hasIsolator = value; OnPropertyChanged(); }
        }

        public bool HasRepeater
        {
            get => _hasRepeater;
            set { _hasRepeater = value; OnPropertyChanged(); }
        }

        public string Validation
        {
            get => _validation;
            set { _validation = value; OnPropertyChanged(); }
        }

        // Maximum current capacity (typically 2.5A for NAC circuits)
        private const double MaxCurrentCapacity = 2.5;

        private void UpdateUtilization()
        {
            if (MaxCurrentCapacity > 0)
            {
                UtilizationPercent = (TotalCurrent / MaxCurrentCapacity) * 100;
                UpdateLimitingFactor();
            }
        }

        private void UpdateVoltageDropPercent()
        {
            // Simplified voltage drop calculation
            // In a real implementation, this would use wire resistance tables
            if (Length > 0 && TotalCurrent > 0)
            {
                double wireResistance = GetWireResistance(WireGauge);
                double voltageDrop = 2 * wireResistance * Length * TotalCurrent / 1000; // 2x for round trip
                VoltageDropPercent = (voltageDrop / 24.0) * 100; // Assuming 24V system
            }
        }

        private double GetWireResistance(string gauge)
        {
            // Ohms per 1000 feet of copper wire
            return gauge switch
            {
                "12 AWG" => 1.98,
                "14 AWG" => 3.14,
                "16 AWG" => 4.99,
                "18 AWG" => 7.95,
                _ => 3.14 // Default to 14 AWG
            };
        }

        private void UpdateLimitingFactor()
        {
            if (UtilizationPercent >= 90)
                LimitingFactor = "Current";
            else if (VoltageDropPercent >= 10)
                LimitingFactor = "Voltage Drop";
            else if (TotalUnitLoads >= 40)
                LimitingFactor = "Unit Loads";
            else
                LimitingFactor = "None";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}