# Debugging Breakpoints Guide for Revit FA Tools

## Essential Breakpoints for Debugging

### 1. Application Entry Points

#### Application.cs - Revit Add-in Initialization
- **Line 22**: `OnStartup()` method entry - First point where add-in loads
- **Line 27**: `InitializeServices()` call - DI container setup
- **Line 44**: Theme initialization - Catch theme loading issues
- **Line 65**: Ribbon tab creation - UI setup issues

#### Command.cs - Main Command Handler
- **Line 16**: `Execute()` method entry - Command invocation point
- **Line 24**: Document null check - Document availability
- **Line 33**: MainWindow creation - UI initialization
- **Line 47**: `mainWindow.Show()` - Window display
- **Line 51-52**: Exception handler in catch block - Error capture

### 2. Service Initialization & DI Container

#### Application.cs - Service Registration
- **Line 131**: `InitializeServices()` method start
- **Line 142**: `BuildServiceProvider()` - Service provider creation
- **Line 145**: `ValidateServices()` - Service validation

#### ServiceRegistration.cs
- Add breakpoints in `RegisterCoreServices()` and `RegisterRevitServices()` methods

### 3. Error Handling & Exception Management

#### ServiceExceptionHandler.cs - Global Error Handler
- **Line 26**: `HandleAsync()` try block start
- **Line 30-36**: ArgumentNullException handler
- **Line 78-84**: General exception handler
- **Line 115**: Synchronous `Handle()` method
- **Line 120**: Error logging call

### 4. Main UI Workflow

#### MainWindow.xaml.cs
- **Line 46**: Constructor entry
- **Line 56**: `ApplyTheme()` call
- **Line 68**: `SetInitialState()` call
- **Line 69**: `ConfigureGrids()` call
- **Line 83**: Error handling in `InitializeWindowControls()`

### 5. Analysis Workflow
Add breakpoints in key analysis methods:
- Run Analysis button click handler
- Device collection methods
- Calculation service calls
- Results display methods

### 6. Critical Service Methods

#### UnifiedAddressingService
- Service initialization
- Address assignment methods
- Validation calls

#### ElectricalCalculator
- Calculation entry points
- Error conditions

#### IDNACAnalyzer & IDNETAnalyzer
- Analysis start methods
- Circuit/channel processing
- Results compilation

## Debugging Configuration

### Update launch.json for Revit Debugging

Since this is a Revit add-in, you'll need to attach to the Revit process. Update your `.vscode/launch.json`:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Attach to Revit",
            "type": "coreclr",
            "request": "attach",
            "processName": "Revit.exe"
        },
        {
            "name": "Debug Revit Add-in",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "C:\\Program Files\\Autodesk\\Revit 2024\\Revit.exe",
            "args": [],
            "cwd": "${workspaceFolder}",
            "console": "internalConsole",
            "stopAtEntry": false,
            "justMyCode": false
        }
    ]
}
```

## Debugging Tips

1. **Enable Debug Output**: All major components use `System.Diagnostics.Debug.WriteLine()` - ensure Debug output window is visible

2. **Conditional Breakpoints**: Use conditional breakpoints for:
   - Specific device types
   - Error conditions
   - Null reference checks

3. **Watch Variables**:
   - `_document` - Revit document state
   - `_serviceProvider` - DI container state
   - Exception objects in catch blocks
   - Service operation results

4. **Call Stack**: Monitor the call stack to understand the flow from Revit command to service execution

5. **Immediate Window**: Use for testing service calls and checking object states

## Common Debugging Scenarios

### 1. Service Resolution Failures
- Set breakpoint at line 145 in Application.cs (`ValidateServices`)
- Check which service fails to resolve
- Verify registration in ServiceRegistration classes

### 2. UI Not Displaying
- Breakpoint at MainWindow constructor
- Check window initialization
- Verify theme application

### 3. Analysis Failures
- Breakpoint in analysis button click handler
- Step through device collection
- Monitor calculation services

### 4. Null Reference Exceptions
- Enable first-chance exception breaks
- Set breakpoints in all catch blocks
- Check document and UI document availability

### 5. Performance Issues
- Use performance profiler
- Set breakpoints with hit counts
- Monitor collection sizes in analysis methods

### 6. "No Elements to Check" Issue (IDNAC Analysis)
This is a critical debugging scenario when IDNAC Analysis reports no elements found.

#### Key Breakpoints for Element Collection:

##### MainWindow.xaml.cs - Analysis Entry & Element Collection
- **Line 896**: `RunAnalysisInternalAsync()` - Analysis start point
- **Line 974**: `GetElementsByScope()` call - Element collection trigger
- **Line 976-981**: Element count check - Where "no elements" error is triggered
- **Line 3158**: `GetElementsByScope()` method entry - Main collection method
- **Line 3162**: Debug output for scope
- **Line 3164**: Switch statement for scope handling

##### Scope-Specific Breakpoints:
**Active View (Lines 3188-3237)**:
- **Line 3199**: FilteredElementCollector creation
- **Line 3206-3210**: Fire Alarm device filtering
- **Line 3212**: Debug output showing element counts
- **Line 3215**: Empty collection check (fallback attempt)

**Selection (Lines 3166-3186)**:
- **Line 3167**: Getting selected element IDs
- **Line 3170**: Empty selection check
- **Line 3179-3183**: Fire Alarm category filtering

**Entire Model (Lines 3239-3254)**:
- **Line 3240**: Model collector creation
- **Line 3247-3251**: Fire Alarm filtering logic

##### Critical Filter Logic:
- **Lines 3179-3183, 3206-3210, 3247-3251**: Category name filtering
  - Watch: `element.Category?.Name`
  - Check if it contains "FIRE ALARM DEVICES" or "FIRE ALARM"

#### Debugging Steps:
1. Set breakpoint at line 3158 (GetElementsByScope entry)
2. Check `_currentScope` value
3. Step into the appropriate case block
4. Monitor:
   - Total FamilyInstances found
   - Fire Alarm filtered count
   - Category names being checked
5. If count is 0, check:
   - Are elements in the model?
   - Is category name matching the filter?
   - Is the view/selection correct?

#### Watch Expressions:
```
_currentScope
allElementsInView.Count
fireAlarmElementsInView.Count
element.Category?.Name
categoryName.ToUpperInvariant()
```

#### Common Causes:
1. **Wrong Category**: Elements might not have "FIRE ALARM" in category name
2. **View Issues**: Active view might not contain the elements
3. **Selection Empty**: No elements selected when using Selection scope
4. **Case Sensitivity**: Although code uses ToUpperInvariant(), check actual category names

## Debugging Workflow

1. Start Revit
2. Load a project with fire alarm devices
3. Attach debugger to Revit.exe process
4. Set initial breakpoints listed above
5. Run the Revit FA Tools command
6. Step through initialization
7. Test specific scenarios based on issue

## Additional Debugging Tools

- **Debug.WriteLine outputs**: Check VS Output window (Debug)
- **Exception Settings**: Break on all CLR exceptions initially
- **Diagnostic Tools**: Monitor memory and CPU usage
- **Call Stack window**: Understand execution flow
- **Locals/Autos windows**: Inspect variable values

This setup provides comprehensive debugging coverage for the main execution paths and common error scenarios.