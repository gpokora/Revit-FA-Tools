@echo off
REM Build script for Revit_FA_Tools Revit 2024 Addin
REM Run this from the project root directory

echo Building Revit_FA_Tools for Revit 2024...

REM Navigate to project root if not already there
cd /d "%~dp0..\.."

REM Check if MSBuild is available
where msbuild >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo MSBuild not found in PATH. Please run from Visual Studio Developer Command Prompt.
    pause
    exit /b 1
)

REM Build the solution
echo Building solution...
msbuild Revit_FA_Tools.sln /p:Configuration=Release /p:Platform=x64 /verbosity:minimal

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build completed successfully!

REM Check if files were copied to Revit addins folder
set ADDIN_PATH=%APPDATA%\Autodesk\Revit\Addins\2024
echo Checking addin installation...

if exist "%ADDIN_PATH%\Revit_FA_Tools.dll" (
    echo ✓ Revit_FA_Tools.dll found in addins folder
) else (
    echo ⚠ Revit_FA_Tools.dll not found in addins folder
    echo Manually copy src\Revit_FA_Tools.Revit\bin\x64\Release\Revit_FA_Tools.dll to %ADDIN_PATH%
)

if exist "%ADDIN_PATH%\Revit_FA_Tools.Core.dll" (
    echo ✓ Revit_FA_Tools.Core.dll found in addins folder
) else (
    echo ⚠ Revit_FA_Tools.Core.dll not found in addins folder
    echo Manually copy src\Revit_FA_Tools.Revit\bin\x64\Release\Revit_FA_Tools.Core.dll to %ADDIN_PATH%
)

if exist "%ADDIN_PATH%\Revit_FA_Tools.addin" (
    echo ✓ Revit_FA_Tools.addin found in addins folder
) else (
    echo ⚠ Revit_FA_Tools.addin not found in addins folder
    echo Manually copy src\Revit_FA_Tools.Revit\Configuration\Revit_FA_Tools.addin to %ADDIN_PATH%
)

echo.
echo Installation complete! Start Revit 2024 to use the Revit_FA_Tools.
echo The tool will appear in the "Revit FA Tools" tab, "Fire Alarm Analysis" panel as "Autocall Design".
echo.
pause