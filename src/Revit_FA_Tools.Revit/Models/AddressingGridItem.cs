using System.ComponentModel;
using System.Runtime.CompilerServices;
using Revit_FA_Tools.Core.Models.Devices;

namespace Revit_FA_Tools.Models
{
    /// <summary>
    /// Grid item for addressing panel
    /// </summary>
    public class AddressingGridItem : INotifyPropertyChanged
    {
        private int _address;
        private int _addressSlots;
        private string _lockState;
        private string _statusDescription;
        private string _validationMessage;

        public DeviceAssignment Assignment { get; set; }
        public int ElementId { get; set; }
        public string Level { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string BranchId { get; set; }

        public int Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged();
                }
            }
        }

        public int AddressSlots
        {
            get => _addressSlots;
            set
            {
                if (_addressSlots != value)
                {
                    _addressSlots = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LockState
        {
            get => _lockState;
            set
            {
                if (_lockState != value)
                {
                    _lockState = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (_statusDescription != value)
                {
                    _statusDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}