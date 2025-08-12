using System;
using System.Windows;
using DevExpress.Xpf.Core;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Revit.UI.ViewModels.Addressing;

namespace Revit_FA_Tools.Revit.UI.Views.Addressing
{
    /// <summary>
    /// Modern addressing panel window using clean architecture with dependency injection
    /// </summary>
    public partial class ModernAddressingPanelWindow : ThemedWindow
    {
        private readonly CleanAddressingViewModel _viewModel;

        public ModernAddressingPanelWindow()
        {
            InitializeComponent();
            
            try
            {
                // Get services from the global service provider
                var serviceProvider = Application.ServiceProvider;
                if (serviceProvider == null)
                {
                    throw new InvalidOperationException("Service provider not initialized. Ensure Application.OnStartup has been called.");
                }

                var addressingPanelService = serviceProvider.GetRequiredService<IAddressingPanelService>();
                var validationService = serviceProvider.GetRequiredService<IValidationService>();

                // Create ViewModel with injected services
                _viewModel = new CleanAddressingViewModel(addressingPanelService, validationService);
                
                // Set DataContext
                DataContext = _viewModel;

                // Configure window
                ConfigureWindow();
                
                // Initialize with data
                _ = _viewModel.LoadDataCommand.Execute(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize addressing panel: {ex.Message}", 
                    "Initialization Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                // Fallback: Close window if initialization fails
                Close();
            }
        }

        /// <summary>
        /// Constructor that allows explicit service injection for testing
        /// </summary>
        public ModernAddressingPanelWindow(
            IAddressingPanelService addressingPanelService, 
            IValidationService validationService) : this()
        {
            if (addressingPanelService == null)
                throw new ArgumentNullException(nameof(addressingPanelService));
            if (validationService == null)
                throw new ArgumentNullException(nameof(validationService));

            _viewModel = new CleanAddressingViewModel(addressingPanelService, validationService);
            DataContext = _viewModel;
        }

        private void ConfigureWindow()
        {
            // Set window properties
            Title = "Fire Alarm Device Addressing - Modern Interface";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            // Configure DevExpress theme
            try
            {
                // Apply theme if not already set
                if (string.IsNullOrEmpty(ApplicationThemeHelper.ApplicationThemeName))
                {
                    ApplicationThemeHelper.ApplicationThemeName = "VS2019Dark";
                }
            }
            catch (Exception ex)
            {
                // Theme application is not critical - log and continue
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
            }

            // Handle window events
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set focus to the first focusable element
                MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));

                // Log window opened
                System.Diagnostics.Debug.WriteLine("Modern Addressing Panel window opened successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window loaded handler: {ex.Message}");
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Check for unsaved changes
                if (_viewModel?.HasUnsavedChanges == true)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes. Do you want to apply them before closing?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            // Apply changes before closing
                            _viewModel.ApplyChangesCommand.Execute(null);
                            break;
                        case MessageBoxResult.No:
                            // Discard changes
                            _viewModel.RevertChangesCommand.Execute(null);
                            break;
                        case MessageBoxResult.Cancel:
                            // Cancel close operation
                            e.Cancel = true;
                            return;
                    }
                }

                // Cleanup resources
                CleanupResources();

                System.Diagnostics.Debug.WriteLine("Modern Addressing Panel window closed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window closing handler: {ex.Message}");
                // Don't cancel close operation due to cleanup errors
            }
        }

        private void CleanupResources()
        {
            try
            {
                // Dispose of any disposable resources in the ViewModel
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                // Clear data context
                DataContext = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during resource cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current ViewModel for external access
        /// </summary>
        public CleanAddressingViewModel ViewModel => _viewModel;

        /// <summary>
        /// Refreshes the data in the window
        /// </summary>
        public void RefreshData()
        {
            try
            {
                _viewModel?.LoadDataCommand.Execute(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to refresh data: {ex.Message}",
                    "Refresh Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Shows the window as a modal dialog and returns the result
        /// </summary>
        public new bool? ShowDialog()
        {
            try
            {
                return base.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error showing dialog: {ex.Message}",
                    "Dialog Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Static factory method to create and show the window
        /// </summary>
        public static ModernAddressingPanelWindow CreateAndShow()
        {
            try
            {
                var window = new ModernAddressingPanelWindow();
                window.Show();
                return window;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create addressing panel window: {ex.Message}",
                    "Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Static factory method to create and show the window as a dialog
        /// </summary>
        public static bool? CreateAndShowDialog()
        {
            try
            {
                var window = new ModernAddressingPanelWindow();
                return window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create addressing panel dialog: {ex.Message}",
                    "Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}