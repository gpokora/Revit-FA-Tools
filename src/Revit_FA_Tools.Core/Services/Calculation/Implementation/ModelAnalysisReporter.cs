using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Comprehensive model analysis tool for extracting information needed for parameter extraction
    /// </summary>
    public class ModelAnalysisReporter
    {
        private Document _document;

        public ModelAnalysisReporter(Document document)
        {
            _document = document;
        }

        /// <summary>
        /// Performs comprehensive analysis of the Revit model and generates detailed report
        /// </summary>
        public ModelAnalysisReport AnalyzeModel()
        {
            var report = new ModelAnalysisReport
            {
                ProjectName = _document.ProjectInformation?.Name ?? "Unknown Project",
                AnalysisTimestamp = DateTime.Now
            };

            try
            {
                System.Diagnostics.Debug.WriteLine("Starting comprehensive model analysis...");

                // 1. Analyze all family instances
                AnalyzeFamilyInstances(report);

                // 2. Analyze categories
                AnalyzeCategories(report);

                // 3. Analyze parameters across the model
                AnalyzeParameters(report);

                // 4. Analyze levels
                AnalyzeLevels(report);

                // 5. Analyze families and types
                AnalyzeFamiliesAndTypes(report);

                // 6. Generate recommendations
                GenerateRecommendations(report);

                System.Diagnostics.Debug.WriteLine($"Model analysis complete. Found {report.FamilyInstanceSummary.Count} family types.");

            }
            catch (Exception ex)
            {
                report.Errors.Add($"Analysis error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Model analysis error: {ex.Message}");
            }

            return report;
        }

        /// <summary>
        /// Analyzes all family instances in the model
        /// </summary>
        private void AnalyzeFamilyInstances(ModelAnalysisReport report)
        {
            var collector = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            var instanceSummary = new Dictionary<string, FamilyInstanceAnalysis>();

            foreach (var instance in collector)
            {
                try
                {
                    var familyName = instance.Symbol?.Family?.Name ?? "Unknown Family";
                    var typeName = instance.Symbol?.Name ?? "Unknown Type";
                    var categoryName = instance.Category?.Name ?? "Unknown Category";
                    var key = $"{familyName}:{typeName}";

                    if (!instanceSummary.ContainsKey(key))
                    {
                        instanceSummary[key] = new FamilyInstanceAnalysis
                        {
                            FamilyName = familyName,
                            TypeName = typeName,
                            CategoryName = categoryName,
                            Count = 0,
                            SampleElementId = instance.Id.Value.ToString(),
                            InstanceParameters = new List<ParameterInfo>(),
                            TypeParameters = new List<ParameterInfo>()
                        };

                        // Analyze instance parameters
                        AnalyzeElementParameters(instance, instanceSummary[key].InstanceParameters, "Instance");

                        // Analyze type parameters  
                        if (instance.Symbol != null)
                        {
                            AnalyzeElementParameters(instance.Symbol, instanceSummary[key].TypeParameters, "Type");
                        }
                    }

                    instanceSummary[key].Count++;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Error analyzing family instance: {ex.Message}");
                }
            }

            report.FamilyInstanceSummary = instanceSummary;
            report.TotalFamilyInstances = instanceSummary.Values.Sum(f => f.Count);
        }

        /// <summary>
        /// Analyzes parameters of a specific element
        /// </summary>
        private void AnalyzeElementParameters(Element element, List<ParameterInfo> parameterList, string source)
        {
            if (element?.Parameters == null) return;

            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    var paramInfo = new ParameterInfo
                    {
                        Name = param.Definition?.Name ?? "Unknown Parameter",
                        StorageType = param.StorageType.ToString(),
                        IsReadOnly = param.IsReadOnly,
                        HasValue = param.HasValue,
                        Source = source,
                        GroupName = param.Definition?.GetGroupTypeId()?.ToString() ?? "Unknown Group"
                    };

                    // Get sample value
                    if (param.HasValue)
                    {
                        try
                        {
                            switch (param.StorageType)
                            {
                                case StorageType.String:
                                    paramInfo.SampleValue = param.AsString() ?? "";
                                    paramInfo.SampleValueString = param.AsValueString() ?? "";
                                    break;
                                case StorageType.Double:
                                    paramInfo.SampleValue = param.AsDouble().ToString("F3");
                                    paramInfo.SampleValueString = param.AsValueString() ?? "";
                                    break;
                                case StorageType.Integer:
                                    paramInfo.SampleValue = param.AsInteger().ToString();
                                    paramInfo.SampleValueString = param.AsValueString() ?? "";
                                    break;
                                case StorageType.ElementId:
                                    paramInfo.SampleValue = param.AsElementId()?.Value.ToString() ?? "";
                                    paramInfo.SampleValueString = param.AsValueString() ?? "";
                                    break;
                                default:
                                    paramInfo.SampleValue = "Unknown Type";
                                    paramInfo.SampleValueString = "";
                                    break;
                            }
                        }
                        catch
                        {
                            paramInfo.SampleValue = "Error reading value";
                            paramInfo.SampleValueString = "";
                        }
                    }

                    parameterList.Add(paramInfo);
                }
                catch (Exception ex)
                {
                    // Skip problematic parameters
                    System.Diagnostics.Debug.WriteLine($"Error analyzing parameter: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Analyzes all categories in the model
        /// </summary>
        private void AnalyzeCategories(ModelAnalysisReport report)
        {
            var categories = _document.Settings.Categories;
            var categorySummary = new List<CategoryInfo>();

            foreach (Category category in categories)
            {
                try
                {
                    if (category == null) continue;

                    var categoryInfo = new CategoryInfo
                    {
                        Name = category.Name,
                        Id = (int)category.Id.Value,
                        CategoryType = category.CategoryType.ToString(),
                        HasMaterial = category.HasMaterialQuantities,
                        CanAddSubcategory = category.CanAddSubcategory,
                        IsHidden = category.get_Visible(_document.ActiveView) == false
                    };

                    // Count elements in this category
                    try
                    {
                        var collector = new FilteredElementCollector(_document)
                            .OfCategoryId(category.Id);
                        categoryInfo.ElementCount = collector.GetElementCount();
                    }
                    catch
                    {
                        categoryInfo.ElementCount = 0;
                    }

                    categorySummary.Add(categoryInfo);
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Error analyzing category: {ex.Message}");
                }
            }

            report.CategorySummary = categorySummary.OrderByDescending(c => c.ElementCount).ToList();
        }

        /// <summary>
        /// Analyzes all unique parameters found in the model
        /// </summary>
        private void AnalyzeParameters(ModelAnalysisReport report)
        {
            var parameterSummary = new Dictionary<string, ParameterStatistics>();

            foreach (var familyAnalysis in report.FamilyInstanceSummary.Values)
            {
                // Process instance parameters
                foreach (var param in familyAnalysis.InstanceParameters)
                {
                    UpdateParameterStatistics(parameterSummary, param, familyAnalysis.FamilyName, "Instance");
                }

                // Process type parameters
                foreach (var param in familyAnalysis.TypeParameters)
                {
                    UpdateParameterStatistics(parameterSummary, param, familyAnalysis.FamilyName, "Type");
                }
            }

            report.ParameterStatistics = parameterSummary.Values
                .OrderByDescending(p => p.FamilyCount)
                .ToList();
        }

        /// <summary>
        /// Updates parameter usage statistics
        /// </summary>
        private void UpdateParameterStatistics(Dictionary<string, ParameterStatistics> stats, 
                                             ParameterInfo param, string familyName, string source)
        {
            if (!stats.ContainsKey(param.Name))
            {
                stats[param.Name] = new ParameterStatistics
                {
                    ParameterName = param.Name,
                    StorageType = param.StorageType,
                    GroupName = param.GroupName,
                    FamilyCount = 0,
                    SampleValues = new HashSet<string>(),
                    FoundInFamilies = new List<string>(),
                    Sources = new HashSet<string>()
                };
            }

            var stat = stats[param.Name];
            if (!stat.FoundInFamilies.Contains(familyName))
            {
                stat.FamilyCount++;
                stat.FoundInFamilies.Add(familyName);
            }

            stat.Sources.Add(source);
            if (!string.IsNullOrEmpty(param.SampleValue))
            {
                stat.SampleValues.Add(param.SampleValue);
            }
        }

        /// <summary>
        /// Analyzes all levels in the model
        /// </summary>
        private void AnalyzeLevels(ModelAnalysisReport report)
        {
            var collector = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>();

            var levelSummary = new List<LevelInfo>();

            foreach (var level in collector)
            {
                try
                {
                    var levelInfo = new LevelInfo
                    {
                        Name = level.Name,
                        Elevation = level.Elevation,
                        Id = (int)level.Id.Value
                    };

                    // Count family instances on this level
                    try
                    {
                        var instancesOnLevel = new FilteredElementCollector(_document)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(fi => fi.LevelId == level.Id)
                            .Count();

                        levelInfo.FamilyInstanceCount = instancesOnLevel;
                    }
                    catch
                    {
                        levelInfo.FamilyInstanceCount = 0;
                    }

                    levelSummary.Add(levelInfo);
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Error analyzing level: {ex.Message}");
                }
            }

            report.LevelSummary = levelSummary.OrderBy(l => l.Elevation).ToList();
        }

        /// <summary>
        /// Analyzes families and family symbols/types
        /// </summary>
        private void AnalyzeFamiliesAndTypes(ModelAnalysisReport report)
        {
            var collector = new FilteredElementCollector(_document)
                .OfClass(typeof(Family))
                .Cast<Family>();

            var familySummary = new List<FamilyInfo>();

            foreach (var family in collector)
            {
                try
                {
                    var familyInfo = new FamilyInfo
                    {
                        Name = family.Name,
                        Id = (int)family.Id.Value,
                        CategoryName = family.FamilyCategory?.Name ?? "Unknown Category",
                        IsInPlace = family.IsInPlace,
                        IsParametric = family.IsParametric,
                        FamilyPlacementType = family.FamilyPlacementType.ToString()
                    };

                    // Get family symbols (types)
                    var familySymbolIds = family.GetFamilySymbolIds();
                    familyInfo.TypeCount = familySymbolIds.Count;

                    var typeNames = new List<string>();
                    foreach (var symbolId in familySymbolIds)
                    {
                        var symbol = _document.GetElement(symbolId) as FamilySymbol;
                        if (symbol != null)
                        {
                            typeNames.Add(symbol.Name);
                        }
                    }
                    familyInfo.TypeNames = typeNames;

                    // Count instances of this family
                    var instanceCount = new FilteredElementCollector(_document)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Symbol?.Family?.Id == family.Id)
                        .Count();

                    familyInfo.InstanceCount = instanceCount;

                    familySummary.Add(familyInfo);
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Error analyzing family: {ex.Message}");
                }
            }

            report.FamilySummary = familySummary.OrderByDescending(f => f.InstanceCount).ToList();
        }

        /// <summary>
        /// Generates recommendations based on analysis
        /// </summary>
        private void GenerateRecommendations(ModelAnalysisReport report)
        {
            var recommendations = new List<string>();

            // Fire alarm parameter recommendations
            var fireAlarmParams = new[] { "Wattage", "CURRENT DRAW", "Current", "Power", "Voltage", "CANDELA", "Candela" };
            var foundFireAlarmParams = report.ParameterStatistics
                .Where(p => fireAlarmParams.Contains(p.ParameterName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            recommendations.Add($"Found {foundFireAlarmParams.Count} fire alarm related parameters out of {fireAlarmParams.Length} searched");

            foreach (var param in foundFireAlarmParams)
            {
                recommendations.Add($"  - '{param.ParameterName}' found in {param.FamilyCount} families ({param.StorageType})");
            }

            // Detection device recommendations
            var possibleDetectionFamilies = report.FamilyInstanceSummary.Values
                .Where(f => IsLikelyFireAlarmDevice(f.FamilyName) || IsLikelyFireAlarmDevice(f.CategoryName))
                .ToList();

            recommendations.Add($"Found {possibleDetectionFamilies.Count} potential fire alarm device families:");
            foreach (var family in possibleDetectionFamilies.Take(10))
            {
                recommendations.Add($"  - {family.FamilyName} ({family.Count} instances) - Category: {family.CategoryName}");
            }

            // Parameter extraction recommendations
            var criticalParams = report.ParameterStatistics
                .Where(p => p.FamilyCount > 1 && (p.ParameterName.ToUpper().Contains("CURRENT") || 
                           p.ParameterName.ToUpper().Contains("WATTAGE") || 
                           p.ParameterName.ToUpper().Contains("POWER")))
                .ToList();

            if (criticalParams.Any())
            {
                recommendations.Add("Critical electrical parameters found:");
                foreach (var param in criticalParams)
                {
                    recommendations.Add($"  - '{param.ParameterName}' ({param.StorageType}) in {param.FamilyCount} families");
                    if (param.SampleValues.Any())
                    {
                        recommendations.Add($"    Sample values: {string.Join(", ", param.SampleValues.Take(3))}");
                    }
                }
            }

            report.Recommendations = recommendations;
        }

        /// <summary>
        /// Determines if a name suggests a fire alarm device
        /// </summary>
        private bool IsLikelyFireAlarmDevice(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            
            var upperName = name.ToUpperInvariant();
            var fireAlarmKeywords = new[]
            {
                "SMOKE", "HEAT", "DETECTOR", "STROBE", "HORN", "SPEAKER", "NOTIFICATION",
                "FIRE", "ALARM", "MANUAL", "PULL", "STATION", "MODULE", "ADDRESSABLE",
                "DUCT", "BEAM", "ASPIRATING", "VESDA", "CANDELA", "DECIBEL"
            };

            return fireAlarmKeywords.Any(keyword => upperName.Contains(keyword));
        }

        /// <summary>
        /// Exports the analysis report to a detailed text file
        /// </summary>
        public void ExportReport(ModelAnalysisReport report, string filePath)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"=== REVIT MODEL ANALYSIS REPORT ===");
            sb.AppendLine($"Project: {report.ProjectName}");
            sb.AppendLine($"Analysis Date: {report.AnalysisTimestamp}");
            sb.AppendLine($"Total Family Instances: {report.TotalFamilyInstances}");
            sb.AppendLine();

            // Family Instance Summary
            sb.AppendLine("=== FAMILY INSTANCE SUMMARY ===");
            sb.AppendLine($"{"Family Name",-40} {"Type Name",-30} {"Category",-20} {"Count",-8} {"Sample Element ID",-15}");
            sb.AppendLine(new string('-', 120));

            foreach (var family in report.FamilyInstanceSummary.Values.OrderByDescending(f => f.Count).Take(50))
            {
                sb.AppendLine($"{family.FamilyName,-40} {family.TypeName,-30} {family.CategoryName,-20} {family.Count,-8} {family.SampleElementId,-15}");
            }
            sb.AppendLine();

            // Parameter Statistics
            sb.AppendLine("=== PARAMETER STATISTICS ===");
            sb.AppendLine($"{"Parameter Name",-40} {"Storage Type",-15} {"Family Count",-12} {"Group",-25} {"Sample Values",-30}");
            sb.AppendLine(new string('-', 130));

            foreach (var param in report.ParameterStatistics.Take(100))
            {
                var sampleValues = string.Join(", ", param.SampleValues.Take(3));
                sb.AppendLine($"{param.ParameterName,-40} {param.StorageType,-15} {param.FamilyCount,-12} {param.GroupName,-25} {sampleValues,-30}");
            }
            sb.AppendLine();

            // Level Summary
            sb.AppendLine("=== LEVEL SUMMARY ===");
            sb.AppendLine($"{"Level Name",-30} {"Elevation",-15} {"Family Instances",-18}");
            sb.AppendLine(new string('-', 70));

            foreach (var level in report.LevelSummary)
            {
                sb.AppendLine($"{level.Name,-30} {level.Elevation,-15:F2} {level.FamilyInstanceCount,-18}");
            }
            sb.AppendLine();

            // Categories
            sb.AppendLine("=== CATEGORY SUMMARY (Top 20) ===");
            sb.AppendLine($"{"Category Name",-40} {"Element Count",-15} {"Category Type",-20}");
            sb.AppendLine(new string('-', 80));

            foreach (var category in report.CategorySummary.Take(20))
            {
                sb.AppendLine($"{category.Name,-40} {category.ElementCount,-15} {category.CategoryType,-20}");
            }
            sb.AppendLine();

            // Recommendations
            sb.AppendLine("=== RECOMMENDATIONS ===");
            foreach (var recommendation in report.Recommendations)
            {
                sb.AppendLine(recommendation);
            }
            sb.AppendLine();

            // Errors
            if (report.Errors.Any())
            {
                sb.AppendLine("=== ERRORS ENCOUNTERED ===");
                foreach (var error in report.Errors)
                {
                    sb.AppendLine($"ERROR: {error}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }

    #region Data Models

    public class ModelAnalysisReport
    {
        public string ProjectName { get; set; }
        public DateTime AnalysisTimestamp { get; set; }
        public int TotalFamilyInstances { get; set; }
        public Dictionary<string, FamilyInstanceAnalysis> FamilyInstanceSummary { get; set; } = new Dictionary<string, FamilyInstanceAnalysis>();
        public List<ParameterStatistics> ParameterStatistics { get; set; } = new List<ParameterStatistics>();
        public List<LevelInfo> LevelSummary { get; set; } = new List<LevelInfo>();
        public List<CategoryInfo> CategorySummary { get; set; } = new List<CategoryInfo>();
        public List<FamilyInfo> FamilySummary { get; set; } = new List<FamilyInfo>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class FamilyInstanceAnalysis
    {
        public string? FamilyName { get; set; }
        public string TypeName { get; set; }
        public string CategoryName { get; set; }
        public int Count { get; set; }
        public string SampleElementId { get; set; }
        public List<ParameterInfo> InstanceParameters { get; set; } = new List<ParameterInfo>();
        public List<ParameterInfo> TypeParameters { get; set; } = new List<ParameterInfo>();
    }

    public class ParameterInfo
    {
        public string Name { get; set; }
        public string StorageType { get; set; }
        public bool IsReadOnly { get; set; }
        public bool HasValue { get; set; }
        public string Source { get; set; } // Instance or Type
        public string GroupName { get; set; }
        public string SampleValue { get; set; }
        public string SampleValueString { get; set; } // AsValueString result
    }

    public class ParameterStatistics
    {
        public string ParameterName { get; set; }
        public string StorageType { get; set; }
        public string GroupName { get; set; }
        public int FamilyCount { get; set; }
        public HashSet<string> SampleValues { get; set; } = new HashSet<string>();
        public List<string> FoundInFamilies { get; set; } = new List<string>();
        public HashSet<string> Sources { get; set; } = new HashSet<string>(); // Instance, Type
    }

    public class LevelInfo
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
        public int Id { get; set; }
        public int FamilyInstanceCount { get; set; }
    }

    public class CategoryInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string CategoryType { get; set; }
        public int ElementCount { get; set; }
        public bool HasMaterial { get; set; }
        public bool CanAddSubcategory { get; set; }
        public bool IsHidden { get; set; }
    }

    public class FamilyInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public bool IsInPlace { get; set; }
        public bool IsParametric { get; set; }
        public string FamilyPlacementType { get; set; }
        public int TypeCount { get; set; }
        public int InstanceCount { get; set; }
        public List<string> TypeNames { get; set; } = new List<string>();
    }

    #endregion
}