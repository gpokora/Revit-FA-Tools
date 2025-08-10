# Revit_FA_Tools - C# Revit 2024 Addin

This is a C# Revit 2024 addin converted from the original Python pyRevit script. It provides enhanced fire alarm load calculations with 4100ES IDNAC analysis.

## Features

- **Fire Alarm Load Calculations**: Calculate current and wattage for fire alarm notification device family instances
- **4100ES IDNAC Analysis**: Comprehensive analysis with 20% spare capacity calculations
- **Device Type Analysis**: Categorizes devices (speakers, strobes, horns, combos) and calculates amplifier requirements
- **Level-Based Analysis**: Organizes loads by building levels with optimization recommendations
- **Panel Placement**: Intelligent recommendations for panel placement considering amplifiers and cabinet space
- **Multiple Scopes**: Supports Active View, Selection, and Entire Model calculations

## Requirements

- Autodesk Revit 2024
- .NET Framework 4.8
- Windows 10/11

## Installation

### Option 1: Manual Installation

1. **Build the Project**:
   - Open `Revit_FA_Tools.csproj` in Visual Studio 2019/2022
   - Ensure Revit 2024 is installed at the default location (`C:\Program Files\Autodesk\Revit 2024\`)
   - Build the solution in Release mode

2. **Copy Files**:
   - Copy `Revit_FA_Tools.dll` to `%AppData%\Autodesk\Revit\Addins\2024\`
   - Copy `Revit_FA_Tools.addin` to `%AppData%\Autodesk\Revit\Addins\2024\`

3. **Start Revit**:
   - The addin will appear in the "Fire Alarm Tools" ribbon panel
   - Click "IDNAC Calculator" to run the tool

### Option 2: Automatic Installation (Post-Build Event)

The project includes a post-build event that automatically copies the files to the correct location when building in Visual Studio.

## Usage

1. **Open a Revit Model** with fire alarm notification device family instances
2. **Click the IDNAC Calculator** button in the Autocall Tools ribbon
3. **Select Calculation Scope**:
   - **Active View**: Analyze elements visible in the current view
   - **Selection**: Analyze only selected elements
   - **Entire Model**: Analyze all elements in the model
4. **Review Results** in the comprehensive tabbed interface:
   - **Summary**: Overall totals and warnings
   - **IDNAC Analysis**: Level-by-level IDNAC requirements
   - **Device Analysis**: Device type breakdown and amplifier requirements
   - **Panel Placement**: Intelligent placement recommendations
   - **Level Details**: Detailed grid view of level data
   - **Family Details**: Family-by-family breakdown

## Required Family Parameters

For accurate calculations, fire alarm notification device families must include:

- **CURRENT DRAW** (Parameter Type: Electrical Current, Group: Electrical)
- **Wattage** (Parameter Type: Electrical Power, Group: Electrical)

These parameters can be defined at either the Type or Instance level.

## Key Calculations

### IDNAC Specifications (4100ES with 20% Spare Capacity)
- **Current Capacity**: 2.4A usable / 3.0A maximum per IDNAC
- **Device Capacity**: 101 usable / 127 maximum addresses per IDNAC
- **Unit Load Capacity**: 111 usable / 139 maximum unit loads per IDNAC
- **Distance Limits**: 4,000 ft to any device, 10,000 ft total wire length
- **Power Supply**: 3 IDNACs per ES-PS power supply

### Amplifier Specifications (4100ES)
- **Flex-35**: 28W usable/35W max, 100 speakers max, 2 blocks, 5.5A
- **Flex-50**: 40W usable/50W max, 100 speakers max, 2 blocks, 5.55A  
- **Flex-100**: 80W usable/100W max, 100 speakers max, 4 blocks, 9.6A

## Project Structure

- `Application.cs`: Revit application entry point and ribbon setup
- `Command.cs`: Main command execution logic
- `ElectricalCalculator.cs`: Electrical parameter extraction and calculations
- `IDNACAnalyzer.cs`: IDNAC analysis with spare capacity calculations
- `AmplifierCalculator.cs`: Device type analysis and amplifier requirements
- `PanelPlacementAnalyzer.cs`: Panel placement recommendations
- `ScopeSelectionDialog.xaml/.cs`: Scope selection user interface
- `ResultsWindow.xaml/.cs`: Results display with tabbed interface

## Development Notes

### Converting from Python pyRevit

This addin was converted from a Python pyRevit script. Key differences:

1. **API Access**: Direct Revit API instead of pyRevit wrappers
2. **UI Framework**: WPF instead of pyRevit forms
3. **Output**: Tabbed WPF window instead of script output window
4. **Error Handling**: TaskDialog instead of pyRevit alerts
5. **Parameter Access**: Direct Revit Parameter API instead of pyRevit shortcuts

### Building and Debugging

1. **Revit API References**: The project references RevitAPI.dll and RevitAPIUI.dll from the Revit 2024 installation
2. **Target Framework**: .NET Framework 4.8 (required for Revit 2024)
3. **Output Path**: Configured to copy to Revit addins folder automatically
4. **Debugging**: Attach debugger to Revit.exe process for debugging

## Troubleshooting

### Common Issues

1. **"Assembly not found" errors**:
   - Verify Revit 2024 is installed
   - Check that RevitAPI.dll paths in .csproj are correct

2. **Addin not loading**:
   - Check .addin file is in correct location
   - Verify Assembly path in .addin file matches DLL location
   - Check Revit add-in manager for load errors

3. **Parameter not found**:
   - Verify family instances have CURRENT DRAW and/or Wattage parameters
   - Check parameter names match exactly (case-sensitive)

4. **Build errors**:
   - Ensure .NET Framework 4.8 Developer Pack is installed
   - Verify Visual Studio has WPF development workload installed

## Support

For issues or questions about this addin, please check:
1. Family parameter requirements
2. Revit 2024 compatibility
3. .NET Framework 4.8 installation
4. Revit API documentation

## Engineering Spec
See [docs/Claude_Commands.md](docs/Claude_Commands.md) for the implementation command set.

## License

This software is provided as-is for fire alarm engineering calculations. Users are responsible for verifying all calculations and compliance with applicable codes and standards.