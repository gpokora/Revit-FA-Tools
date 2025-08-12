namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Results from battery requirement calculations
    /// </summary>
    public class BatteryCalculationResults
    {
        public double RequiredCapacityAh { get; set; }
        public string RecommendedBatteryType { get; set; } = string.Empty;
        public double BackupTimeHours { get; set; }
        public bool IsAdequate { get; set; }
        public string[] Recommendations { get; set; } = new string[0];
    }
}