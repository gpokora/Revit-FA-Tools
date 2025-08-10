using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Revit_FA_Tools.ViewModels.Tree;

namespace Revit_FA_Tools.Converters
{
    /// <summary>
    /// Converter to show panel icon only for panel nodes
    /// </summary>
    public class PanelIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is PanelNodeVM ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to show branch icon only for branch nodes
    /// </summary>
    public class BranchIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is BranchNodeVM ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to show device icon only for device nodes
    /// </summary>
    public class DeviceIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is DeviceNodeVM ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to compare a value with a parameter and return true if greater
    /// </summary>
    public class GreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            if (double.TryParse(value.ToString(), out double val) && 
                double.TryParse(parameter.ToString(), out double threshold))
            {
                return val > threshold;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to compare a value with a parameter and return true if less
    /// </summary>
    public class LessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            if (double.TryParse(value.ToString(), out double val) && 
                double.TryParse(parameter.ToString(), out double threshold))
            {
                return val < threshold;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for formatting boolean values as Yes/No
    /// </summary>
    public class BooleanToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Yes" : "No";
            }

            return "No";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return string.Equals(stringValue, "Yes", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }

    /// <summary>
    /// Converter to format current values with proper units and color coding
    /// </summary>
    public class CurrentFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double currentValue)
            {
                if (currentValue < 0.001) // Less than 1mA
                    return "0A";
                else if (currentValue < 1.0) // Less than 1A
                    return $"{currentValue * 1000:F0}mA";
                else
                    return $"{currentValue:F2}A";
            }

            return "0A";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to format voltage drop with warning levels
    /// </summary>
    public class VoltageDropFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double voltageDropPct)
            {
                if (voltageDropPct <= 0)
                    return "-";
                else
                    return $"{voltageDropPct:F1}%";
            }

            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to format spare capacity with appropriate warnings
    /// </summary>
    public class SpareCapacityFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sparePct)
            {
                if (sparePct < 0)
                    return $"{Math.Abs(sparePct):F1}% OVER";
                else if (sparePct < 10)
                    return $"{sparePct:F1}% LOW";
                else
                    return $"{sparePct:F1}%";
            }

            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Multi-value converter for node type display text
    /// </summary>
    public class NodeTypeDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length >= 2 && values[0] is BaseNodeVM node)
            {
                switch (node)
                {
                    case PanelNodeVM panel:
                        return $"Panel {panel.Name} ({panel.Branches.Count} branches)";
                    case BranchNodeVM branch:
                        return $"Branch {branch.Name} ({branch.Devices.Count} devices)";
                    case DeviceNodeVM device:
                        return $"{device.Family} - {device.Type}";
                    default:
                        return node.Name;
                }
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}