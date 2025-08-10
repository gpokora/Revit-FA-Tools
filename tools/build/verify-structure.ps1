Write-Host "Verifying project structure..." -ForegroundColor Yellow

$criticalFiles = @(
    "src/Revit_FA_Tools.Core/Services/Calculation/Implementation/IDNACAnalyzer.cs",
    "src/Revit_FA_Tools.Core/Services/ParameterMapping/Implementation/ParameterMappingEngine.cs", 
    "src/Revit_FA_Tools.Core/Models/Devices/DeviceModels.cs",
    "src/Revit_FA_Tools.Revit/Application.cs",
    "src/Revit_FA_Tools.Revit/UI/Views/Main/MainWindow.xaml",
    "src/Revit_FA_Tools.Revit/UI/ViewModels/Addressing/AddressingViewModel.cs",
    "data/DeviceCatalogs/AutoCallDeviceCatalog.json",
    "data/Configurations/CandelaCurrentMapping.json",
    "src/Revit_FA_Tools.Core/Revit_FA_Tools.Core.csproj",
    "Revit_FA_Tools.sln"
)

$allPassed = $true

foreach ($file in $criticalFiles) {
    if (Test-Path $file) {
        Write-Host "✓ $file" -ForegroundColor Green
    } else {
        Write-Host "✗ $file MISSING" -ForegroundColor Red
        $allPassed = $false
    }
}

if ($allPassed) {
    Write-Host "`nProject restructuring completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nProject restructuring has issues - see missing files above" -ForegroundColor Red
}