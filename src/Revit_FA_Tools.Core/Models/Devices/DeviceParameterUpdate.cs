using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Core.Models.Devices
{
    /// <summary>
    /// Represents a parameter update for a device
    /// </summary>
    public class DeviceParameterUpdate
    {
        public ElementId ElementId { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public object NewValue { get; set; }
        public string OldValue { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}