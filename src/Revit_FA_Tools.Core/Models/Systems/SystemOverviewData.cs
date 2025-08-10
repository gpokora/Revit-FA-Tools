using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Data model for the System Overview by-level snapshot grid
    /// </summary>
    public class SystemOverviewData : INotifyPropertyChanged
    {
        private string _level = string.Empty;
        private double _elevation;
        private string _zone = string.Empty;
        private int _idnacDevices;
        private double _idnacCurrent;
        private double _idnacWattage;
        private int _idnacCircuits;
        private int _idnetDevices;
        private int _idnetPoints;
        private int _idnetUnitLoads;
        private int _idnetChannels;
        private double _utilizationPercent;
        private string _limitingFactor = string.Empty;
        private string _comments = string.Empty;

        public string Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(); }
        }

        public double Elevation
        {
            get => _elevation;
            set { _elevation = value; OnPropertyChanged(); }
        }

        public string Zone
        {
            get => _zone;
            set { _zone = value; OnPropertyChanged(); }
        }

        // IDNAC Properties
        public int IDNACDevices
        {
            get => _idnacDevices;
            set { _idnacDevices = value; OnPropertyChanged(); }
        }

        public double IDNACCurrent
        {
            get => _idnacCurrent;
            set { _idnacCurrent = value; OnPropertyChanged(); }
        }

        public double IDNACWattage
        {
            get => _idnacWattage;
            set { _idnacWattage = value; OnPropertyChanged(); }
        }

        public int IDNACCircuits
        {
            get => _idnacCircuits;
            set { _idnacCircuits = value; OnPropertyChanged(); }
        }

        // IDNET Properties
        public int IDNETDevices
        {
            get => _idnetDevices;
            set { _idnetDevices = value; OnPropertyChanged(); }
        }

        public int IDNETPoints
        {
            get => _idnetPoints;
            set { _idnetPoints = value; OnPropertyChanged(); }
        }

        public int IDNETUnitLoads
        {
            get => _idnetUnitLoads;
            set { _idnetUnitLoads = value; OnPropertyChanged(); }
        }

        public int IDNETChannels
        {
            get => _idnetChannels;
            set { _idnetChannels = value; OnPropertyChanged(); }
        }

        // Utilization Properties
        public double UtilizationPercent
        {
            get => _utilizationPercent;
            set { _utilizationPercent = value; OnPropertyChanged(); }
        }

        public string LimitingFactor
        {
            get => _limitingFactor;
            set { _limitingFactor = value; OnPropertyChanged(); }
        }

        public string Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(); }
        }

        // Additional properties for compatibility
        private int _totalDevices;
        private decimal _totalCurrent;
        private decimal _totalPower;
        private string _systemStatus;
        private int _idnacsRequired;
        private int _amplifiersRequired;

        public int TotalDevices
        {
            get => _totalDevices;
            set { _totalDevices = value; OnPropertyChanged(); }
        }

        public decimal TotalCurrent
        {
            get => _totalCurrent;
            set { _totalCurrent = value; OnPropertyChanged(); }
        }

        public decimal TotalPower
        {
            get => _totalPower;
            set { _totalPower = value; OnPropertyChanged(); }
        }

        public string SystemStatus
        {
            get => _systemStatus;
            set { _systemStatus = value; OnPropertyChanged(); }
        }

        public int IDNACsRequired
        {
            get => _idnacsRequired;
            set { _idnacsRequired = value; OnPropertyChanged(); }
        }

        public int AmplifiersRequired
        {
            get => _amplifiersRequired;
            set { _amplifiersRequired = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}