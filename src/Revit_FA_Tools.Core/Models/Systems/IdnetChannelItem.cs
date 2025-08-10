using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Model for IDNET channel analysis grid
    /// </summary>
    public class IdnetChannelItem : INotifyPropertyChanged
    {
        private string _channel = string.Empty;
        private string _segment = string.Empty;
        private string _panelTransponder = string.Empty;
        private int _totalDevices;
        private int _detectors;
        private int _modules;
        private int _points;
        private int _unitLoads;
        private double _utilizationPercent;
        private string _limitingFactor = "Devices";
        private int _channelsRequired;
        private double _cableLength;
        private string _loopRedundancy = string.Empty;
        private int _isolators;
        private string _validation = string.Empty;

        public string Channel
        {
            get => _channel;
            set { _channel = value; OnPropertyChanged(); }
        }

        public string Segment
        {
            get => _segment;
            set { _segment = value; OnPropertyChanged(); }
        }

        public string PanelTransponder
        {
            get => _panelTransponder;
            set { _panelTransponder = value; OnPropertyChanged(); }
        }

        public int TotalDevices
        {
            get => _totalDevices;
            set { _totalDevices = value; OnPropertyChanged(); UpdateUtilization(); }
        }

        public int Detectors
        {
            get => _detectors;
            set { _detectors = value; OnPropertyChanged(); }
        }

        public int Modules
        {
            get => _modules;
            set { _modules = value; OnPropertyChanged(); }
        }

        public int Points
        {
            get => _points;
            set { _points = value; OnPropertyChanged(); UpdateUtilization(); }
        }

        public int UnitLoads
        {
            get => _unitLoads;
            set { _unitLoads = value; OnPropertyChanged(); UpdateUtilization(); }
        }

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

        public int ChannelsRequired
        {
            get => _channelsRequired;
            set { _channelsRequired = value; OnPropertyChanged(); }
        }

        public double CableLength
        {
            get => _cableLength;
            set { _cableLength = value; OnPropertyChanged(); }
        }

        public string LoopRedundancy
        {
            get => _loopRedundancy;
            set { _loopRedundancy = value; OnPropertyChanged(); }
        }

        public int Isolators
        {
            get => _isolators;
            set { _isolators = value; OnPropertyChanged(); }
        }

        public string Validation
        {
            get => _validation;
            set { _validation = value; OnPropertyChanged(); }
        }

        // IDNET typical limits
        private const int MaxDevicesPerChannel = 127;
        private const int MaxPointsPerChannel = 250;
        private const int MaxUnitLoadsPerChannel = 127;

        private void UpdateUtilization()
        {
            double deviceUtilization = (TotalDevices / (double)MaxDevicesPerChannel) * 100;
            double pointUtilization = (Points / (double)MaxPointsPerChannel) * 100;
            double unitLoadUtilization = (UnitLoads / (double)MaxUnitLoadsPerChannel) * 100;

            UtilizationPercent = Math.Max(Math.Max(deviceUtilization, pointUtilization), unitLoadUtilization);

            // Determine limiting factor
            if (deviceUtilization >= pointUtilization && deviceUtilization >= unitLoadUtilization)
            {
                LimitingFactor = "Devices";
            }
            else if (pointUtilization >= unitLoadUtilization)
            {
                LimitingFactor = "Points";
            }
            else
            {
                LimitingFactor = "Unit Loads";
            }

            // Calculate channels required
            int channelsByDevices = (int)Math.Ceiling(TotalDevices / (double)MaxDevicesPerChannel);
            int channelsByPoints = (int)Math.Ceiling(Points / (double)MaxPointsPerChannel);
            int channelsByUnitLoads = (int)Math.Ceiling(UnitLoads / (double)MaxUnitLoadsPerChannel);

            ChannelsRequired = Math.Max(Math.Max(channelsByDevices, channelsByPoints), channelsByUnitLoads);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}