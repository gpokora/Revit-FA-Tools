namespace Revit_FA_Tools.Core.Models.Analysis
{
    /// <summary>
    /// Results from circuit balancing calculations
    /// </summary>
    public class CircuitBalancingResults
    {
        public int CircuitsCreated { get; set; }
        public bool IsBalanced { get; set; }
        public double BalancingScore { get; set; }
        public string[] BalancingIssues { get; set; } = new string[0];
    }
}