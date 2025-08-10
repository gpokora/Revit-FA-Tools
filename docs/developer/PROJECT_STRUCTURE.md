# IDNAC Calculator Project Restructuring Instructions

## Overview
These instructions will reorganize the existing IDNAC Calculator project into an optimal folder structure without deleting any files or creating new elements. All existing files will be preserved and moved to appropriate locations.

## Prerequisites
- Ensure you have a complete backup of the current project
- Close Visual Studio and any file explorers with the project open
- Run these operations from the project root directory

## Phase 1: Create New Folder Structure

### 1.1 Create Core Source Structure
```bash
# Create main source directories
mkdir -p src/Revit_FA_Tools.Core/Models/Devices
mkdir -p src/Revit_FA_Tools.Core/Models/Systems  
mkdir -p src/Revit_FA_Tools.Core/Models/Calculations
mkdir -p src/Revit_FA_Tools.Core/Models/Addressing
mkdir -p src/Revit_FA_Tools.Core/Models/Configuration

mkdir -p src/Revit_FA_Tools.Core/Services/Calculation/Interfaces
mkdir -p src/Revit_FA_Tools.Core/Services/Calculation/Implementation
mkdir -p src/Revit_FA_Tools.Core/Services/ParameterMapping/Interfaces
mkdir -p src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation
mkdir -p src/Revit_FA_Tools.Core/Services/Engineering/Interfaces
mkdir -p src/Revit_FA_Tools.Core/Services/Engineering/Implementation
mkdir -p src/Revit_FA_Tools.Core/Services/Integration
mkdir -p src/Revit_FA_Tools.Core/Services/Addressing
mkdir -p src/Revit_FA_Tools.Core/Services/Reporting/Interfaces
mkdir -p src/Revit_FA_Tools.Core/Services/Reporting/Implementation

mkdir -p src/Revit_FA_Tools.Core/Utilities/Extensions
mkdir -p src/Revit_FA_Tools.Core/Utilities/Helpers
mkdir -p src/Revit_FA_Tools.Core/Utilities/Constants
mkdir -p src/Revit_FA_Tools.Core/Exceptions
```

### 1.2 Create Revit Add-in Structure  
```bash
# Create Revit-specific directories
mkdir -p src/Revit_FA_Tools.Revit/Commands
mkdir -p src/Revit_FA_Tools.Revit/UI/Views/Main
mkdir -p src/Revit_FA_Tools.Revit/UI/Views/Analysis
mkdir -p src/Revit_FA_Tools.Revit/UI/Views/Addressing
mkdir -p src/Revit_FA_Tools.Revit/UI/ViewModels/Base
mkdir -p src/Revit_FA_Tools.Revit/UI/ViewModels/Main
mkdir -p src/Revit_FA_Tools.Revit/UI/ViewModels/Addressing
mkdir -p src/Revit_FA_Tools.Revit/UI/Controls
mkdir -p src/Revit_FA_Tools.Revit/UI/Converters
mkdir -p src/Revit_FA_Tools.Revit/Services/Revit/Interfaces
mkdir -p src/Revit_FA_Tools.Revit/Services/Revit/Implementation
mkdir -p src/Revit_FA_Tools.Revit/Services/Import
mkdir -p src/Revit_FA_Tools.Revit/Services/Export
mkdir -p src/Revit_FA_Tools.Revit/Configuration
mkdir -p src/Revit_FA_Tools.Revit/Resources/Images
mkdir -p src/Revit_FA_Tools.Revit/Resources/Styles
```

### 1.3 Create Data and Documentation Structure
```bash
# Create data and documentation directories
mkdir -p data/DeviceCatalogs
mkdir -p data/Configurations
mkdir -p data/Templates/Reports
mkdir -p data/Templates/Export

mkdir -p docs/user-guide
mkdir -p docs/developer
mkdir -p docs/specifications

mkdir -p tools/build
mkdir -p tools/testing
mkdir -p tools/code-quality

mkdir -p resources/icons
mkdir -p resources/themes
```

## Phase 2: Move Existing Files

### 2.1 Move Core Business Logic Files

#### Move Models
```bash
# Move existing model files to Core/Models
mv Models/DeviceModels.cs src/Revit_FA_Tools.Core/Models/Devices/
mv Models/DeviceSnapshotExtensions.cs src/Revit_FA_Tools.Core/Models/Devices/
mv Models/SystemOverviewData.cs src/Revit_FA_Tools.Core/Models/Systems/
mv Models/EquipmentNode.cs src/Revit_FA_Tools.Core/Models/Systems/
mv Models/IdnacCircuitItem.cs src/Revit_FA_Tools.Core/Models/Systems/
mv Models/IdnetChannelItem.cs src/Revit_FA_Tools.Core/Models/Systems/

# Move addressing models
mv Models/Addressing/AddressingSystem.cs src/Revit_FA_Tools.Core/Models/Addressing/
mv Models/Addressing/SmartDeviceNode.cs src/Revit_FA_Tools.Core/Models/Addressing/
```

#### Move Core Calculation Services
```bash
# Move calculation engines to Core/Services/Calculation/Implementation
mv IDNACAnalyzer.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
mv IDNETAnalyzer.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
mv AmplifierCalculator.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
mv ElectricalCalculator.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
mv PanelPlacementAnalyzer.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
mv ModelAnalysisReporter.cs src/Revit_FA_Tools.Core/Services/Calculation/Implementation/
```

#### Move Parameter Mapping Services
```bash
# Move parameter mapping services
mv Services/ParameterMapping/ParameterMappingEngine.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
mv Services/ParameterMapping/DeviceRepositoryService.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
mv Services/ParameterMapping/AdvancedParameterMappingService.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
mv Services/ParameterMapping/ParameterExtractor.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
mv Services/ParameterMapping/EnhancedValidationEngine.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
mv Services/ParameterMapping/PerformanceOptimizationService.cs src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/
```

#### Move Engineering Services
```bash
# Move engineering services
mv Services/CircuitBalancingService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/CircuitOrganizationService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/ValidationService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/PowerSupplyCalculationService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/CableCalculationService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/VoltageDropCalculator.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/BatteryCalculator.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/CircuitBalancer.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
```

#### Move Integration Services
```bash
# Move integration services
mv Services/Integration/ComprehensiveEngineeringService.cs src/Revit_FA_Tools.Core/Services/Integration/
mv Services/Integration/ParameterMappingIntegrationService.cs src/Revit_FA_Tools.Core/Services/Integration/
mv Services/Integration/IntegrationTestService.cs src/Revit_FA_Tools.Core/Services/Integration/
```

#### Move Addressing Services
```bash
# Move addressing services
mv Services/DeviceAddressingService.cs src/Revit_FA_Tools.Core/Services/Addressing/
mv Services/Addressing/AddressPoolManager.cs src/Revit_FA_Tools.Core/Services/Addressing/
mv Services/Addressing/ValidationEngine.cs src/Revit_FA_Tools.Core/Services/Addressing/
```

#### Move Reporting Services
```bash
# Move reporting services
mv Services/ReportingService.cs src/Revit_FA_Tools.Core/Services/Reporting/Implementation/
mv Services/ReportBuilder.cs src/Revit_FA_Tools.Core/Services/Reporting/Implementation/
```

#### Move Other Core Services
```bash
# Move remaining core services
mv Services/ConfigurationManagementService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/IDNACEngineeringEngine.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/AssignmentStore.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/ModelSyncService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/PendingChangesService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/RiserSyncService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/TreeAssignmentService.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
mv Services/UndoStack.cs src/Revit_FA_Tools.Core/Services/Engineering/Implementation/
```

#### Move Utility Files
```bash
# Move utility and helper files
mv DictionaryExtensions.cs src/Revit_FA_Tools.Core/Utilities/Extensions/
mv DisplayFormatting.cs src/Revit_FA_Tools.Core/Utilities/Helpers/
mv AnalysisServices.cs src/Revit_FA_Tools.Core/Utilities/Helpers/
mv IsExternalInitPolyfill.cs src/Revit_FA_Tools.Core/Utilities/
```

### 2.2 Move Revit-Specific Files

#### Move Application and Commands
```bash
# Move Revit application and command files
mv Application.cs src/Revit_FA_Tools.Revit/
mv Command.cs src/Revit_FA_Tools.Revit/Commands/
```

#### Move UI Components
```bash
# Move main UI files
mv DXRibbonWindow1.xaml src/Revit_FA_Tools.Revit/UI/Views/Main/MainWindow.xaml
mv DXRibbonWindow1.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Main/MainWindow.xaml.cs
mv DXRibbonWindow1_MainContent.xaml src/Revit_FA_Tools.Revit/UI/Views/Main/
mv DXRibbonWindow1_NewUI.xaml src/Revit_FA_Tools.Revit/UI/Views/Main/

# Move addressing UI files
mv Views/Addressing/DeviceAddressingWindow.xaml src/Revit_FA_Tools.Revit/UI/Views/Addressing/
mv Views/Addressing/DeviceAddressingWindow.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Addressing/
mv Views/AssignmentTreeEditor.xaml src/Revit_FA_Tools.Revit/UI/Views/Addressing/
mv Views/AssignmentTreeEditor.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Addressing/

# Move additional view files if they exist
if [ -f "Views/AssignmentTreeView.xaml" ]; then
    mv Views/AssignmentTreeView.xaml src/Revit_FA_Tools.Revit/UI/Views/Addressing/
    mv Views/AssignmentTreeView.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Addressing/
fi

if [ -f "Views/MappingEditor.xaml" ]; then
    mv Views/MappingEditor.xaml src/Revit_FA_Tools.Revit/UI/Views/Addressing/
    mv Views/MappingEditor.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Addressing/
fi

if [ -f "Views/AddressingPanelWindow.xaml" ]; then
    mv Views/AddressingPanelWindow.xaml src/Revit_FA_Tools.Revit/UI/Views/Addressing/
    mv Views/AddressingPanelWindow.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Addressing/
fi
```

#### Move ViewModels
```bash
# Move ViewModels
mv ViewModels/Addressing/AddressingViewModel.cs src/Revit_FA_Tools.Revit/UI/ViewModels/Addressing/

# Move additional ViewModels if they exist
if [ -f "ViewModels/Tree/AssignmentTreeViewModel.cs" ]; then
    mv ViewModels/Tree/AssignmentTreeViewModel.cs src/Revit_FA_Tools.Revit/UI/ViewModels/Addressing/
fi
```

#### Move Converters
```bash
# Move UI converters
mv Converters/TreeNodeConverters.cs src/Revit_FA_Tools.Revit/UI/Converters/
mv Converters/UIConverters.cs src/Revit_FA_Tools.Revit/UI/Converters/
```

#### Move Import/Export Services
```bash
# Move import/export services
mv Services/FQQImportExportService.cs src/Revit_FA_Tools.Revit/Services/Export/
mv Services/FamilyCatalogImporter.cs src/Revit_FA_Tools.Revit/Services/Import/
```

#### Move Configuration Files
```bash
# Move Revit configuration files
mv Revit_FA_Tools.addin src/Revit_FA_Tools.Revit/Configuration/
mv App.config src/Revit_FA_Tools.Revit/Configuration/
```

### 2.3 Move Data and Configuration Files

#### Move Device Catalogs and Data
```bash
# Move device catalogs
mv Data/AutoCallDeviceCatalog.json data/DeviceCatalogs/
mv device-repository.json data/DeviceCatalogs/

# Move configuration files
mv CandelaConfiguration.cs src/Revit_FA_Tools.Core/Models/Configuration/
mv CandelaCurrentMapping.json data/Configurations/
mv FireAlarmConfiguration.cs src/Revit_FA_Tools.Core/Models/Configuration/
```

#### Move Shared Data Models
```bash
# Move shared data models
mv SharedDataModels.cs src/Revit_FA_Tools.Core/Models/Systems/
```

### 2.4 Move Project Files

#### Move Build and Project Files
```bash
# Move build files
mv build.bat tools/build/
mv Revit_FA_Tools.csproj src/Revit_FA_Tools.Revit/
mv packages.config src/Revit_FA_Tools.Revit/

# Move assembly info
if [ -f "Properties/AssemblyInfo.cs" ]; then
    mv Properties/AssemblyInfo.cs src/Revit_FA_Tools.Revit/
fi

# Move progress window if it exists
if [ -f "ProgressWindow.xaml" ]; then
    mv ProgressWindow.xaml src/Revit_FA_Tools.Revit/UI/Views/Main/
    mv ProgressWindow.xaml.cs src/Revit_FA_Tools.Revit/UI/Views/Main/
fi
```

### 2.5 Move Documentation and Resources

#### Move Documentation
```bash
# Move documentation files
mv README.md docs/user-guide/
mv PROJECT_STRUCTURE.md docs/developer/
mv CURRENT_PROJECT_STRUCTURE.md docs/developer/

# Move any instruction files
if [ -d "Instructions" ]; then
    mv Instructions/* docs/developer/
    rmdir Instructions
fi
```

## Phase 3: Update Project References

### 3.1 Create Core Project File
Create `src/Revit_FA_Tools.Core/Revit_FA_Tools.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### 3.2 Update Solution File
Create new solution file `Revit_FA_Tools.sln` in root:
```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Revit_FA_Tools.Core", "src\Revit_FA_Tools.Core\Revit_FA_Tools.Core.csproj", "{NEW-GUID-1}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Revit_FA_Tools.Revit", "src\Revit_FA_Tools.Revit\Revit_FA_Tools.csproj", "{NEW-GUID-2}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {NEW-GUID-1}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {NEW-GUID-1}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {NEW-GUID-1}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {NEW-GUID-1}.Release|Any CPU.Build.0 = Release|Any CPU
        {NEW-GUID-2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {NEW-GUID-2}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {NEW-GUID-2}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {NEW-GUID-2}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal
```

### 3.3 Update Revit Project References
Update `src/Revit_FA_Tools.Revit/Revit_FA_Tools.csproj` to add Core project reference:
```xml
<ItemGroup>
  <ProjectReference Include="..\Revit_FA_Tools.Core\Revit_FA_Tools.Core.csproj" />
</ItemGroup>
```

## Phase 4: Clean Up Empty Directories

### 4.1 Remove Empty Directories
```bash
# Remove empty directories (only if they're empty)
find . -type d -empty -delete 2>/dev/null || true

# Remove original folders that should now be empty
if [ -d "Models" ] && [ -z "$(ls -A Models)" ]; then rmdir Models; fi
if [ -d "Services" ] && [ -z "$(ls -A Services)" ]; then rmdir Services; fi
if [ -d "Views" ] && [ -z "$(ls -A Views)" ]; then rmdir Views; fi
if [ -d "ViewModels" ] && [ -z "$(ls -A ViewModels)" ]; then rmdir ViewModels; fi
if [ -d "Converters" ] && [ -z "$(ls -A Converters)" ]; then rmdir Converters; fi
if [ -d "Data" ] && [ -z "$(ls -A Data)" ]; then rmdir Data; fi
```

## Phase 5: Verification

### 5.1 Verify File Locations
```bash
# Check that critical files are in their new locations
echo "Verifying Core files..."
ls -la src/Revit_FA_Tools.Core/Services/Calculation/Implementation/IDNACAnalyzer.cs
ls -la src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/ParameterMappingEngine.cs
ls -la src/Revit_FA_Tools.Core/Models/Devices/DeviceModels.cs

echo "Verifying Revit files..."
ls -la src/Revit_FA_Tools.Revit/Application.cs
ls -la src/Revit_FA_Tools.Revit/UI/Views/Main/MainWindow.xaml
ls -la src/Revit_FA_Tools.Revit/UI/ViewModels/Addressing/AddressingViewModel.cs

echo "Verifying Data files..."
ls -la data/DeviceCatalogs/AutoCallDeviceCatalog.json
ls -la data/Configurations/CandelaCurrentMapping.json
```

### 5.2 Create Verification Script
Create `tools/build/verify-structure.ps1`:
```powershell
Write-Host "Verifying project structure..."

$criticalFiles = @(
    "src/Revit_FA_Tools.Core/Services/Calculation/Implementation/IDNACAnalyzer.cs",
    "src/Revit_FA_Tools.Revit/Application.cs",
    "data/DeviceCatalogs/AutoCallDeviceCatalog.json"
)

foreach ($file in $criticalFiles) {
    if (Test-Path $file) {
        Write-Host "✓ $file" -ForegroundColor Green
    } else {
        Write-Host "✗ $file MISSING" -ForegroundColor Red
    }
}
```

## Important Notes

1. **Backup First**: Always create a complete backup before running these operations
2. **Test Builds**: After restructuring, test that the solution builds successfully
3. **Update Namespaces**: You'll need to update namespaces in moved files to match new structure
4. **Git History**: If using Git, consider using `git mv` instead of `mv` to preserve file history
5. **IDE Integration**: Close and reopen Visual Studio after restructuring

## Next Steps After Restructuring

1. Update all namespace declarations in moved files
2. Fix any broken references between projects
3. Update using statements to match new namespaces
4. Test build and fix any compilation errors
5. Run existing tests to verify functionality
6. Update documentation to reflect new structure

This restructuring preserves all existing functionality while organizing the project for better maintainability and development workflow.