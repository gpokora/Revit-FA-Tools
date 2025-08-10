using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit_FA_Tools.Converters
{
    /// <summary>
    /// Converts utilization percentage to color for visual indication
    /// </summary>
    public class UtilizationToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double utilization)
            {
                if (utilization >= 90)
                    return new SolidColorBrush(Colors.Red);
                else if (utilization >= 80)
                    return new SolidColorBrush(Colors.Orange);
                else if (utilization >= 70)
                    return new SolidColorBrush(Colors.Gold);
                else
                    return new SolidColorBrush(Colors.Green);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts utilization percentage to validation status string for icon selection
    /// </summary>
    public class UtilizationToValidationStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double utilization)
            {
                if (utilization >= 90)
                    return "Critical";
                else if (utilization >= 80)
                    return "Warning";
                else
                    return "Good";
            }
            return "Good";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts validation status to appropriate icon
    /// </summary>
    public class StatusToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                switch (status?.ToLower())
                {
                    case "valid":
                    case "success":
                    case "ok":
                        return "{dx:DXImage SvgImages/Icon Builder/Actions_CheckCircled.svg}";
                    case "warning":
                    case "attention":
                        return "{dx:DXImage SvgImages/Icon Builder/Actions_Warning.svg}";
                    case "error":
                    case "invalid":
                    case "fail":
                        return "{dx:DXImage SvgImages/Icon Builder/Actions_Error.svg}";
                    case "info":
                        return "{dx:DXImage SvgImages/Icon Builder/Actions_Info.svg}";
                    default:
                        return "{dx:DXImage SvgImages/Icon Builder/Actions_Question.svg}";
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to visibility
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts comments text to boolean indicating if content exists
    /// </summary>
    public class CommentsToHasContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts boolean value
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}