using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit_FA_Tools
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Error", "No active document found.");
                    return Result.Failed;
                }

                // Show MainWindow as non-modal to keep Revit UI responsive
                try
                {
                    var mainWindow = new MainWindow(doc, uiDoc);
                    
                    // Set Revit as owner window for proper behavior
                    try
                    {
                        var revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                        var helper = new WindowInteropHelper(mainWindow);
                        helper.Owner = revitHandle;
                    }
                    catch
                    {
                        // Ignore ownership setting errors
                    }
                    
                    mainWindow.Show(); // Non-modal window
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error", $"Error showing analysis window: {ex.Message}");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"An error occurred: {ex.Message}";
                if (ex.InnerException != null)
                    errorMessage += $"\nInner Exception: {ex.InnerException.Message}";

                System.Diagnostics.Debug.WriteLine($"Command execution error: {errorMessage}");
                TaskDialog.Show("Error", errorMessage);
                return Result.Failed;
            }
        }
    }
}