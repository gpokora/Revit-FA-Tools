using System;
using Autodesk.Revit.UI;
using DevExpress.Xpf.Core;
using Revit_FA_Tools.Core.Infrastructure.ServiceRegistration;
using Revit_FA_Tools.Core.Infrastructure.DependencyInjection;
using IServiceProvider = Revit_FA_Tools.Core.Infrastructure.DependencyInjection.IServiceProvider;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Implements the Revit add-in interface IExternalApplication
    /// </summary>
    public class Application : IExternalApplication
    {
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// Gets the global service provider for the application
        /// </summary>
        public static IServiceProvider ServiceProvider => _serviceProvider;

        public Result OnStartup(UIControlledApplication application)
        {
            // Initialize dependency injection container
            try
            {
                InitializeServices();
                System.Diagnostics.Debug.WriteLine("Dependency injection container initialized successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize dependency injection: {ex.Message}");
                // Continue startup even if DI fails to maintain backward compatibility
            }
            // Initialize DevExpress theme at application level
            try
            {
                string[] themesToTry = { "VS2019Dark", "Win11Dark", "Office2019Black", "VS2017Dark", "Win10Dark" };
                
                foreach (var theme in themesToTry)
                {
                    try
                    {
                        ApplicationThemeHelper.ApplicationThemeName = theme;
                        System.Diagnostics.Debug.WriteLine($"Applied application theme: {theme}");
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Application theme initialization failed: {ex.Message}");
            }

            // Create custom Revit tab
            string tabName = "Revit FA Tools";
            
            // Try to create the tab, but handle the case where it might already exist
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (System.ArgumentException)
            {
                // Tab already exists, which is fine
                System.Diagnostics.Debug.WriteLine($"Tab '{tabName}' already exists");
            }

            // Create ribbon panels within the Revit FA Tools tab
            RibbonPanel analysisPanel = application.CreateRibbonPanel(tabName, "Fire Alarm Analysis");

            // Create Autocall Design button
            PushButtonData idnacButtonData = new PushButtonData("Revit_FA_Tools",
                "Autocall\nDesign",
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                "Revit_FA_Tools.Command");

            idnacButtonData.ToolTip = "Calculate fire alarm notification device loads with 4100ES IDNAC analysis";
            idnacButtonData.LongDescription = "Enhanced Fire Alarm Load Calculator with IDNAC Analysis\n\n" +
                "This tool calculates current and wattage for fire alarm notification device family instances " +
                "across active view, selection, or entire model, and provides 4100ES IDNAC analysis and panel " +
                "placement recommendations with proper 20% spare capacity.";

            // Add button to analysis panel
            PushButton idnacButton = analysisPanel.AddItem(idnacButtonData) as PushButton;

            // Create additional panel for future tools
            RibbonPanel utilitiesPanel = application.CreateRibbonPanel(tabName, "Utilities");
            
            // Add placeholder for future tools
            PushButtonData placeholderData = new PushButtonData("ComingSoon",
                "More Tools\nComing Soon",
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                "Revit_FA_Tools.Command"); // Reuse the same command for now
                
            placeholderData.ToolTip = "Additional Autocall fire alarm tools";
            placeholderData.LongDescription = "Future expansion area for additional Autocall fire alarm engineering tools";
            
            PushButton placeholderButton = utilitiesPanel.AddItem(placeholderData) as PushButton;
            placeholderButton.Enabled = false; // Disable until actual functionality is added

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // Dispose of the service provider
            try
            {
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }
                System.Diagnostics.Debug.WriteLine("Service provider disposed successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing service provider: {ex.Message}");
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Initializes the dependency injection container
        /// </summary>
        private void InitializeServices()
        {
            var services = new ServiceCollection();
            
            // Register Core services
            services.RegisterCoreServices();
            
            // Register Revit-specific services
            services.RegisterRevitServices();
            
            // Build the service provider
            _serviceProvider = services.BuildServiceProvider();
            
            // Validate that all required services can be resolved
            ServiceRegistration.ValidateServices((System.IServiceProvider)_serviceProvider);
        }
    }
}