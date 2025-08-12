using System;
using System.Collections.Generic;

namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Amplifier requirements analysis results
    /// </summary>
    public class AmplifierRequirements
    {
        public double RequiredWattage { get; set; }
        public string RecommendedModel { get; set; } = string.Empty;
        public int RecommendedQuantity { get; set; } = 1;
        public double TotalCurrent { get; set; }
        public double EfficiencyFactor { get; set; } = 0.85; // Standard amplifier efficiency
        
        /// <summary>
        /// Load distribution
        /// </summary>
        public Dictionary<string, double> LoadByLevel { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> LoadByZone { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Backup requirements
        /// </summary>
        public double BackupWattage { get; set; }
        public bool RequiresRedundancy { get; set; }
        
        /// <summary>
        /// Analysis status
        /// </summary>
        public bool IsAdequate { get; set; } = true;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public DateTime AnalysisTime { get; set; } = DateTime.Now;
    }
}