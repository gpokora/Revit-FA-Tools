using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;

namespace Revit_FA_Tools
{
    public partial class ProgressWindow : ThemedWindow
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCompleted = false;

        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;
        public bool IsCanceled => _cancellationTokenSource?.IsCancellationRequested ?? false;
        public bool IsCompleted => _isCompleted;

        public ProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Apply theme
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            try
            {
                // Use same theme detection logic as main UI
                bool isDarkMode = IsSystemDarkMode();
                
                // Apply DevExpress theme with proper fallback (same as main UI)
                string[] themesToTry = isDarkMode 
                    ? new[] { "Win11Dark", "VS2017Dark", "Office2019Black", "VS2017Blue" }
                    : new[] { "Office2019Colorful", "VS2017Blue", "Office2016White" };

                bool themeApplied = false;
                foreach (var theme in themesToTry)
                {
                    try
                    {
                        ApplicationThemeHelper.ApplicationThemeName = theme;
                        System.Diagnostics.Debug.WriteLine($"Progress window applied DevExpress theme: {theme}");
                        themeApplied = true;
                        break;
                    }
                    catch (Exception themeEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to apply theme {theme}: {themeEx.Message}");
                        // Continue to next theme
                    }
                }

                if (!themeApplied)
                {
                    System.Diagnostics.Debug.WriteLine("Progress window: No DevExpress theme could be applied, using fallback styling");
                }

                // Apply custom dark mode styling if needed
                if (isDarkMode)
                {
                    ApplyDarkTheme();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Progress window theme application failed: {ex.Message}");
                // Continue without theme - window will use default styling
            }
        }

        /// <summary>
        /// System theme detection - matches main UI exactly
        /// </summary>
        private bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int value)
                    {
                        return value == 0; // 0 = dark mode, 1 = light mode
                    }
                }
            }
            catch
            {
                // Ignore registry access errors
            }
            return false; // Default to light mode
        }

        private void ApplyDarkTheme()
        {
            try
            {
                // Update window background for dark mode
                this.Background = (SolidColorBrush)this.FindResource("ProgressDarkBackgroundBrush");
                
                // DevExpress components will inherit theme through resources
                // Manual color updates are handled by the resource styles
                
                System.Diagnostics.Debug.WriteLine("Progress window: Dark theme applied using DevExpress resources");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dark theme application failed: {ex.Message}");
                // Fallback to manual colors if resources fail
                this.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            }
        }

        /// <summary>
        /// Update the current operation description
        /// </summary>
        public void UpdateOperation(string operation)
        {
            if (Dispatcher.CheckAccess())
            {
                OperationLabel.Text = operation;
            }
            else
            {
                Dispatcher.Invoke(() => OperationLabel.Text = operation);
            }
        }

        /// <summary>
        /// Update the progress percentage
        /// </summary>
        public void UpdateProgress(int percentage, string? details = null)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateProgressInternal(percentage, details);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateProgressInternal(percentage, details));
            }
        }

        private void UpdateProgressInternal(int percentage, string details)
        {
            // Clamp percentage to valid range
            percentage = Math.Max(0, Math.Min(100, percentage));
            
            // Update DevExpress ProgressBarEdit
            MainProgressBar.EditValue = percentage;
            ProgressText.Text = $"{percentage}% Complete";

            if (!string.IsNullOrEmpty(details))
            {
                DetailsLabel.Text = details;
            }
        }

        /// <summary>
        /// Update both operation and progress
        /// </summary>
        public void UpdateProgress(string operation, int percentage, string? details = null)
        {
            if (Dispatcher.CheckAccess())
            {
                OperationLabel.Text = operation;
                UpdateProgressInternal(percentage, details);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    OperationLabel.Text = operation;
                    UpdateProgressInternal(percentage, details);
                });
            }
        }
        
        /// <summary>
        /// Update current item being processed (file, instance, etc.)
        /// </summary>
        public void UpdateCurrentItem(string currentItem)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateCurrentItemInternal(currentItem);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateCurrentItemInternal(currentItem));
            }
        }
        
        private void UpdateCurrentItemInternal(string currentItem)
        {
            if (!string.IsNullOrEmpty(currentItem))
            {
                CurrentItemLabel.Text = currentItem;
                CurrentItemPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentItemPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        /// <summary>
        /// Update current operation details
        /// </summary>
        public void UpdateCurrentOperation(string operation)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateCurrentOperationInternal(operation);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateCurrentOperationInternal(operation));
            }
        }
        
        private void UpdateCurrentOperationInternal(string operation)
        {
            if (!string.IsNullOrEmpty(operation))
            {
                CurrentOperationLabel.Text = operation;
                CurrentOperationPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentOperationPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        /// <summary>
        /// Update all progress information at once
        /// </summary>
        public void UpdateProgressDetailed(string operation, int percentage, string? details = null, string? currentItem = null, string? currentOperation = null)
        {
            if (Dispatcher.CheckAccess())
            {
                OperationLabel.Text = operation;
                UpdateProgressInternal(percentage, details);
                UpdateCurrentItemInternal(currentItem);
                UpdateCurrentOperationInternal(currentOperation);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    OperationLabel.Text = operation;
                    UpdateProgressInternal(percentage, details);
                    UpdateCurrentItemInternal(currentItem);
                    UpdateCurrentOperationInternal(currentOperation);
                });
            }
        }
        
        /// <summary>
        /// Clear real-time information displays
        /// </summary>
        public void ClearRealTimeInfo()
        {
            if (Dispatcher.CheckAccess())
            {
                CurrentItemPanel.Visibility = Visibility.Collapsed;
                CurrentOperationPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    CurrentItemPanel.Visibility = Visibility.Collapsed;
                    CurrentOperationPanel.Visibility = Visibility.Collapsed;
                });
            }
        }

        /// <summary>
        /// Mark the operation as completed
        /// </summary>
        public void Complete(string finalMessage = "Analysis completed successfully!")
        {
            if (Dispatcher.CheckAccess())
            {
                CompleteInternal(finalMessage);
            }
            else
            {
                Dispatcher.Invoke(() => CompleteInternal(finalMessage));
            }
        }

        private void CompleteInternal(string finalMessage)
        {
            _isCompleted = true;
            UpdateProgressInternal(100, finalMessage);
            OperationLabel.Text = "Analysis Complete";
            
            // Update operation icon to success
            if (OperationIcon != null)
            {
                try
                {
                    var imageUri = "pack://application:,,,/DevExpress.Images.v24.2;component/SvgImages/Icon Builder/Actions_Apply.svg";
                    OperationIcon.Source = DevExpress.Xpf.Core.DXImageHelper.GetImageSource(imageUri);
                }
                catch 
                {
                    // Fallback - use existing icon
                }
            }
            
            // Update button content with close icon
            var closeContent = new StackPanel { Orientation = Orientation.Horizontal };
            var closeIcon = new DevExpress.Xpf.Core.DXImage
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0)
            };
            try
            {
                var closeImageUri = "pack://application:,,,/DevExpress.Images.v24.2;component/SvgImages/Icon Builder/Actions_Close.svg";
                closeIcon.Source = DevExpress.Xpf.Core.DXImageHelper.GetImageSource(closeImageUri);
            }
            catch 
            {
                // Use default close icon
            }
            var closeText = new TextBlock { Text = "Close", VerticalAlignment = VerticalAlignment.Center };
            closeContent.Children.Add(closeIcon);
            closeContent.Children.Add(closeText);
            CancelButton.Content = closeContent;
        }

        /// <summary>
        /// Show error state
        /// </summary>
        public void ShowError(string errorMessage)
        {
            if (Dispatcher.CheckAccess())
            {
                ShowErrorInternal(errorMessage);
            }
            else
            {
                Dispatcher.Invoke(() => ShowErrorInternal(errorMessage));
            }
        }

        private void ShowErrorInternal(string errorMessage)
        {
            OperationLabel.Text = "Analysis Failed";
            DetailsLabel.Text = errorMessage;
            
            // Update operation icon to error
            if (OperationIcon != null)
            {
                try
                {
                    var errorImageUri = "pack://application:,,,/DevExpress.Images.v24.2;component/SvgImages/Icon Builder/Actions_Cancel.svg";
                    OperationIcon.Source = DevExpress.Xpf.Core.DXImageHelper.GetImageSource(errorImageUri);
                }
                catch 
                {
                    // Fallback - use existing icon
                }
            }
            
            // Set progress bar to error appearance
            MainProgressBar.EditValue = 100;
            ProgressText.Text = "Failed";
            
            // Update button content with close icon
            var closeContent = new StackPanel { Orientation = Orientation.Horizontal };
            var closeIcon = new DevExpress.Xpf.Core.DXImage
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0)
            };
            try
            {
                var closeImageUri = "pack://application:,,,/DevExpress.Images.v24.2;component/SvgImages/Icon Builder/Actions_Close.svg";
                closeIcon.Source = DevExpress.Xpf.Core.DXImageHelper.GetImageSource(closeImageUri);
            }
            catch 
            {
                // Use default close icon
            }
            var closeText = new TextBlock { Text = "Close", VerticalAlignment = VerticalAlignment.Center };
            closeContent.Children.Add(closeIcon);
            closeContent.Children.Add(closeText);
            CancelButton.Content = closeContent;
        }

        /// <summary>
        /// Set indeterminate progress (for unknown duration operations)
        /// </summary>
        public void SetIndeterminate(bool indeterminate)
        {
            if (Dispatcher.CheckAccess())
            {
                // DevExpress ProgressBarEdit indeterminate mode
                // Note: ShowProgressValue property may not exist in this version
                // MainProgressBar.ShowProgressValue = !indeterminate;
                if (indeterminate)
                {
                    MainProgressBar.EditValue = null;
                    ProgressText.Text = "Processing...";
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    // MainProgressBar.ShowProgressValue = !indeterminate;
                    if (indeterminate)
                    {
                        MainProgressBar.EditValue = null;
                        ProgressText.Text = "Processing...";
                    }
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleted || CancelButton.Content.ToString() == "Close")
            {
                // Close the window
                DialogResult = true;
                Close();
            }
            else
            {
                // Request cancellation
                var result = DXMessageBox.Show(
                    "Are you sure you want to cancel the analysis?",
                    "Cancel Analysis",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource?.Cancel();
                    OperationLabel.Text = "Canceling analysis...";
                    DetailsLabel.Text = "Please wait while the analysis is safely canceled.";
                    CancelButton.IsEnabled = false;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// Static method to create and show progress window
        /// </summary>
        public static ProgressWindow CreateAndShow(Window? owner = null, string initialOperation = "Initializing...")
        {
            var progressWindow = new ProgressWindow();
            
            if (owner != null)
            {
                progressWindow.Owner = owner;
            }

            progressWindow.UpdateOperation(initialOperation);
            progressWindow.Show();
            
            return progressWindow;
        }

        /// <summary>
        /// Helper method for common progress updates during analysis phases
        /// </summary>
        public void UpdateAnalysisPhase(AnalysisPhase phase, int elementsProcessed = 0, int totalElements = 0, string? currentItem = null, string? currentOperation = null)
        {
            string operation;
            int percentage;
            string details;

            switch (phase)
            {
                case AnalysisPhase.Initializing:
                    operation = "Initializing Analysis";
                    percentage = 5;
                    details = "Preparing analysis environment...";
                    break;

                case AnalysisPhase.CollectingElements:
                    operation = "Collecting Elements";
                    percentage = 15;
                    details = $"Scanning model for fire alarm devices...";
                    break;

                case AnalysisPhase.FilteringElements:
                    operation = "Filtering Elements";
                    percentage = 25;
                    details = totalElements > 0 
                        ? $"Filtering {totalElements} elements for fire alarm devices..."
                        : "Filtering elements for analysis...";
                    break;

                case AnalysisPhase.AnalyzingElectrical:
                    operation = "Analyzing Electrical Load";
                    percentage = 45;
                    details = elementsProcessed > 0 && totalElements > 0
                        ? $"Processing device {elementsProcessed} of {totalElements}..."
                        : "Calculating electrical loads and circuit requirements...";
                    break;

                case AnalysisPhase.AnalyzingIDNAC:
                    operation = "Analyzing IDNAC Circuits";
                    percentage = 65;
                    details = "Calculating notification appliance circuit requirements...";
                    break;

                case AnalysisPhase.AnalyzingIDNET:
                    operation = "Analyzing IDNET Devices";
                    percentage = 80;
                    details = "Processing detection device network topology...";
                    break;

                case AnalysisPhase.CalculatingAmplifiers:
                    operation = "Calculating Amplifier Requirements";
                    percentage = 90;
                    details = "Determining amplifier sizing and placement...";
                    break;

                case AnalysisPhase.GeneratingResults:
                    operation = "Generating Results";
                    percentage = 95;
                    details = "Compiling analysis results and recommendations...";
                    break;

                case AnalysisPhase.Complete:
                    Complete("Analysis completed successfully!");
                    return;

                default:
                    operation = "Processing";
                    percentage = 50;
                    details = "Processing analysis...";
                    break;
            }

            UpdateProgressDetailed(operation, percentage, details, currentItem, currentOperation);
        }
    }

    /// <summary>
    /// Analysis phases for progress tracking
    /// </summary>
    public enum AnalysisPhase
    {
        Initializing,
        CollectingElements,
        FilteringElements,
        AnalyzingElectrical,
        AnalyzingIDNAC,
        AnalyzingIDNET,
        CalculatingAmplifiers,
        GeneratingResults,
        Complete,
        Cancelled
    }
}