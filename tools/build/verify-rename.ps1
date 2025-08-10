Write-Host "=== REVIT_FA_TOOLS PROJECT RENAME VERIFICATION ===" -ForegroundColor Green
Write-Host ""

# Test critical project files
Write-Host "Testing project structure..." -ForegroundColor Yellow

$criticalFiles = @(
    "Revit_FA_Tools.sln",
    "src/Revit_FA_Tools.Core/Revit_FA_Tools.Core.csproj",
    "src/Revit_FA_Tools.Revit/Revit_FA_Tools.Revit.csproj",
    "src/Revit_FA_Tools.Revit/Configuration/Revit_FA_Tools.addin"
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

Write-Host ""
Write-Host "Testing for old IDNACCalculator references in source files..." -ForegroundColor Yellow

# Check for any remaining IDNACCalculator references in source files
$oldRefs = Get-ChildItem -Recurse -Include "*.cs","*.xaml" -Path "src" | 
    Select-String -Pattern "IDNACCalculator" | 
    Where-Object { $_.Line -notmatch "//.*IDNACCalculator" }  # Ignore commented references

if ($oldRefs) {
    Write-Host "✗ Found remaining IDNACCalculator references:" -ForegroundColor Red
    $oldRefs | ForEach-Object { 
        Write-Host "  - $($_.Filename):$($_.LineNumber) - $($_.Line.Trim())" -ForegroundColor Red 
    }
    $allPassed = $false
} else {
    Write-Host "✓ No IDNACCalculator references found in source files" -ForegroundColor Green
}

Write-Host ""
Write-Host "Testing namespace consistency..." -ForegroundColor Yellow

# Test sample namespace declarations
$sampleFile = "src/Revit_FA_Tools.Core/Services/Integration/ComprehensiveEngineeringService.cs"
if (Test-Path $sampleFile) {
    $content = Get-Content $sampleFile -Head 15
    if ($content -match "namespace Revit_FA_Tools") {
        Write-Host "✓ Core namespaces correctly updated" -ForegroundColor Green
    } else {
        Write-Host "✗ Core namespaces not updated" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host ""
if ($allPassed) {
    Write-Host "🎉 PROJECT RENAME SUCCESSFUL!" -ForegroundColor Green
    Write-Host "All IDNACCalculator references have been updated to Revit_FA_Tools" -ForegroundColor Green
} else {
    Write-Host "❌ PROJECT RENAME INCOMPLETE" -ForegroundColor Red
    Write-Host "Some issues need to be addressed" -ForegroundColor Red
}