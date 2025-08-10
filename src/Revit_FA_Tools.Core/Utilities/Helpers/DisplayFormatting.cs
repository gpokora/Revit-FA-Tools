using System;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Centralized display formatting for consistent decimal precision
    /// </summary>
    public static class DisplayFormatting
    {
        // Format strings for consistent decimal places
        public const string TOTAL_CURRENT_FORMAT = "F2";      // 2 decimal places: 1.25A
        public const string INDIVIDUAL_CURRENT_FORMAT = "F3"; // 3 decimal places: 0.125A  
        public const string TOTAL_WATTAGE_FORMAT = "F2";     // 2 decimal places: 15.50W
        public const string INDIVIDUAL_WATTAGE_FORMAT = "F2"; // 2 decimal places: 7.25W
        public const string PERCENTAGE_FORMAT = "F1";         // 1 decimal place: 80.5%
        public const string INTEGER_FORMAT = "F0";            // 0 decimal places: 15
        
        // Current formatting methods
        public static string FormatTotalCurrent(double current)
        {
            return $"{current.ToString(TOTAL_CURRENT_FORMAT)}A";
        }
        
        public static string FormatIndividualCurrent(double current)
        {
            return $"{current.ToString(INDIVIDUAL_CURRENT_FORMAT)}A";
        }
        
        public static string FormatStrobeCurrent(double current, bool isTotal = true)
        {
            var format = isTotal ? TOTAL_CURRENT_FORMAT : INDIVIDUAL_CURRENT_FORMAT;
            return $"{current.ToString(format)}A";
        }
        
        // Wattage formatting methods
        public static string FormatTotalWattage(double wattage)
        {
            return $"{wattage.ToString(TOTAL_WATTAGE_FORMAT)}W";
        }
        
        public static string FormatIndividualWattage(double wattage)
        {
            return $"{wattage.ToString(INDIVIDUAL_WATTAGE_FORMAT)}W";
        }
        
        public static string FormatSpeakerWattage(double wattage, bool isTotal = true)
        {
            // Both total and individual speaker wattage use 2 decimal places
            return $"{wattage.ToString(TOTAL_WATTAGE_FORMAT)}W";
        }
        
        // Percentage formatting
        public static string FormatPercentage(double percentage)
        {
            return $"{percentage.ToString(PERCENTAGE_FORMAT)}%";
        }
        
        // Count formatting
        public static string FormatCount(int count)
        {
            return count.ToString();
        }
        
        // Raw number formatting (without units)
        public static string FormatRawCurrent(double current, bool isTotal = true)
        {
            var format = isTotal ? TOTAL_CURRENT_FORMAT : INDIVIDUAL_CURRENT_FORMAT;
            return current.ToString(format);
        }
        
        public static string FormatRawWattage(double wattage, bool isTotal = true)
        {
            // Both total and individual wattage use 2 decimal places
            return wattage.ToString(TOTAL_WATTAGE_FORMAT);
        }
    }
}