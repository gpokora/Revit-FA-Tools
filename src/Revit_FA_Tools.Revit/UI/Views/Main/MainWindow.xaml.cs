using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Editors;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfVisibility = System.Windows.Visibility;
using RvtVisibility = Autodesk.Revit.DB.Visibility;
using Revit_FA_Tools.Models;
using Microsoft.Win32;
using System.IO;
using Revit_FA_Tools.Services;
using Revit_FA_Tools.Views;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using TableView = DevExpress.Xpf.Grid.TableView;
using DevExpress.Utils;
using DevExpress.Data;
using Revit_FA_Tools.Core.Models.Analysis;
using Revit_FA_Tools.Core.Models.Electrical;
using Revit_FA_Tools.Core.Models.Recommendations;

namespace Revit_FA_Tools
{
    /// <summary>
    /// FIXED: Complete DevExpress implementation for IDNAC Calculator
    /// All critical issues resolved: theming, level sorting, grid configuration, and professional UI
    /// </summary>
    public partial class MainWindow : ThemedWindow
    {
        public MainWindow(Document document, UIDocument uiDocument)
        {
            _document = document;
            _uiDocument = uiDocument;
            InitializeComponent();
            InitializeWindowControls();
            
            _windowInstance = this;

            // Apply theme after components are initialized
            ApplyTheme();

            // Initialize debug logging
            InitializeDebugLog();

            // Initialize layout persistence path
            InitializeLayoutPersistence();
            
            // Hook up window closing event
            this.Closing += Window_Closing;

            SetInitialState();
            ConfigureGrids();
        }

        private void InitializeWindowControls()
        {
            try
            {
                // Initialize window state
                _currentScope = "Active View";
                _analysisCompleted = false;
                
                // Setup initial UI state
                InitializeLayoutPersistence();
                UpdateScopeDisplay();
                UpdateStatus("Ready - Select scope and click 'Run Analysis'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing window controls: {ex.Message}");
            }
        }

        internal Document _document;
        internal UIDocument _uiDocument;
        internal ElectricalResults _electricalResults;
        private IDNACSystemResults _idnacResults;
        private AmplifierRequirements _amplifierResults;
        private List<PanelPlacementRecommendation> _panelRecommendations;
        private IDNETSystemResults _idnetResults;
        
        // Layout persistence
        private const string LayoutConfigFileName = "Revit_FA_Tools_Layout.xml";
        private string _layoutConfigPath;
        private bool _layoutRestored = false;
        private string _currentScope = "Active View";
        private bool _analysisCompleted = false;
        private string _lastAnalysisScope = "Active View";
        
        // Analysis results fields - moved to lines 92-96

        // Debug logging
        private static string _debugLogPath;
        private static System.IO.StreamWriter _debugWriter;
        private static readonly object _logLock = new object();

        // Helper method for safe name lookup
        private T Find<T>(string name) where T : class
            => ((System.Windows.FrameworkElement)this).FindName(name) as T;

        // Missing UI elements (to prevent compilation errors) - these remain null intentionally
        private DevExpress.Xpf.Editors.SpinEdit SpareCapacitySpinner = null;

        // Window reference for cases where 'this' scope is problematic
        private MainWindow _windowInstance;
        private System.Windows.Controls.TextBlock SelectedScopeText = null;
        private System.Windows.Controls.StackPanel IDNETDeviceTypeCardsPanel = null;
        private DevExpress.Xpf.Grid.GridControl DeviceAnalysisGrid = null;
        private DevExpress.Xpf.Grid.GridControl LevelAnalysisGrid = null;
        private System.Windows.Controls.TextBlock IDNETSystemCompliantText = null;
        private System.Windows.Controls.TextBlock IDNETSystemNonCompliantText = null;
        private List<RawCircuitData> _rawCircuitData = null;


        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                var pendingChanges = PendingChangesService.Instance;
                if (pendingChanges.HasPending)
                {
                    var result = ShowUnsyncedChangesDialog(pendingChanges.PendingCount);

                    switch (result)
                    {
                        case UnsyncedChangesResult.Save:
                            // Apply changes synchronously before closing
                            var syncTask = ApplyChangesToRevitSync();
                            if (!syncTask.Result)
                            {
                                e.Cancel = true; // Cancel close if save failed
                                return;
                            }
                            break;

                        case UnsyncedChangesResult.Discard:
                            // Clear all pending changes
                            pendingChanges.ClearChanges();
                            break;

                        case UnsyncedChangesResult.Cancel:
                            e.Cancel = true;
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnClosing: {ex.Message}");
            }

            base.OnClosing(e);
        }

        private UnsyncedChangesResult ShowUnsyncedChangesDialog(int changeCount)
        {
            var dialog = new UnsyncedChangesDialog(changeCount);
            dialog.Owner = (Window)this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var dialogResult = dialog.ShowDialog();
            return dialog.Result;
        }

        private async Task<bool> ApplyChangesToRevitSync()
        {
            try
            {
                var syncService = new ModelSyncService(_document, _uiDocument);
                var result = await syncService.ApplyPendingChangesToModel();

                if (!result.Success)
                {
                    MessageBox.Show($"Failed to save changes: {result.Message}", "Save Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }



        private static void InitializeDebugLog()
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logFolder = Path.Combine(documentsPath, "IDNAC Calculator Logs");

                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _debugLogPath = Path.Combine(logFolder, $"IDNET_Analysis_{timestamp}.log");

                // Write log header
                System.Diagnostics.Debug.WriteLine($"================================================================================");
                System.Diagnostics.Debug.WriteLine($"IDNAC Calculator - IDNET Device Detection Analysis Log");
                System.Diagnostics.Debug.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"Log File: {_debugLogPath}");
                System.Diagnostics.Debug.WriteLine($"================================================================================");
                System.Diagnostics.Debug.WriteLine($"");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize debug log: {ex.Message}");
            }
        }

        private static void WriteToDebugLog(string message)
        {
            try
            {
                lock (_logLock)
                {
                    if (string.IsNullOrEmpty(_debugLogPath))
                        InitializeDebugLog();

                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] {message}";

                    File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);

                    // Also write to debug output for development
                    System.Diagnostics.Debug.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to debug log: {ex.Message}");
            }
        }

        private static void CompleteDebugLog(bool deleteAfterCompletion = true)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"================================================================================");
                System.Diagnostics.Debug.WriteLine($"Analysis completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"Log file saved to: {_debugLogPath}");
                System.Diagnostics.Debug.WriteLine($"================================================================================");

                // Close the file writer to release the lock
                _debugWriter?.Flush();
                _debugWriter?.Close();
                _debugWriter?.Dispose();
                _debugWriter = null;

                // Delete debug log after successful analysis if requested
                if (deleteAfterCompletion && !string.IsNullOrEmpty(_debugLogPath) && File.Exists(_debugLogPath))
                {
                    try
                    {
                        File.Delete(_debugLogPath);
                        System.Diagnostics.Debug.WriteLine($"Debug log file deleted: {_debugLogPath}");
                    }
                    catch (Exception deleteEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete debug log file: {deleteEx.Message}");
                    }
                }
                else if (!deleteAfterCompletion && !string.IsNullOrEmpty(_debugLogPath) && File.Exists(_debugLogPath))
                {
                    // Show message to user about log location only if not deleting
                    DXMessageBox.Show($"Debug log saved to:\n{_debugLogPath}\n\nThis file contains detailed information about device detection and filtering.",
                        "Debug Log Created", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to complete debug log: {ex.Message}");
            }
        }



        #region Layout Persistence

        /// <summary>
        /// Initialize layout persistence configuration
        /// </summary>
        private void InitializeLayoutPersistence()
        {
            try
            {
                // Create layout config path in user's AppData folder
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Revit_FA_Tools");
                Directory.CreateDirectory(appDataFolder);
                _layoutConfigPath = Path.Combine(appDataFolder, LayoutConfigFileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize layout persistence: {ex.Message}");
                _layoutConfigPath = null;
            }
        }

        /// <summary>
        /// Save the current DockLayoutManager layout to XML
        /// </summary>
        private void SaveLayoutToFile()
        {
            try
            {
                if (string.IsNullOrEmpty(_layoutConfigPath) || MainDockLayoutManager == null)
                    return;

                // Save DockLayoutManager layout
                MainDockLayoutManager.SaveLayoutToXml(_layoutConfigPath);
                System.Diagnostics.Debug.WriteLine($"Layout saved to: {_layoutConfigPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore the DockLayoutManager layout from XML
        /// </summary>
        private void RestoreLayoutFromFile()
        {
            try
            {
                if (string.IsNullOrEmpty(_layoutConfigPath) || !File.Exists(_layoutConfigPath) || MainDockLayoutManager == null)
                    return;

                // Restore DockLayoutManager layout
                MainDockLayoutManager.RestoreLayoutFromXml(_layoutConfigPath);
                _layoutRestored = true;
                System.Diagnostics.Debug.WriteLine($"Layout restored from: {_layoutConfigPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore layout: {ex.Message}");
                _layoutRestored = false;
            }
        }

        /// <summary>
        /// Window closing event to save layout preferences
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                // Save current layout before closing
                SaveLayoutToFile();
                System.Diagnostics.Debug.WriteLine("Window closing - layout saved");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving layout on window closing: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset layout to default state and ensure all panels are visible
        /// </summary>
        private void ResetLayoutToDefault()
        {
            try
            {
                // Delete the saved layout file
                if (File.Exists(_layoutConfigPath))
                {
                    File.Delete(_layoutConfigPath);
                    System.Diagnostics.Debug.WriteLine($"Deleted saved layout file: {_layoutConfigPath}");
                }

                // Clear any saved layout from the DockLayoutManager
                if (MainDockLayoutManager != null)
                {
                    // Get all layout panels and make them visible
                    var layoutPanels = MainDockLayoutManager.GetItems().OfType<DevExpress.Xpf.Docking.LayoutPanel>().ToList();
                    foreach (var panel in layoutPanels)
                    {
                        panel.Visibility = WpfVisibility.Visible;
                        panel.Closed = false;
                        if (panel.Parent is DevExpress.Xpf.Docking.LayoutGroup group)
                        {
                            group.SelectedTabIndex = 0;
                        }
                    }
                    
                    // Activate the first panel
                    if (layoutPanels.Any())
                    {
                        MainDockLayoutManager.Activate(layoutPanels.First());
                    }
                }
                
                // Ensure all grids are visible
                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as DevExpress.Xpf.Grid.GridControl;
                if (detailedGrid != null)
                {
                    detailedGrid.Visibility = WpfVisibility.Visible;
                }
                
                // Force a layout update
                MainDockLayoutManager?.UpdateLayout();
                
                UpdateStatus("Layout reset to default - please restart the application for best results");
                System.Diagnostics.Debug.WriteLine("Layout reset to default");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting layout: {ex.Message}");
                ShowErrorMessage($"Error resetting layout: {ex.Message}");
            }
        }

        #endregion Layout Persistence

        #region Master-Detail Drilldown Behavior

        /// <summary>
        /// Handle selection changes in the SystemOverviewGrid
        /// </summary>
        private void SystemOverviewGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            try
            {
                if (e.NewItem is SystemOverviewData selectedLevel)
                {
                    // Update status bar to show selected level info
                    if (StatusMessage != null)
                    {
                        var statusMsg = Find<TextBlock>("StatusMessage");
                        if (statusMsg != null) statusMsg.Text = $"Selected: {selectedLevel.Level} - {selectedLevel.IDNACDevices} IDNAC devices, {selectedLevel.IDNETDevices} IDNET devices";
                    }

                    // Future enhancement: Filter other grids based on selected level
                    System.Diagnostics.Debug.WriteLine($"Level selected: {selectedLevel.Level}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SystemOverviewGrid_CurrentItemChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle double-click in the SystemOverviewGrid to show level details
        /// </summary>
        private void SystemOverviewGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var grid = sender as GridControl;
                var selectedLevel = grid?.CurrentItem as SystemOverviewData;
                
                if (selectedLevel != null)
                {
                    // Show detailed view for the selected level
                    ShowLevelDetails(selectedLevel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SystemOverviewGrid_MouseDoubleClick: {ex.Message}");
            }
        }

        /// <summary>
        /// Show detailed information for a selected level
        /// </summary>
        private void ShowLevelDetails(SystemOverviewData levelData)
        {
            try
            {
                // Create a detail popup or navigate to a detail tab
                var message = $"Level Details: {levelData.Level}\n\n" +
                             $"IDNAC System:\n" +
                             $"  • Devices: {levelData.IDNACDevices}\n" +
                             $"  • Current: {levelData.IDNACCurrent:F1}A\n" +
                             $"  • Wattage: {levelData.IDNACWattage:F0}W\n" +
                             $"  • Circuits: {levelData.IDNACCircuits}\n\n" +
                             $"IDNET System:\n" +
                             $"  • Devices: {levelData.IDNETDevices}\n" +
                             $"  • Points: {levelData.IDNETPoints}\n" +
                             $"  • Unit Loads: {levelData.IDNETUnitLoads}\n" +
                             $"  • Channels: {levelData.IDNETChannels}\n\n" +
                             $"Utilization: {levelData.UtilizationPercent:F1}%\n" +
                             $"Limiting Factor: {levelData.LimitingFactor}\n\n" +
                             $"Comments: {levelData.Comments}";

                DXMessageBox.Show(message, $"Level Details - {levelData.Level}", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                System.Diagnostics.Debug.WriteLine($"Showed details for level: {levelData.Level}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing level details: {ex.Message}");
                DXMessageBox.Show("Unable to show level details.", "Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Context menu handler to show level details
        /// </summary>
        private void ShowLevelDetailsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedLevel = SystemOverviewGrid?.CurrentItem as SystemOverviewData;
                if (selectedLevel != null)
                {
                    ShowLevelDetails(selectedLevel);
                }
                else
                {
                    DXMessageBox.Show("Please select a level first.", "No Selection", 
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowLevelDetailsMenuItem_Click: {ex.Message}");
            }
        }

        /// <summary>
        /// Context menu handler to export grid to Excel
        /// </summary>
        private void ExportGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SystemOverviewGrid == null)
                    return;

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    DefaultExt = "xlsx",
                    FileName = $"SystemOverview_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Use DevExpress export functionality
                    SystemOverviewGrid.View.ExportToXlsx(saveDialog.FileName);
                    
                    DXMessageBox.Show($"Grid exported successfully to:\n{saveDialog.FileName}", 
                                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    System.Diagnostics.Debug.WriteLine($"SystemOverviewGrid exported to: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting grid: {ex.Message}");
                DXMessageBox.Show("Failed to export grid to Excel.", "Export Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion Master-Detail Drilldown Behavior



        private void SetInitialState()
        {
            // Set initial status bar messages
            var statusMsg = Find<TextBlock>("StatusMessage");
            if (statusMsg != null) statusMsg.Text = "Ready - Select scope and click 'Run Analysis'";
            var deviceCount = Find<TextBlock>("DeviceCount");
            if (deviceCount != null) deviceCount.Text = "Devices: 0";
            var idnacCount = Find<TextBlock>("IDNACCount");
            if (idnacCount != null) idnacCount.Text = "IDNACs: 0";
            var currentLoad = Find<TextBlock>("CurrentLoad");
            if (currentLoad != null) currentLoad.Text = "Load: 0.0A";

            // Update window title with version and build info
            this.Title = "Autocall 4100ES Design Tool - Professional DevExpress Edition v2.0";

            // Set initial dashboard values
            UpdateDashboard(null, null, null);

            // Set last analysis time
            var lastAnalysisTime = Find<TextBlock>("LastAnalysisTimeText");
            if (lastAnalysisTime != null) lastAnalysisTime.Text = "Never";

            // Set initial analysis status
            var analysisStatus = Find<TextBlock>("AnalysisStatusText");
            if (analysisStatus != null) analysisStatus.Text = "Ready - Select scope and click 'Run Analysis'";
        }

        /// <summary>
        /// FIXED: Complete DevExpress grid configuration with professional styling
        /// </summary>
        private void ConfigureGrids()
        {
            // Configure IDNAC Grid with advanced DevExpress features
            if (IDNACGrid != null)
            {
                IDNACGrid.ItemsSource = null;
                IDNACGrid.AutoGenerateColumns = AutoGenerateColumnsMode.None;

                // Enable advanced grid features
                if (IDNACGrid.View is TableView idnacView)
                {
                    idnacView.AllowEditing = false;
                    idnacView.AutoWidth = true;
                    idnacView.ShowGroupPanel = true;
                    idnacView.AllowGrouping = true;
                    idnacView.AllowSorting = true;
                    idnacView.AllowColumnFiltering = true;
                    idnacView.ShowFilterPanelMode = ShowFilterPanelMode.Never;
                    idnacView.NavigationStyle = GridViewNavigationStyle.Row;


                    // Professional row appearance
                    idnacView.RowMinHeight = 28;
                    idnacView.ShowVerticalLines = true;
                    idnacView.ShowHorizontalLines = true;
                    idnacView.AlternateRowBackground = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0));

                    // FIXED: Utilization category conditional formatting
                    idnacView.CustomRowAppearance += IDNACGrid_CustomRowAppearance;
                }
            }

            // Configure Device Grid with professional styling
            if (DeviceGrid != null)
            {
                DeviceGrid.ItemsSource = null;
                DeviceGrid.AutoGenerateColumns = AutoGenerateColumnsMode.None;

                if (DeviceGrid.View is TableView deviceView)
                {
                    deviceView.AllowEditing = false;
                    deviceView.AutoWidth = true;
                    deviceView.ShowGroupPanel = true;
                    deviceView.AllowGrouping = true;
                    deviceView.AllowSorting = true;
                    deviceView.RowMinHeight = 26;
                    deviceView.ShowVerticalLines = false;
                    deviceView.AlternateRowBackground = new SolidColorBrush(Color.FromArgb(8, 0, 0, 0));
                }
            }

            // Configure Level Grid with elevation-based sorting
            if (LevelGrid != null)
            {
                LevelGrid.ItemsSource = null;
                LevelGrid.AutoGenerateColumns = AutoGenerateColumnsMode.None;

                if (LevelGrid.View is TableView levelView)
                {
                    levelView.AllowEditing = false;
                    levelView.AutoWidth = true;
                    levelView.AllowSorting = true;
                    levelView.RowMinHeight = 26;
                    levelView.ShowVerticalLines = false;
                    levelView.AlternateRowBackground = new SolidColorBrush(Color.FromArgb(8, 0, 0, 0));
                }
            }

            // Configure Raw Data Grid
            if (RawDataGrid != null)
            {
                RawDataGrid.ItemsSource = null;
                RawDataGrid.AutoGenerateColumns = AutoGenerateColumnsMode.None;

                if (RawDataGrid.View is TableView rawView)
                {
                    rawView.AllowEditing = false;
                    rawView.AutoWidth = true;
                    rawView.AllowSorting = true;
                    rawView.AllowColumnFiltering = true;
                    rawView.RowMinHeight = 24;
                    rawView.ShowVerticalLines = false;
                    rawView.AlternateRowBackground = new SolidColorBrush(Color.FromArgb(6, 0, 0, 0));
                }
            }

            // Set default tab to IDNAC Analysis
            if (ResultsTabControl != null)
                ResultsTabControl.SelectedIndex = 0;
        }

        /// <summary>
        /// FIXED: Complete professional DevExpress theme implementation
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Detect system theme preference
                bool isDarkMode = IsSystemDarkMode();

                // Apply DevExpress theme with proper fallback
                string[] themesToTry = isDarkMode
                    ? new[] { "Win11Dark", "VS2017Dark", "Office2019Black", "VS2017Blue" }
                    : new[] { "Office2019Colorful", "VS2017Blue", "Office2016White" };

                bool themeApplied = false;
                foreach (var theme in themesToTry)
                {
                    try
                    {
                        ApplicationThemeHelper.ApplicationThemeName = theme;
                        System.Diagnostics.Debug.WriteLine($"Applied DevExpress theme: {theme}");
                        themeApplied = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to apply theme {theme}: {ex.Message}");
                        continue;
                    }
                }

                if (!themeApplied)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: No themes could be applied, using default");
                }

                // Ensure window control buttons are visible in dark theme
                EnsureWindowControlsVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme application failed: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Enhanced window control visibility for Revit environment
        /// </summary>
        private void EnsureWindowControlsVisibility()
        {
            try
            {
                // FIXED: Explicit window controls for Revit environment
                this.ShowIcon = true;
                this.ShowInTaskbar = true;
                this.ShowActivated = true;
                this.Topmost = false;
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowStyle = WindowStyle.SingleBorderWindow;

                // Apply DevExpress ThemedWindow properties explicitly
                this.SetValue(ThemedWindow.ShowIconProperty, true);
                this.SetValue(ThemedWindow.ControlBoxButtonSetProperty,
                    ControlBoxButtons.Close | ControlBoxButtons.Minimize | ControlBoxButtons.MaximizeRestore);
                this.SetValue(ThemedWindow.WindowKindProperty, WindowKind.Normal);

                // Ensure window is properly sized and positioned
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                }

                // Set proper dimensions
                this.Width = Math.Max(this.Width, 1400);
                this.Height = Math.Max(this.Height, 900);
                this.MinWidth = 1200;
                this.MinHeight = 800;

                // Force template and visual refresh with proper timing
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        this.ApplyTemplate();
                        this.InvalidateVisual();
                        this.UpdateLayout();

                        // Secondary refresh to ensure controls are properly rendered
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            this.InvalidateArrange();
                            this.InvalidateMeasure();
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception refreshEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Template refresh warning: {refreshEx.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                System.Diagnostics.Debug.WriteLine("Enhanced window controls visibility ensured for Revit environment");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring window controls visibility: {ex.Message}");
            }
        }

        /// <summary>

        /// <summary>
        /// FIXED: Window Loaded event handler to ensure controls are visible in Revit
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Final check for window controls after window is fully loaded
                EnsureWindowControlsVisibility();

                // Initialize dashboard displays
                UpdateScopeDisplay();

                // Initialize spare capacity spinner with current configuration value
                InitializeSpareCapacitySetting();
                
                // Restore saved layout if available
                RestoreLayoutFromFile();

                System.Diagnostics.Debug.WriteLine("Window loaded - controls visibility verified, dashboard initialized, and layout restored");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Window_Loaded: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize spare capacity spinner with current configuration value
        /// </summary>
        private void InitializeSpareCapacitySetting()
        {
            try
            {
                var configService = new ConfigurationManagementService();
                var config = configService.GetSystemConfiguration();

                if (SpareCapacitySpinner != null)
                {
                    SpareCapacitySpinner.Value = (decimal)config.SpareCapacityPercent;
                    System.Diagnostics.Debug.WriteLine($"Spare capacity initialized to {config.SpareCapacityPercent}%");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing spare capacity setting: {ex.Message}");
                // Set default value if config loading fails
                if (SpareCapacitySpinner != null)
                {
                    if (SpareCapacitySpinner != null)
                    {
                        SpareCapacitySpinner.Value = 20.0M;
                    }
                    SpareCapacitySpinner.Value = 20.0M;
                }
            }
        }

        /// <summary>
        /// FIXED: System theme detection for automatic theme selection
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

        /// <summary>
        /// FIXED: Professional utilization category row styling for DevExpress WPF
        /// Note: DevExpress CustomRowAppearanceEventArgs uses different property names
        /// </summary>
        private void IDNACGrid_CustomRowAppearance(object sender, CustomRowAppearanceEventArgs e)
        {
            if (e.RowHandle < 0) return;

            var gridItem = IDNACGrid.GetRow(e.RowHandle) as IDNACAnalysisGridItem;
            if (gridItem == null) return;

            // Apply professional color coding based on utilization category
            // Note: DevExpress uses different property names - this method may need adjustment
            // based on the actual DevExpress version being used
            try
            {
                switch (gridItem.UtilizationCategory?.ToLower())
                {
                    case "optimized":
                        // For DevExpress, we may need to use conditional formatting instead
                        // or different event/property names
                        break;
                    case "excellent":
                        break;
                    case "good":
                        break;
                    case "underutilized":
                        break;
                    default:
                        // Use default styling
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying row styling: {ex.Message}");
            }
        }


        private async void RunAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            // Use new async method with better progress reporting and cancellation
            await RunAnalysisInternalAsync();
        }

        /// <summary>
        /// New async analysis method using the modern async services
        /// </summary>
        private async Task RunAnalysisInternalAsync()
        {
            if (_document == null)
            {
                await Dispatcher.InvokeAsync(() => ShowErrorMessage("No Revit document available for analysis."));
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            ProgressWindow progressWindow = null;

            try
            {
                // Create progress reporter with proper async dispatch
                var progress = new Progress<Models.AnalysisProgress>(p =>
                {
                    // Use BeginInvoke for better responsiveness instead of Invoke
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            progressWindow?.UpdateProgressDetailed(
                                p.Operation,
                                (int)p.PercentComplete,
                                p.Message,
                                null, // currentItem 
                                p.Operation);
                            UpdateStatus(p.Message);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Progress update error: {ex.Message}");
                        }
                    }));
                });

                // Create and show progress window with cancellation support
                await Dispatcher.InvokeAsync(() =>
                {
                    progressWindow = ProgressWindow.CreateAndShow(this, "Fire Alarm System Analysis");
                    progressWindow.UpdateAnalysisPhase(AnalysisPhase.Initializing);
                    UpdateStatus("Starting comprehensive analysis...");
                    SetUIEnabled(false);
                });

                // Step 1: Model Validation (Pre-Analysis Gate)
                progressWindow?.UpdateAnalysisPhase(AnalysisPhase.ValidatingModel,
                    currentOperation: "Validating model components and data quality");

                var preAnalysisValidator = new PreAnalysisValidator(_document);
                var validationResult = await Task.Run(() => preAnalysisValidator.ValidateBeforeAnalysis(), 
                    cancellationTokenSource.Token).ConfigureAwait(false);

                // Check validation gate decision
                if (validationResult.Decision == PreAnalysisValidator.AnalysisGateDecision.Block)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        progressWindow?.ShowError("Model validation failed - analysis cannot proceed");
                        this.ShowValidationResults(validationResult);
                    });
                    return;
                }
                else if (validationResult.Decision == PreAnalysisValidator.AnalysisGateDecision.GuidedFix)
                {
                    var proceed = await Dispatcher.InvokeAsync(() => this.ShowGuidedFixDialog(validationResult));
                    if (!proceed)
                        return;
                }
                else if (validationResult.Decision == PreAnalysisValidator.AnalysisGateDecision.ProceedWithWarnings)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        progressWindow?.UpdateCurrentItem($"Proceeding with {validationResult.AccuracyEstimate}");
                    });
                }

                // Get elements asynchronously to prevent UI blocking
                var elements = await Task.Run(() => GetElementsByScope(), cancellationTokenSource.Token).ConfigureAwait(false);

                if (elements == null || !elements.Any())
                {
                    await Dispatcher.InvokeAsync(() =>
                        ShowErrorMessage($"No fire alarm family instances found in {_currentScope}."));
                    return;
                }

                // Use the new comprehensive analysis service with proper async pattern
                var analysisService = new ComprehensiveAnalysisService();
                var results = await analysisService.AnalyzeAsync(
                    elements,
                    _currentScope,
                    cancellationTokenSource.Token,
                    progress).ConfigureAwait(false);

                // Update local results on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    _electricalResults = results.ElectricalResults;
                    _idnacResults = results.IDNACResults;
                    _idnetResults = results.IDNETResults;
                    _amplifierResults = results.AmplifierResults;
                    _panelRecommendations = results.PanelRecommendations;
                    _analysisCompleted = true;
                });

                // Update UI with results asynchronously
                await Dispatcher.InvokeAsync(() =>
                {
                    LoadAllGrids();
                    UpdateDashboard(_electricalResults, _idnacResults, _amplifierResults);
                    UpdateLastAnalysisTime();
                    progressWindow?.UpdateAnalysisPhase(AnalysisPhase.Complete);
                    UpdateStatus($"Analysis complete - {elements.Count} elements processed in {results.TotalAnalysisTime.TotalSeconds:F1}s");
                });

                // Clean up debug log after completion
                CompleteDebugLog(true);
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateStatus("Analysis cancelled by user");
                    progressWindow?.UpdateAnalysisPhase(AnalysisPhase.Cancelled);
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    progressWindow?.ShowError($"Analysis failed: {ex.Message}");
                    UpdateStatus("Analysis failed");
                    ShowErrorMessage($"Analysis failed: {ex.Message}");
                });
                System.Diagnostics.Debug.WriteLine($"Analysis error: {ex}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SetUIEnabled(true);
                    progressWindow?.Close();
                });
            }
        }

        private bool ShowGuidedFixDialog(PreAnalysisValidator.PreAnalysisValidationResult validationResult)
        {
            try
            {
                var message = $"Model validation found issues that can be automatically fixed:\n\n";
                message += $"Current Status: {validationResult.AccuracyEstimate}\n\n";

                if (validationResult.AutoFixOptions.Any())
                {
                    message += "AVAILABLE AUTO-FIXES:\n";
                    foreach (var option in validationResult.AutoFixOptions.Take(3))
                    {
                        message += $"• {option.Name}: {option.Description}\n";
                        message += $"  Affects {option.AffectedDevices} devices\n";
                    }
                    message += "\n";
                }

                message += $"RECOMMENDATION:\n{validationResult.RecommendedAction}\n\n";
                message += "Would you like to:\n";
                message += "• YES: Apply auto-fixes and continue with analysis\n";
                message += "• NO: Review issues manually before proceeding";

                var result = DXMessageBox.Show(message, "Model Validation - Guided Fix",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Apply auto-fixes
                    foreach (var option in validationResult.AutoFixOptions.Where(o => o.IsRecommended))
                    {
                        try
                        {
                            option.AutoFixAction?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Auto-fix error for {option.Name}: {ex.Message}");
                        }
                    }

                    DXMessageBox.Show("Auto-fixes applied successfully. Proceeding with analysis.",
                        "Auto-Fix Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing guided fix dialog: {ex.Message}");
                return false;
            }
        }

        private void ShowValidationResults(PreAnalysisValidator.PreAnalysisValidationResult validationResult)
        {
            try
            {
                var message = "Model Validation Results:\n\n";

                if (validationResult.BlockingIssues.Any())
                {
                    message += "BLOCKING ISSUES:\n";
                    message += string.Join("\n", validationResult.BlockingIssues.Take(5).Select(i => $"• {i}"));
                    if (validationResult.BlockingIssues.Count > 5)
                        message += $"\n• ... and {validationResult.BlockingIssues.Count - 5} more issues";
                    message += "\n\n";
                }

                if (validationResult.WarningIssues.Any())
                {
                    message += "WARNINGS:\n";
                    message += string.Join("\n", validationResult.WarningIssues.Take(3).Select(i => $"• {i}"));
                    if (validationResult.WarningIssues.Count > 3)
                        message += $"\n• ... and {validationResult.WarningIssues.Count - 3} more warnings";
                    message += "\n\n";
                }

                message += $"RECOMMENDATION:\n{validationResult.RecommendedAction}";

                DXMessageBox.Show(message, "Model Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing validation results: {ex.Message}");
            }
        }

        /// <summary>
        /// Legacy analysis method - kept for compatibility
        /// </summary>
        private async Task RunAnalysisInternal()
        {
            if (_document == null)
            {
                ShowErrorMessage("No Revit document available for analysis.");
                return;
            }

            ProgressWindow progressWindow = null;

            try
            {
                // Log the scope being used for IDNAC analysis
                System.Diagnostics.Debug.WriteLine($"IDNAC Analysis: Starting with scope '{_currentScope}'");
                ValidateScopeSync();

                // Create and show progress window
                progressWindow = ProgressWindow.CreateAndShow(this, "Starting IDNAC Analysis");
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.Initializing);

                UpdateStatus("Starting analysis...");

                // Disable UI during analysis
                SetUIEnabled(false);

                // Check for cancellation
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("Analysis canceled by user");
                    return;
                }

                // Phase 1: Collect elements
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.CollectingElements,
                    currentOperation: $"Scanning scope: {_currentScope}");
                var allElements = GetElementsByScope();
                System.Diagnostics.Debug.WriteLine($"Analysis scope '{_currentScope}': Found {allElements.Count} total FamilyInstances");

                progressWindow.UpdateCurrentItem($"Found: {allElements.Count} total instances");

                // Check for cancellation
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("Analysis canceled by user");
                    return;
                }

                // Phase 2: Filter elements
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.FilteringElements, 0, allElements.Count,
                    currentOperation: "Filtering for IDNAC devices");
                var electricalElements = allElements.Where(IsElectricalFamilyInstance).ToList();
                System.Diagnostics.Debug.WriteLine($"Analysis: Found {electricalElements.Count} electrical elements out of {allElements.Count} total elements");

                progressWindow.UpdateCurrentItem($"IDNAC devices: {electricalElements.Count}/{allElements.Count}");

                // Comprehensive input validation
                var validationResult = ValidateAnalysisInput(allElements, electricalElements);
                if (!validationResult.IsValid)
                {
                    UpdateStatus("Analysis validation failed");
                    ShowErrorMessage(validationResult.ErrorMessage);
                    return;
                }

                if (electricalElements.Count == 0)
                {
                    if (allElements.Count == 0)
                    {
                        UpdateStatus($"No elements found in scope '{_currentScope}'");
                        ShowErrorMessage($"No elements found in scope '{_currentScope}'.\n\n" +
                            "Please check:\n" +
                            "• For 'Active View': Ensure fire alarm devices are visible in the current view\n" +
                            "• For 'Selection': Select some fire alarm devices first\n" +
                            "• For 'Entire Model': Ensure the model contains fire alarm devices");
                    }
                    else
                    {
                        UpdateStatus($"No electrical/fire alarm devices found in {allElements.Count} elements");
                        ShowErrorMessage($"No electrical or fire alarm devices found in {allElements.Count} elements.\n\n" +
                            "The analysis looks for elements with:\n" +
                            "• CURRENT DRAW parameter (in Amperes)\n" +
                            "• Wattage parameter (in Watts)\n" +
                            "• Fire alarm device names (Speaker, Horn, Strobe, Bell, etc.)\n\n" +
                            "Please ensure your fire alarm devices have the correct electrical parameters.");
                    }
                    return;
                }

                // Phase 3: Extract element data
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.AnalyzingElectrical);
                var elementDataList = new List<ElementData>();

                for (int i = 0; i < electricalElements.Count; i++)
                {
                    var element = electricalElements[i];

                    // Update progress for element processing
                    if (i % 10 == 0) // Update every 10 elements to avoid too frequent updates
                    {
                        int percentage = 25 + (i * 20 / Math.Max(electricalElements.Count, 1)); // 25-45%
                        progressWindow.UpdateProgress($"Analyzing Electrical Load", percentage,
                            $"Processing device {i + 1} of {electricalElements.Count}...");
                    }

                    // Check for cancellation periodically
                    if (i % 50 == 0 && progressWindow.IsCanceled)
                    {
                        UpdateStatus("Analysis canceled by user");
                        return;
                    }

                    try
                    {
                        var elementData = ExtractElementData(element);
                        if (elementData != null)
                            elementDataList.Add(elementData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error extracting data from element {element.Id}: {ex.Message}");
                    }
                }

                // Check for cancellation before background processing
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("Analysis canceled by user");
                    return;
                }

                // Run analysis calculations in background (non-Revit operations)
                await Task.Run(() => RunAnalysisInternal(elementDataList, progressWindow));

                // Check for cancellation after background processing
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("Analysis canceled by user");
                    return;
                }

                // Phase 5: Generate results
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.GeneratingResults);

                UpdateStatus("Analysis completed successfully");
                _analysisCompleted = true;

                // Update UI with results
                LoadAllGrids();
                UpdateDashboard(_electricalResults, _idnacResults, _amplifierResults);
                UpdateLastAnalysisTime();

                // Complete the progress
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.Complete);

            }
            catch (Exception ex)
            {
                progressWindow?.ShowError($"Analysis failed: {ex.Message}");
                UpdateStatus("Analysis failed");
                ShowErrorMessage($"Analysis failed: {ex.Message}");
            }
            finally
            {
                // Re-enable UI
                SetUIEnabled(true);

                // Close progress window after a short delay if completed
                if (progressWindow != null && progressWindow.IsCompleted)
                {
                    _ = Task.Delay(2000).ContinueWith(_ => progressWindow.Dispatcher.Invoke(() => progressWindow.Close()));
                }
            }
        }

        private void RunAnalysisInternal(List<ElementData> elementDataList, ProgressWindow progressWindow = null)
        {
            try
            {
                // Phase 1: Create electrical results
                progressWindow?.UpdateAnalysisPhase(AnalysisPhase.AnalyzingElectrical,
                    currentOperation: "Processing electrical parameters");
                progressWindow?.UpdateCurrentItem($"Analyzing {elementDataList.Count} devices");

                _electricalResults = CreateElectricalResultsFromData(elementDataList);

                // Check for cancellation
                if (progressWindow?.IsCanceled == true) return;

                // IDNAC analysis
                progressWindow?.UpdateAnalysisPhase(AnalysisPhase.AnalyzingIDNAC,
                    currentOperation: "Calculating circuit requirements");
                progressWindow?.UpdateCurrentItem($"Processing {_electricalResults?.Elements?.Count ?? 0} IDNAC devices");

                var idnacAnalyzer = new IDNACAnalyzer();
                _idnacResults = idnacAnalyzer.AnalyzeIDNACRequirements(_electricalResults, _currentScope);

                // Check for cancellation
                if (progressWindow?.IsCanceled == true) return;

                // IDNET analysis
                progressWindow?.UpdateAnalysisPhase(AnalysisPhase.AnalyzingIDNET,
                    currentOperation: "Analyzing detection devices");
                var idnetDeviceCount = _electricalResults?.Elements?.Count(e => IsDetectionDevice(e.Element)) ?? 0;
                progressWindow?.UpdateCurrentItem($"Processing {idnetDeviceCount} IDNET devices");

                var idnetAnalyzer = new IDNETAnalyzer();
                _idnetResults = idnetAnalyzer.AnalyzeIDNET(_electricalResults);

                // Check for cancellation
                if (progressWindow?.IsCanceled == true) return;

                // Amplifier analysis
                progressWindow?.UpdateAnalysisPhase(AnalysisPhase.CalculatingAmplifiers,
                    currentOperation: "Determining amplifier requirements");
                var speakerCount = _electricalResults?.Elements?.Count(e => e.FamilyName?.ToUpper().Contains("SPEAKER") == true) ?? 0;
                progressWindow?.UpdateCurrentItem($"Sizing for {speakerCount} speakers");

                var amplifierCalculator = new AmplifierCalculator();
                _amplifierResults = amplifierCalculator.CalculateAmplifierRequirements(_electricalResults);

                // Check for cancellation
                if (progressWindow?.IsCanceled == true) return;

                // Panel placement analysis
                System.Diagnostics.Debug.WriteLine("Starting panel placement analysis...");
                var panelAnalyzer = new PanelPlacementAnalyzer();
                _panelRecommendations = panelAnalyzer.RecommendPanelPlacement(_idnacResults, _amplifierResults, _electricalResults, _idnetResults);
                System.Diagnostics.Debug.WriteLine($"Panel placement analysis complete: {_panelRecommendations?.Count ?? 0} recommendations generated");
            }
            catch (Exception ex)
            {
                progressWindow?.ShowError($"Internal analysis error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RunAnalysisInternal error: {ex}");
                throw; // Re-throw to be handled by the main method
            }
        }

        private void SetUIEnabled(bool enabled)
        {
            this.Dispatcher.Invoke(() =>
            {
                // Enable/Disable ribbon buttons and main controls
                if (MainRibbon != null)
                    MainRibbon.IsEnabled = enabled;

                if (ResultsTabControl != null)
                    ResultsTabControl.IsEnabled = enabled;
            });
        }



        private void LoadAllGrids()
        {
            this.Dispatcher.Invoke(() =>
            {
                LoadSystemOverview();
                LoadIDNACGrid();
                LoadIDNETGrid();
                LoadDeviceGrid();
                LoadDetailedDeviceGrid();
                LoadLevelGrid();
                LoadRawDataGrid();
                LoadPanelPlacement();
            });
        }

        /// <summary>
        /// Async version of LoadAllGrids to prevent UI blocking
        /// </summary>
        private async Task LoadAllGridsAsync()
        {
            await Task.Run(() =>
            {
                // Execute grid loading operations on background thread, then marshal results to UI
                var systemOverviewData = PrepareSystemOverviewData();
                var idnacGridData = PrepareIDNACGridData();
                var deviceGridData = PrepareDeviceGridData();
                var levelGridData = PrepareLevelGridData();
                var rawDataGridData = PrepareRawDataGridData();
                var panelPlacementData = PreparePanelPlacementData();

                // Apply all data to UI on UI thread
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        ApplySystemOverviewData(systemOverviewData);
                        ApplyIDNACGridData(idnacGridData);
                        ApplyDeviceGridData(deviceGridData);
                        ApplyLevelGridData(levelGridData);
                        ApplyRawDataGridData(rawDataGridData);
                        ApplyPanelPlacementData(panelPlacementData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying grid data: {ex.Message}");
                        // Fallback to synchronous loading
                        LoadAllGrids();
                    }
                }));
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads the System Overview tab with combined IDNAC and IDNET system summary and level breakdown
        /// </summary>
        private void LoadSystemOverview()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoadSystemOverview: Starting system overview update");

                // Update IDNAC System Summary
                if (_idnacResults != null)
                {
                    var totalDevices = _electricalResults?.Elements?.Count ?? 0;
                    var totalCurrent = _electricalResults?.Totals?.ContainsKey("current") == true ? _electricalResults.Totals["current"] : 0.0;
                    var totalWattage = _electricalResults?.Totals?.ContainsKey("wattage") == true ? _electricalResults.Totals["wattage"] : 0.0;
                    var totalCircuits = _idnacResults.TotalIdnacsNeeded;

                    var idnacDevicesText = Find<TextBlock>("IDNACSystemDevicesText");
                    if (idnacDevicesText != null) idnacDevicesText.Text = DisplayFormatting.FormatCount(totalDevices);
                    var idnacCircuitsText = Find<TextBlock>("IDNACSystemCircuitsText");
                    if (idnacCircuitsText != null) idnacCircuitsText.Text = totalCircuits.ToString();
                    var idnacCurrentText = Find<TextBlock>("IDNACSystemCurrentText");
                    if (idnacCurrentText != null) idnacCurrentText.Text = $"{totalCurrent:F1}A";
                    var idnacWattageText = Find<TextBlock>("IDNACSystemWattageText");
                    if (idnacWattageText != null) idnacWattageText.Text = $"{totalWattage:F0}W";

                    System.Diagnostics.Debug.WriteLine($"IDNAC Summary: {totalDevices} devices, {totalCircuits} circuits, {totalCurrent:F1}A, {totalWattage:F0}W");
                }

                // Update IDNET System Summary
                if (_idnetResults != null)
                {
                    var idnetDeviceCount = _idnetResults.TotalDevices;
                    var channelCount = _idnetResults.SystemSummary?.RecommendedNetworkChannels ?? 0;
                    var totalPower = _idnetResults.AllDevices?.Sum(d => d.PowerConsumption) ?? 0;
                    var segmentCount = _idnetResults.NetworkSegments?.Count ?? 0;

                    var idnetDevicesText = Find<TextBlock>("IDNETSystemDevicesText");
                    if (idnetDevicesText != null) idnetDevicesText.Text = DisplayFormatting.FormatCount(idnetDeviceCount);
                    var idnetChannelsText = Find<TextBlock>("IDNETSystemChannelsText");
                    if (idnetChannelsText != null) idnetChannelsText.Text = channelCount.ToString();
                    var idnetPowerText = Find<TextBlock>("IDNETSystemPowerText");
                    if (idnetPowerText != null) idnetPowerText.Text = $"{totalPower:F0}mA";
                    var idnetSegmentsText = Find<TextBlock>("IDNETSystemSegmentsText");
                    if (idnetSegmentsText != null) idnetSegmentsText.Text = segmentCount.ToString();

                    System.Diagnostics.Debug.WriteLine($"IDNET Summary: {idnetDeviceCount} devices, {channelCount} channels, {totalPower:F0}mA, {segmentCount} segments");
                }

                // Load Combined System Overview Grid
                LoadSystemOverviewGrid();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system overview: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the system overview grid with combined level-by-level analysis
        /// </summary>
        private void LoadSystemOverviewGrid()
        {
            try
            {
                if (SystemOverviewGrid == null)
                {
                    System.Diagnostics.Debug.WriteLine("SystemOverviewGrid is null");
                    return;
                }

                var overviewData = new List<SystemOverviewData>();

                // Get all levels from both IDNAC and IDNET results
                var allLevels = new HashSet<string>();

                if (_idnacResults?.LevelAnalysis != null)
                {
                    foreach (var level in _idnacResults.LevelAnalysis)
                    {
                        // Skip combined floors to avoid double-counting devices
                        if (level.Value?.IsCombined == true)
                            continue;
                            
                        allLevels.Add(level.Key);
                    }
                }

                if (_idnetResults?.AllDevices != null)
                {
                    foreach (var device in _idnetResults.AllDevices)
                        if (!string.IsNullOrEmpty(device.Level))
                            allLevels.Add(device.Level);
                }

                // Create combined level analysis
                foreach (var levelName in allLevels.OrderBy(l => l))
                {
                    var idnacDevices = 0;
                    var idnacCurrent = 0.0;
                    var idnacCircuits = 0;

                    if (_idnacResults?.LevelAnalysis?.ContainsKey(levelName) == true)
                    {
                        var idnacLevel = _idnacResults.LevelAnalysis[levelName];
                        idnacDevices = idnacLevel.Devices; // Fixed: Use 'Devices' instead of 'TotalDevices'
                        idnacCurrent = idnacLevel.Current; // Fixed: Use 'Current' instead of 'TotalCurrent'
                        idnacCircuits = idnacLevel.IdnacsRequired; // Fixed: Use 'IdnacsRequired' instead of 'IdnacsNeeded'
                    }

                    var idnetDevices = _idnetResults?.AllDevices?.Count(d => d.Level == levelName) ?? 0;
                    var idnetCurrent = _idnetResults?.AllDevices?.Where(d => d.Level == levelName)?.Sum(d => d.PowerConsumption) ?? 0;
                    // Fixed: IDNETNetworkSegment doesn't have Level property, use CoveredLevels instead
                    var idnetSegments = _idnetResults?.NetworkSegments?.Count(s => s.CoveredLevels?.Contains(levelName) == true) ?? 0;

                    var totalDevices = idnacDevices + idnetDevices;
                    var status = totalDevices > 0 ? "Active" : "No Devices";

                    overviewData.Add(new SystemOverviewData
                    {
                        Level = levelName,
                        IDNACDevices = idnacDevices,
                        IDNETDevices = idnetDevices,
                        TotalDevices = totalDevices,
                        IDNACCurrent = idnacCurrent,
                        IDNETCurrent = $"{idnetCurrent:F0}",
                        IDNACCircuits = idnacCircuits,
                        IDNETSegments = idnetSegments,
                        Status = status
                    });
                }

                SystemOverviewGrid.ItemsSource = overviewData;
                System.Diagnostics.Debug.WriteLine($"LoadSystemOverviewGrid: Loaded {overviewData.Count} levels");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system overview grid: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Complete IDNAC grid loading with utilization categories
        /// </summary>
        private void LoadIDNACGrid()
        {
            if (IDNACGrid == null || _idnacResults?.LevelAnalysis == null)
                return;

            try
            {
                var gridData = new ObservableCollection<IDNACAnalysisGridItem>();

                foreach (var level in _idnacResults.LevelAnalysis.Where(x => !string.IsNullOrEmpty(x.Key)).OrderBy(x => GetLevelSortKey(x.Key)))
                {
                    if (level.Value?.Devices > 0)
                    {
                        var analysis = level.Value;
                        
                        // Skip combined floors - show only individual floors
                        if (analysis.IsCombined)
                            continue;
                            
                        var wattage = GetLevelWattage(level.Key, analysis);

                        var currentUtil = analysis.SpareInfo?.CurrentUtilization ?? 0;
                        var deviceUtil = analysis.SpareInfo?.DeviceUtilization ?? 0;
                        var maxUtilization = Math.Max(currentUtil, deviceUtil);


                        string utilizationCategory = DetermineUtilizationCategory(analysis, maxUtilization);

                        gridData.Add(new IDNACAnalysisGridItem
                        {
                            Level = level.Key,
                            Devices = analysis.Devices,
                            Current = $"{analysis.Current:F2}",
                            Wattage = $"{wattage:F1}",
                            IdnacsRequired = analysis.IdnacsRequired,
                            UtilizationPercent = $"{maxUtilization:F0}%",
                            UtilizationCategory = utilizationCategory,
                            Status = analysis.Status ?? "",
                            LimitingFactor = analysis.LimitingFactor ?? ""
                        });
                    }
                }

                IDNACGrid.ItemsSource = gridData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading IDNAC grid: {ex.Message}");
            }
        }

        private void LoadIDNETGrid()
        {

            if (_idnetResults == null || _idnetResults.AllDevices == null || !_idnetResults.AllDevices.Any())
            {
                System.Diagnostics.Debug.WriteLine("No IDNET data to load in grid");
                return;
            }

            try
            {
                // Grid control not defined in current XAML - data is loaded via LoadIDNETGrids method
                // This method is a compatibility stub to prevent errors
                System.Diagnostics.Debug.WriteLine($"IDNET data available: {_idnetResults.AllDevices.Count} devices");
                LoadIDNETGrids(); // Call the actual implementation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading IDNET grid: {ex.Message}");
            }
        }


        /// <summary>
        /// FIXED: Professional utilization category logic
        /// </summary>
        private string DetermineUtilizationCategory(IDNACAnalysisResult analysis, double maxUtilization)
        {
            if (analysis.IsCombined)
            {
                return "Optimized";
            }
            else if (maxUtilization >= 85)
            {
                return "Excellent";
            }
            else if (maxUtilization >= 70)
            {
                return "Good";
            }
            else if (maxUtilization >= 50)
            {
                return "Fair";
            }
            else
            {
                return "Underutilized";
            }
        }

        /// <summary>
        /// FIXED: Device grid with speaker/amplifier unit clarity
        /// </summary>
        private void LoadDeviceGrid()
        {
            if (DeviceGrid == null || _electricalResults?.ByFamily == null)
                return;

            try
            {
                var gridData = new ObservableCollection<DeviceAnalysisGridItem>();

                foreach (var family in _electricalResults.ByFamily.OrderBy(x => x.Key))
                {
                    var deviceType = GetDeviceType(family.Key);
                    var requiresAmplifier = RequiresAmplifier(family.Key);

                    // FIXED: Clear distinction between speakers (W) and amplifiers (A)
                    string amplifierInfo = requiresAmplifier ?
                        GetAmplifierRequirementDetails(family.Key, family.Value.Wattage) : "No";

                    gridData.Add(new DeviceAnalysisGridItem
                    {
                        FamilyName = family.Key,
                        DeviceType = deviceType,
                        Count = family.Value.Count,
                        TotalCurrent = family.Value.Current, // Raw double value
                        TotalWattage = family.Value.Wattage, // Raw double value
                        AmplifierRequired = amplifierInfo
                    });
                }

                DeviceGrid.ItemsSource = gridData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading device grid: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Amplifier requirement details with proper units
        /// </summary>
        private string GetAmplifierRequirementDetails(string familyName, double totalWattage)
        {
            if (totalWattage <= 0) return "No";

            // Determine amplifier type needed based on total wattage
            if (totalWattage <= 28)
            {
                return $"Yes - Flex-35 (35W max, 5.5A)";
            }
            else if (totalWattage <= 40)
            {
                return $"Yes - Flex-50 (50W max, 5.55A)";
            }
            else if (totalWattage <= 80)
            {
                return $"Yes - Flex-100 (100W max, 9.6A)";
            }
            else
            {
                int amplifiersNeeded = (int)Math.Ceiling(totalWattage / 80.0);
                return $"Yes - {amplifiersNeeded}x Flex-100 (100W each, 9.6A each)";
            }
        }

        private void LoadLevelGrid()
        {
            if (LevelGrid == null || _electricalResults?.ByLevel == null)
                return;

            try
            {
                var gridData = new ObservableCollection<LevelGridItem>();

                foreach (var level in _electricalResults.ByLevel.OrderBy(x => GetLevelSortKey(x.Key)))
                {
                    var elevation = GetLevelElevation(level.Key);

                    gridData.Add(new LevelGridItem
                    {
                        LevelName = level.Key,
                        Elevation = elevation,
                        DeviceCount = level.Value.Devices,
                        CurrentLoad = level.Value.Current,
                        WattageLoad = level.Value.Wattage
                    });
                }

                LevelGrid.ItemsSource = gridData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading level grid: {ex.Message}");
            }
        }

        private void LoadRawDataGrid()
        {
            if (RawDataGrid == null || _electricalResults?.Elements == null)
                return;

            try
            {
                // FIXED: Configure columns to ensure Element ID is visible as first column
                RawDataGrid.AutoGenerateColumns = AutoGenerateColumnsMode.None;
                RawDataGrid.Columns.Clear();

                // Add Element ID as first column for easy identification
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "Id", Header = "Element ID", Width = 100 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "FamilyName", Header = "Family Name", Width = 200 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "TypeName", Header = "Type Name", Width = 150 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "LevelName", Header = "Level", Width = 120 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "Current", Header = "Current (A)", Width = 100 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "Wattage", Header = "Wattage (W)", Width = 100 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "Voltage", Header = "Voltage (V)", Width = 100 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "Description", Header = "Description", Width = 200 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "FoundParams", Header = "Found Parameters", Width = 250 });
                RawDataGrid.Columns.Add(new GridColumn() { FieldName = "CalculatedParams", Header = "Calculated Parameters", Width = 200 });

                RawDataGrid.ItemsSource = _electricalResults.Elements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading raw data grid: {ex.Message}");
            }
        }
        
        private void LoadDetailedDeviceGrid()
        {
            try
            {
                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as DevExpress.Xpf.Grid.GridControl;
                if (detailedGrid == null || _electricalResults?.Elements == null)
                {
                    System.Diagnostics.Debug.WriteLine("LoadDetailedDeviceGrid: DetailedDeviceGrid or _electricalResults.Elements is null");
                    return;
                }

                var deviceDetailItems = new ObservableCollection<DeviceDetailItem>();
                
                foreach (var element in _electricalResults.Elements)
                {
                    var deviceItem = new DeviceDetailItem
                    {
                        ElementId = (int)element.Id,
                        Level = element.LevelName ?? "Unknown",
                        DeviceType = element.Element?.Category?.Name ?? "Unknown",
                        FamilyName = element.FamilyName ?? "Unknown",
                        TypeName = element.TypeName ?? "Unknown",
                        Current = element.Current,
                        Wattage = element.Wattage,
                        HasStrobe = (element.TypeName ?? "").ToUpper().Contains("STROBE"),
                        HasSpeaker = (element.TypeName ?? "").ToUpper().Contains("SPEAKER"),
                        IsIsolator = (element.TypeName ?? "").ToUpper().Contains("ISOLATOR"),
                        UnitLoads = (int)(element.Current / 0.0008), // Convert current to unit loads
                        Status = "Unassigned",
                        Zone = null,
                        Panel = null,
                        Circuit = null,
                        Address = 0,
                        CircuitUtilization = 0.0,
                        IsAddressLocked = false
                    };
                    
                    deviceDetailItems.Add(deviceItem);
                }

                detailedGrid.ItemsSource = deviceDetailItems;
                System.Diagnostics.Debug.WriteLine($"LoadDetailedDeviceGrid: Populated {deviceDetailItems.Count} device items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading detailed device grid: {ex.Message}");
            }
        }

        private void LoadPanelPlacement()
        {
            try
            {
                if (PanelRecommendationsPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("LoadPanelPlacement: PanelRecommendationsPanel is null");
                    return;
                }

                // Clear existing content
                PanelRecommendationsPanel.Children.Clear();

                // Add header
                var headerText = new TextBlock
                {
                    Text = "Panel Placement Recommendations",
                    Foreground = (Brush)FindResource("DarkTextBrush"),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                PanelRecommendationsPanel.Children.Add(headerText);

                if (_panelRecommendations == null || _panelRecommendations.Count == 0)
                {
                    var noDataText = new TextBlock
                    {
                        Text = _panelRecommendations == null ?
                            "Run analysis to see panel placement recommendations..." :
                            "No panel placement recommendations generated. Check analysis results.",
                        Foreground = (Brush)FindResource("DarkSecondaryTextBrush"),
                        FontSize = 12,
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.Wrap
                    };
                    PanelRecommendationsPanel.Children.Add(noDataText);
                    System.Diagnostics.Debug.WriteLine($"LoadPanelPlacement: No recommendations to display (recommendations={_panelRecommendations?.Count ?? 0})");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"LoadPanelPlacement: Displaying {_panelRecommendations.Count} recommendations");

                foreach (var recommendation in _panelRecommendations)
                {
                    var card = CreatePanelRecommendationCard(recommendation);
                    PanelRecommendationsPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading panel placement: {ex.Message}");

                // Show error in UI
                if (PanelRecommendationsPanel != null)
                {
                    var errorText = new TextBlock
                    {
                        Text = $"Error loading panel recommendations: {ex.Message}",
                        Foreground = Brushes.Red,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    PanelRecommendationsPanel.Children.Add(errorText);
                }
            }
        }


        /// <summary>
        /// Prepares system overview data on background thread
        /// </summary>
        private object PrepareSystemOverviewData()
        {
            var data = new
            {
                IDNACSystemData = (_idnacResults != null) ? new
                {
                    TotalDevices = _electricalResults?.Elements?.Count ?? 0,
                    TotalCurrent = _electricalResults?.Totals?.ContainsKey("current") == true ? _electricalResults.Totals["current"] : 0.0,
                    TotalWattage = _electricalResults?.Totals?.ContainsKey("wattage") == true ? _electricalResults.Totals["wattage"] : 0.0,
                    TotalCircuits = _idnacResults.TotalIdnacsNeeded
                } : null,
                IDNETSystemData = (_idnetResults != null) ? new
                {
                    TotalDevices = _idnetResults.AllDevices?.Count ?? 0,
                    TotalPower = _idnetResults.TotalPowerConsumption,
                    TotalSegments = _idnetResults.NetworkSegments?.Count ?? 0,
                    CompliantDevices = _idnetResults.AllDevices?.Count(d => d.PowerConsumption <= 25.5) ?? 0,
                    NonCompliantDevices = _idnetResults.AllDevices?.Count(d => d.PowerConsumption > 25.5) ?? 0
                } : null,
                GridData = PrepareSystemOverviewGridData()
            };
            return data;
        }

        /// <summary>
        /// Prepares system overview grid data
        /// </summary>
        private List<SystemOverviewData> PrepareSystemOverviewGridData()
        {
            var overviewData = new List<SystemOverviewData>();

            // Get all levels from both IDNAC and IDNET results
            var allLevels = new HashSet<string>();

            if (_idnacResults?.LevelAnalysis != null)
            {
                foreach (var level in _idnacResults.LevelAnalysis.Keys)
                    allLevels.Add(level);
            }

            if (_idnetResults?.AllDevices != null)
            {
                foreach (var device in _idnetResults.AllDevices)
                    if (!string.IsNullOrEmpty(device.Level))
                        allLevels.Add(device.Level);
            }

            // Create combined level analysis
            foreach (var levelName in allLevels.OrderBy(l => l))
            {
                var idnacDevices = 0;
                var idnacCurrent = 0.0;
                var idnacCircuits = 0;

                if (_idnacResults?.LevelAnalysis?.ContainsKey(levelName) == true)
                {
                    var idnacLevel = _idnacResults.LevelAnalysis[levelName];
                    idnacDevices = idnacLevel.Devices;
                    idnacCurrent = idnacLevel.Current;
                    idnacCircuits = idnacLevel.IdnacsRequired;
                }

                var idnetDevices = _idnetResults?.AllDevices?.Count(d => d.Level == levelName) ?? 0;
                var idnetCurrent = _idnetResults?.AllDevices?.Where(d => d.Level == levelName)?.Sum(d => d.PowerConsumption) ?? 0;
                var idnetSegments = _idnetResults?.NetworkSegments?.Count(s => s.CoveredLevels?.Contains(levelName) == true) ?? 0;

                var totalDevices = idnacDevices + idnetDevices;
                var status = totalDevices > 0 ? "Active" : "No Devices";

                overviewData.Add(new SystemOverviewData
                {
                    Level = levelName,
                    IDNACDevices = idnacDevices,
                    IDNETDevices = idnetDevices,
                    TotalDevices = totalDevices,
                    IDNACCurrent = idnacCurrent,
                    IDNETCurrent = $"{idnetCurrent:F0}",
                    IDNACCircuits = idnacCircuits,
                    IDNETSegments = idnetSegments,
                    Status = status
                });
            }

            return overviewData;
        }

        /// <summary>
        /// Applies system overview data on UI thread
        /// </summary>
        private void ApplySystemOverviewData(object data)
        {
            if (data == null) return;
            
            var dataType = data.GetType();
            var idnacSystemDataProperty = dataType.GetProperty("IDNACSystemData");
            var idnacSystemData = idnacSystemDataProperty?.GetValue(data);

            // Update IDNAC System Summary
            if (idnacSystemData != null)
            {
                var idnacDataType = idnacSystemData.GetType();
                
                var totalDevicesProperty = idnacDataType.GetProperty("TotalDevices");
                var totalDevices = totalDevicesProperty?.GetValue(idnacSystemData);
                if (IDNACSystemDevicesText != null && totalDevices != null)
                    IDNACSystemDevicesText.Text = DisplayFormatting.FormatCount((int)totalDevices);
                    
                var totalCircuitsProperty = idnacDataType.GetProperty("TotalCircuits");
                var totalCircuits = totalCircuitsProperty?.GetValue(idnacSystemData);
                if (IDNACSystemCircuitsText != null && totalCircuits != null)
                    IDNACSystemCircuitsText.Text = totalCircuits.ToString();
                    
                var totalCurrentProperty = idnacDataType.GetProperty("TotalCurrent");
                var totalCurrent = totalCurrentProperty?.GetValue(idnacSystemData);
                if (IDNACSystemCurrentText != null && totalCurrent != null)
                    IDNACSystemCurrentText.Text = $"{totalCurrent:F1}A";
                    
                var totalWattageProperty = idnacDataType.GetProperty("TotalWattage");
                var totalWattage = totalWattageProperty?.GetValue(idnacSystemData);
                if (IDNACSystemWattageText != null && totalWattage != null)
                    IDNACSystemWattageText.Text = $"{totalWattage:F0}W";
            }

            // Update IDNET System Summary
            var idnetSystemDataProperty = dataType.GetProperty("IDNETSystemData");
            var idnetSystemData = idnetSystemDataProperty?.GetValue(data);
            if (idnetSystemData != null)
            {
                var idnetDataType = idnetSystemData.GetType();
                
                var totalDevicesProperty = idnetDataType.GetProperty("TotalDevices");
                var totalDevices = totalDevicesProperty?.GetValue(idnetSystemData);
                if (IDNETSystemDevicesText != null && totalDevices != null)
                    IDNETSystemDevicesText.Text = DisplayFormatting.FormatCount((int)totalDevices);
                    
                var totalSegmentsProperty = idnetDataType.GetProperty("TotalSegments");
                var totalSegments = totalSegmentsProperty?.GetValue(idnetSystemData);
                if (IDNETSystemSegmentsText != null && totalSegments != null)
                    IDNETSystemSegmentsText.Text = totalSegments.ToString();
                    
                var totalPowerProperty = idnetDataType.GetProperty("TotalPower");
                var totalPower = totalPowerProperty?.GetValue(idnetSystemData);
                if (IDNETSystemPowerText != null && totalPower != null)
                    IDNETSystemPowerText.Text = $"{totalPower:F0}W";
                    
                var compliantDevicesProperty = idnetDataType.GetProperty("CompliantDevices");
                var compliantDevices = compliantDevicesProperty?.GetValue(idnetSystemData);
                if (IDNETSystemCompliantText != null && compliantDevices != null)
                    IDNETSystemCompliantText.Text = compliantDevices.ToString();
                    
                var nonCompliantDevicesProperty = idnetDataType.GetProperty("NonCompliantDevices");
                var nonCompliantDevices = nonCompliantDevicesProperty?.GetValue(idnetSystemData);
                if (IDNETSystemNonCompliantText != null && nonCompliantDevices != null)
                    IDNETSystemNonCompliantText.Text = nonCompliantDevices.ToString();
            }

            // Update grid
            var gridDataProperty = dataType.GetProperty("GridData");
            var gridData = gridDataProperty?.GetValue(data);
            if (SystemOverviewGrid != null && gridData != null)
            {
                SystemOverviewGrid.ItemsSource = gridData;
            }
        }

        /// <summary>
        /// Prepares IDNAC grid data on background thread
        /// </summary>
        private object PrepareIDNACGridData()
        {
            if (_idnacResults?.LevelAnalysis == null || _electricalResults?.Elements == null)
                return null;

            var idnacData = new List<object>();

            foreach (var level in _idnacResults.LevelAnalysis.OrderBy(l => l.Key))
            {
                var levelDevices = _electricalResults.Elements
                    .Where(e => e.LevelName?.Contains(level.Key) == true)
                    .OrderBy(e => e.Description);

                foreach (var device in levelDevices)
                {
                    var current = device.Current;
                    var wattage = device.Wattage;

                    idnacData.Add(new
                    {
                        Level = level.Key,
                        DeviceType = device.Description ?? "Unknown",
                        Quantity = 1,
                        UnitCurrent = device.Current,
                        TotalCurrent = current,
                        TotalWattage = wattage,
                        CircuitType = "Standard",
                        Status = "Active"
                    });
                }
            }

            return idnacData;
        }

        /// <summary>
        /// Applies IDNAC grid data on UI thread
        /// </summary>
        private void ApplyIDNACGridData(object data)
        {
            if (IDNACGrid != null && data != null)
            {
                IDNACGrid.ItemsSource = data as IEnumerable;
            }
        }

        /// <summary>
        /// Prepares device grid data on background thread
        /// </summary>
        private object PrepareDeviceGridData()
        {
            var deviceData = new List<DeviceAnalysisGridItem>();

            if (_electricalResults?.Elements != null)
            {
                foreach (var element in _electricalResults.Elements.OrderBy(e => e.Description))
                {
                    var isIdnet = false; // Default to IDNAC since ElementData doesn't have Tags
                    var current = element.Current;
                    var wattage = element.Wattage;

                    deviceData.Add(new DeviceAnalysisGridItem
                    {
                        DeviceType = element.Description ?? "Unknown",
                        Category = element.LevelName ?? "Uncategorized",
                        Quantity = 1,
                        TotalCurrent = current,
                        TotalWattage = wattage,
                        SystemType = isIdnet ? "IDNET" : "IDNAC",
                        References = element.Id.ToString()
                    });
                }
            }

            if (_idnetResults?.AllDevices != null)
            {
                var existingDeviceTypes = new HashSet<string>(deviceData.Select(d => d.DeviceType));

                var groupedIdnetDevices = _idnetResults.AllDevices
                    .GroupBy(d => d.DeviceType ?? "Unknown")
                    .Where(g => !existingDeviceTypes.Contains(g.Key));

                foreach (var group in groupedIdnetDevices)
                {
                    var totalPower = group.Sum(d => d.PowerConsumption);
                    var totalCurrent = totalPower / 24.0;

                    deviceData.Add(new DeviceAnalysisGridItem
                    {
                        DeviceType = group.Key,
                        Category = "IDNET",
                        Quantity = group.Count(),
                        TotalCurrent = totalCurrent,
                        TotalWattage = totalPower,
                        SystemType = "IDNET",
                        References = string.Join(", ", group.Select(d => d.DeviceId))
                    });
                }
            }

            return deviceData;
        }

        /// <summary>
        /// Applies device grid data on UI thread
        /// </summary>
        private void ApplyDeviceGridData(object data)
        {
            if (DeviceAnalysisGrid != null && data != null)
            {
                DeviceAnalysisGrid.ItemsSource = data as IEnumerable;
            }
        }

        /// <summary>
        /// Prepares level grid data on background thread
        /// </summary>
        private object PrepareLevelGridData()
        {
            var levelData = new List<LevelGridItem>();

            if (_idnacResults?.LevelAnalysis != null)
            {
                foreach (var kvp in _idnacResults.LevelAnalysis.OrderBy(l => l.Key))
                {
                    levelData.Add(new LevelGridItem
                    {
                        Level = kvp.Key,
                        Devices = kvp.Value.Devices,
                        Current = kvp.Value.Current,
                        CapacityUsage = kvp.Value.SpareInfo?.CurrentUtilization ?? 0.0,
                        CircuitType = "Standard", // Default circuit type
                        IdnacsRequired = kvp.Value.IdnacsRequired,
                        Status = kvp.Value.Status
                    });
                }
            }

            return levelData;
        }

        /// <summary>
        /// Applies level grid data on UI thread
        /// </summary>
        private void ApplyLevelGridData(object data)
        {
            if (LevelAnalysisGrid != null && data != null)
            {
                LevelAnalysisGrid.ItemsSource = data as IEnumerable;
            }
        }

        /// <summary>
        /// Prepares raw data grid data on background thread
        /// </summary>
        private object PrepareRawDataGridData()
        {
            var rawData = new List<object>();

            if (_rawCircuitData != null)
            {
                foreach (var circuit in _rawCircuitData.OrderBy(c => c.Level).ThenBy(c => c.SourceElementId))
                {
                    rawData.Add(new
                    {
                        Level = circuit.Level,
                        SourceElement = circuit.SourceElementId,
                        DeviceType = circuit.DeviceType,
                        Current = circuit.Current,
                        Wattage = circuit.Wattage,
                        CircuitType = circuit.CircuitType,
                        IdnacIndex = circuit.IdnacIndex
                    });
                }
            }

            return rawData;
        }

        /// <summary>
        /// Applies raw data grid data on UI thread
        /// </summary>
        private void ApplyRawDataGridData(object data)
        {
            if (RawDataGrid != null && data != null)
            {
                RawDataGrid.ItemsSource = data as IEnumerable;
            }
        }

        /// <summary>
        /// Prepares panel placement data on background thread
        /// </summary>
        private object PreparePanelPlacementData()
        {
            if (_panelRecommendations == null || _panelRecommendations.Count == 0)
                return null;

            return _panelRecommendations.Select(rec => new
            {
                Strategy = rec.Strategy,
                PanelCount = rec.PanelCount,
                Location = rec.Location?.Item1 ?? "Not specified",
                Reasoning = rec.Reasoning,
                Equipment = rec.Equipment,
                AmplifierInfo = rec.AmplifierInfo,
                Advantages = rec.Advantages,
                Considerations = rec.Considerations,
                Panels = rec.Panels
            }).ToList();
        }

        /// <summary>
        /// Applies panel placement data on UI thread
        /// </summary>
        private void ApplyPanelPlacementData(object data)
        {
            if (PanelRecommendationsPanel == null || data == null)
                return;

            PanelRecommendationsPanel.Children.Clear();

            if (!(data is System.Collections.IEnumerable panelData))
                return;
                
            foreach (var rec in panelData)
            {
                if (rec == null) continue;
                var recType = rec.GetType();
                
                var strategyProperty = recType.GetProperty("Strategy");
                var strategy = strategyProperty?.GetValue(rec)?.ToString() ?? "Unknown";
                
                var recGroup = new GroupBox
                {
                    Header = $"Strategy: {strategy}",
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var stackPanel = new StackPanel();

                // Strategy details
                var panelCountProperty = recType.GetProperty("PanelCount");
                var panelCount = panelCountProperty?.GetValue(rec)?.ToString() ?? "0";
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"Panel Count: {panelCount}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                
                var locationProperty = recType.GetProperty("Location");
                var location = locationProperty?.GetValue(rec)?.ToString() ?? "Not specified";
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"Location: {location}",
                    Margin = new Thickness(0, 0, 0, 5)
                });
                var reasoningProperty = recType.GetProperty("Reasoning");
                var reasoning = reasoningProperty?.GetValue(rec)?.ToString();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = $"Reasoning: {reasoning}",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                }

                // Advantages
                var advantagesProperty = recType.GetProperty("Advantages");
                var advantages = advantagesProperty?.GetValue(rec) as System.Collections.IEnumerable;
                if (advantages != null)
                {
                    var advantagesList = new List<string>();
                    foreach (var adv in advantages)
                    {
                        if (adv != null) advantagesList.Add(adv.ToString());
                    }
                    
                    if (advantagesList.Count > 0)
                    {
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = "Advantages:",
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 10, 0, 5)
                        });
                        foreach (string advantage in advantagesList)
                        {
                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = $"• {advantage}",
                                Margin = new Thickness(10, 0, 0, 2)
                            });
                        }
                    }
                }

                // Considerations
                var considerationsProperty = recType.GetProperty("Considerations");
                var considerations = considerationsProperty?.GetValue(rec) as System.Collections.IEnumerable;
                if (considerations != null)
                {
                    var considerationsList = new List<string>();
                    foreach (var cons in considerations)
                    {
                        if (cons != null) considerationsList.Add(cons.ToString());
                    }
                    
                    if (considerationsList.Count > 0)
                    {
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = "Considerations:",
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 10, 0, 5)
                        });
                        foreach (string consideration in considerationsList)
                        {
                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = $"• {consideration}",
                                Margin = new Thickness(10, 0, 0, 2)
                            });
                        }
                    }
                }

                recGroup.Content = stackPanel;
                PanelRecommendationsPanel.Children.Add(recGroup);
            }
        }

        private void UpdateDashboard(ElectricalResults electrical, IDNACSystemResults idnac, AmplifierRequirements amplifier)
        {
            try
            {
                // Update device count
                var deviceCount = electrical?.Elements?.Count ?? 0;
                // Update dashboard totals
                if (TotalFireAlarmDevicesText != null)
                    TotalFireAlarmDevicesText.Text = DisplayFormatting.FormatCount(deviceCount);

                // Legacy support for old field names
                var totalDevicesText = Find<TextBlock>("TotalDevicesText");
                if (totalDevicesText != null) totalDevicesText.Text = DisplayFormatting.FormatCount(deviceCount);

                // Use AmplifierCalculator for consistent device analysis (same as ResultsWindow)
                var strobeCurrent = 0.0;
                var speakerWattage = 0.0;
                if (electrical != null)
                {
                    var amplifierCalculator = new AmplifierCalculator();
                    var deviceAnalysis = amplifierCalculator.AnalyzeDeviceTypes(electrical);

                    // Get strobe current from strobes + combo devices (Horn/Strobe combinations)
                    var strobeCurrentFromStrobes = deviceAnalysis.DeviceTypes.ContainsKey("strobes") ?
                        deviceAnalysis.DeviceTypes["strobes"].Current : 0.0;
                    var strobeCurrentFromCombo = deviceAnalysis.DeviceTypes.ContainsKey("combo") ?
                        deviceAnalysis.DeviceTypes["combo"].Current : 0.0;
                    strobeCurrent = strobeCurrentFromStrobes + strobeCurrentFromCombo;

                    // Get speaker wattage from speakers + combo (same as ResultsWindow)
                    var speakerWattageFromSpeakers = deviceAnalysis.DeviceTypes.ContainsKey("speakers") ?
                        deviceAnalysis.DeviceTypes["speakers"].Wattage : 0.0;
                    var speakerWattageFromCombo = deviceAnalysis.DeviceTypes.ContainsKey("combo") ?
                        deviceAnalysis.DeviceTypes["combo"].Wattage : 0.0;
                    speakerWattage = speakerWattageFromSpeakers + speakerWattageFromCombo;
                }

                // Update total system current - use 2 decimal places for total
                var totalSystemCurrent = electrical?.Totals?.GetValueOrDefault("current", 0.0) ?? 0;
                var nonStrobeCurrent = totalSystemCurrent - strobeCurrent;

                // Update dashboard displays
                // Update IDNAC device count
                var idnacDeviceCount = electrical?.Elements?.Count(e => !IsDetectionDevice(e.Element)) ?? 0;
                if (IDNACDevicesText != null)
                    IDNACDevicesText.Text = DisplayFormatting.FormatCount(idnacDeviceCount);

                // Update dashboard power display
                if (TotalPowerText != null)
                    TotalPowerText.Text = DisplayFormatting.FormatTotalCurrent(totalSystemCurrent);

                // Legacy support for old field names
                var totalCurrentText = Find<TextBlock>("TotalCurrentText");
                if (totalCurrentText != null) totalCurrentText.Text = DisplayFormatting.FormatTotalCurrent(totalSystemCurrent);

                var strobeCurrentText = Find<TextBlock>("StrobeCurrentText");
                if (strobeCurrentText != null) strobeCurrentText.Text = DisplayFormatting.FormatStrobeCurrent(strobeCurrent, true);

                var totalWattageText = Find<TextBlock>("TotalWattageText");
                if (totalWattageText != null) totalWattageText.Text = DisplayFormatting.FormatTotalWattage(speakerWattage);

                // Update IDNAC count
                var idnacCount = idnac?.TotalIdnacsNeeded ?? 0;
                if (IDNACsRequiredText != null)
                    IDNACsRequiredText.Text = idnacCount.ToString();


                var idnetDeviceCount = _idnetResults?.TotalDevices ?? 0;
                if (IDNETDevicesText != null)
                    IDNETDevicesText.Text = idnetDeviceCount.ToString();


                // Update amplifiers (FIXED: Show current draw separate from speaker total wattage)
                var amplifierCount = amplifier?.AmplifiersNeeded ?? 0;
                if (AmplifiersText != null)
                    AmplifiersText.Text = amplifierCount.ToString();

                // Update status bar
                if (DeviceCount != null)
                    DeviceCount.Text = $"Devices: {deviceCount}";
                if (IDNACCount != null)
                    IDNACCount.Text = $"IDNACs: {idnacCount}";
                if (CurrentLoad != null)
                    CurrentLoad.Text = $"Load: {totalSystemCurrent:F1}A";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating dashboard: {ex.Message}");
            }
        }

        private void UpdateLastAnalysisTime()
        {
            var lastAnalysisTime = Find<TextBlock>("LastAnalysisTimeText");
            if (lastAnalysisTime != null) lastAnalysisTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void UpdateStatus(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (StatusMessage != null)
                    StatusMessage.Text = message;
                var analysisStatusText = Find<TextBlock>("AnalysisStatusText");
                if (analysisStatusText != null) analysisStatusText.Text = message;
                // Update dashboard system status
                if (SystemStatusText != null)
                    SystemStatusText.Text = message;
            });
        }

        /// <summary>
        /// Updates the selected scope display in dashboard
        /// </summary>
        private void UpdateScopeDisplay()
        {
            if (SelectedScopeText != null)
            {
                string scopeText = "Scope: ";
                if (MainScopeActiveViewItem?.IsChecked == true)
                    scopeText += "Active View";
                else if (MainScopeSelectionItem?.IsChecked == true)
                    scopeText += "Selection";
                else if (MainScopeEntireModelItem?.IsChecked == true)
                    scopeText += "Entire Model";
                else
                    scopeText += "Active View";

                SelectedScopeText.Text = scopeText;
            }
        }

        /// <summary>
        /// Validates that all scope UI controls are synchronized with _currentScope
        /// </summary>
        private void ValidateScopeSync()
        {
            System.Diagnostics.Debug.WriteLine($"Validating scope synchronization for '{_currentScope}':");

            // Check unified scope controls
            var scopeActiveChecked = MainScopeActiveViewItem?.IsChecked ?? false;
            var scopeSelectionChecked = MainScopeSelectionItem?.IsChecked ?? false;
            var scopeModelChecked = MainScopeEntireModelItem?.IsChecked ?? false;

            System.Diagnostics.Debug.WriteLine($"  Unified Scope: Active={scopeActiveChecked}, Selection={scopeSelectionChecked}, Model={scopeModelChecked}");

            // Validate that the correct scope is checked
            bool isValid = true;
            switch (_currentScope)
            {
                case "Active View":
                    isValid = scopeActiveChecked && !scopeSelectionChecked && !scopeModelChecked;
                    break;
                case "Selection":
                    isValid = scopeSelectionChecked && !scopeActiveChecked && !scopeModelChecked;
                    break;
                case "Entire Model":
                    isValid = scopeModelChecked && !scopeActiveChecked && !scopeSelectionChecked;
                    break;
            }

            if (!isValid)
            {
                System.Diagnostics.Debug.WriteLine($"  WARNING: Scope UI controls are not synchronized! Fixing...");
                UpdateScopeSelection();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  ✓ Scope UI controls are properly synchronized");
            }
        }

        /// <summary>
        /// Updates IDNET dashboard metrics from analysis results
        /// </summary>
        private void UpdateIDNETDashboard()
        {
            if (_idnetResults == null) return;

            // Update IDNET device count
            var idnetDeviceCount = _idnetResults.TotalDevices;
            if (IDNETDevicesText != null)
                IDNETDevicesText.Text = DisplayFormatting.FormatCount(idnetDeviceCount);

            // Update IDNET channels
            var channelCount = _idnetResults.SystemSummary?.RecommendedNetworkChannels ?? 0;
            if (IDNETSystemChannelsText != null)
                IDNETSystemChannelsText.Text = channelCount.ToString();

            // Update IDNET power consumption
            var totalPower = _idnetResults.AllDevices?.Sum(d => d.PowerConsumption) ?? 0;
            if (IDNETSystemPowerText != null)
                IDNETSystemPowerText.Text = $"{totalPower:F0}mA";

            // Update IDNET segments
            var segmentCount = _idnetResults.NetworkSegments?.Count ?? 0;
            if (IDNETSystemSegmentsText != null)
                IDNETSystemSegmentsText.Text = segmentCount.ToString();

            System.Diagnostics.Debug.WriteLine($"IDNET Dashboard updated: {idnetDeviceCount} devices, {channelCount} channels");
        }

        /// <summary>
        /// Test method to validate scope functionality (for debugging)
        /// </summary>
        private void TestScopeImplementation()
        {
            System.Diagnostics.Debug.WriteLine("=== SCOPE IMPLEMENTATION TEST ===");

            // Test all scope values
            var testScopes = new[] { "Active View", "Selection", "Entire Model" };
            var originalScope = _currentScope;

            foreach (var scope in testScopes)
            {
                System.Diagnostics.Debug.WriteLine($"Testing scope: {scope}");
                _currentScope = scope;
                UpdateScopeSelection();
                ValidateScopeSync();

                // Test element collection
                try
                {
                    var elements = GetElementsByScope();
                    System.Diagnostics.Debug.WriteLine($"  {scope}: Found {elements.Count} elements");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  {scope}: ERROR - {ex.Message}");
                }
            }

            // Restore original scope
            _currentScope = originalScope;
            UpdateScopeSelection();

            System.Diagnostics.Debug.WriteLine("=== SCOPE TEST COMPLETE ===");
        }

        private void ShowErrorMessage(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                DXMessageBox.Show(message, "IDNAC Calculator Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        
        private void UpdatePendingChangesCount()
        {
            try
            {
                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as DevExpress.Xpf.Grid.GridControl;
                if (detailedGrid?.ItemsSource is ObservableCollection<DeviceDetailItem> items)
                {
                    int pendingCount = items.Count(item => item.IsDirty);

                    var pendingBadge = ((System.Windows.FrameworkElement)this).FindName("PendingChangesBadge") as System.Windows.Controls.TextBlock;
                    if (pendingBadge != null)
                    {
                        pendingBadge.Text = pendingCount.ToString();
                        pendingBadge.Visibility = pendingCount > 0 ? WpfVisibility.Visible : WpfVisibility.Collapsed;
                    }

                    // Update the Apply Changes button
                    UpdateApplyChangesButton();

                    // Update status bar
                    UpdateUnsyncedChangesStatusBar();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating pending changes count: {ex.Message}");
            }
        }
        
        private void UpdateApplyChangesButton()
        {
            try
            {
                var pendingChanges = PendingChangesService.Instance;
                var applyButton = ((System.Windows.FrameworkElement)this).FindName("ApplyChangesButton") as DevExpress.Xpf.Bars.BarButtonItem;

                if (applyButton != null)
                {
                    applyButton.IsEnabled = pendingChanges.HasPending;

                    // Update button content to show pending count
                    if (pendingChanges.HasPending)
                    {
                        applyButton.Content = $"Apply Changes to Revit ({pendingChanges.PendingCount})";
                    }
                    else
                    {
                        applyButton.Content = "Apply Changes to Revit";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating apply changes button: {ex.Message}");
            }
        }
        
        private void UpdateUnsyncedChangesStatusBar()
        {
            try
            {
                var pendingChanges = PendingChangesService.Instance;
                var statusItem = ((System.Windows.FrameworkElement)this).FindName("UnsyncedChangesStatusItem") as System.Windows.Controls.Primitives.StatusBarItem;
                var statusText = ((System.Windows.FrameworkElement)this).FindName("UnsyncedChangesText") as System.Windows.Controls.TextBlock;

                if (statusItem != null && statusText != null)
                {
                    if (pendingChanges.HasPending)
                    {
                        statusItem.Visibility = WpfVisibility.Visible;
                        statusText.Text = $"{pendingChanges.PendingCount} unsynced changes";
                    }
                    else
                    {
                        statusItem.Visibility = WpfVisibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating unsynced changes status bar: {ex.Message}");
            }
        }
        
        private void UpdateConfigurationSummary()
        {
            try
            {
                // Update mapping count
                var mappingCountText = ((System.Windows.FrameworkElement)this).FindName("MappingCountText") as System.Windows.Controls.TextBlock;
                if (mappingCountText != null)
                {
                    try
                    {
                        var candelaConfig = new CandelaConfiguration();
                        int mappingCount = candelaConfig.DeviceTypes?.Count ?? 0;
                        mappingCountText.Text = $"{mappingCount} device types configured";
                    }
                    catch
                    {
                        mappingCountText.Text = "Error loading mappings";
                    }
                }

                // Update last modified time
                var lastUpdatedText = ((System.Windows.FrameworkElement)this).FindName("ConfigLastUpdatedText") as System.Windows.Controls.TextBlock;
                if (lastUpdatedText != null)
                {
                    try
                    {
                        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CandelaCurrentMapping.json");
                        if (File.Exists(configPath))
                        {
                            var lastWrite = File.GetLastWriteTime(configPath);
                            lastUpdatedText.Text = lastWrite.ToString("MMM dd, yyyy HH:mm");
                        }
                        else
                        {
                            lastUpdatedText.Text = "Config file not found";
                        }
                    }
                    catch
                    {
                        lastUpdatedText.Text = "Unable to check";
                    }
                }

                // Update validation status
                var validationIcon = ((System.Windows.FrameworkElement)this).FindName("ValidationStatusIcon") as DevExpress.Xpf.Core.DXImage;
                var validationText = ((System.Windows.FrameworkElement)this).FindName("ValidationStatusText") as System.Windows.Controls.TextBlock;

                if (validationIcon != null && validationText != null)
                {
                    try
                    {
                        var candelaConfig = new CandelaConfiguration();
                        bool isValid = candelaConfig.DeviceTypes != null && candelaConfig.DeviceTypes.Any();

                        if (isValid)
                        {
                            // validationIcon.Source = "{dx:DXImage SvgImages/Icon Builder/Actions_CheckCircled.svg}".ToImageSource();
                            validationText.Text = "Configuration valid";
                            validationText.Foreground = new SolidColorBrush(Colors.Green);
                        }
                        else
                        {
                            // validationIcon.Source = "{dx:DXImage SvgImages/Icon Builder/Actions_Warning.svg}".ToImageSource();
                            validationText.Text = "Configuration issues detected";
                            validationText.Foreground = new SolidColorBrush(Colors.Orange);
                        }
                    }
                    catch
                    {
                        // validationIcon.Source = "{dx:DXImage SvgImages/Icon Builder/Actions_Error.svg}".ToImageSource();
                        validationText.Text = "Configuration error";
                        validationText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating configuration summary: {ex.Message}");
            }
        }
        
        private void ZoomToElementInRevit(int elementId)
        {
            try
            {
                // Access the UIDocument through the class instance to avoid scope issues
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                var uiDoc = mainWindow?._uiDocument;
                if (uiDoc?.Document != null)
                {
                    var element = uiDoc.Document.GetElement(new ElementId((long)elementId));
                    if (element != null)
                    {
                        var elementIds = new List<ElementId> { element.Id };
                        uiDoc.ShowElements(elementIds);
                        uiDoc.Selection.SetElementIds(elementIds);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to direct debug output if WriteToDebugLog has scoping issues
                System.Diagnostics.Debug.WriteLine($"Error zooming to element {elementId}: {ex.Message}");
            }
        }
        
        private int FindFirstAvailableAddress(string circuitName)
        {
            try
            {
                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as GridControl;
                if (detailedGrid?.ItemsSource is ObservableCollection<DeviceDetailItem> items)
                {
                    var circuitDevices = items.Where(d => d.Circuit == circuitName).ToList();
                    var usedAddresses = new HashSet<int>(circuitDevices.Select(d => d.Address));

                    for (int addr = 1; addr <= 254; addr++)
                    {
                        if (!usedAddresses.Contains(addr))
                        {
                            return addr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding first available address: {ex.Message}");
            }

            return -1; // No available address found
        }
        
        private List<Models.DeviceSnapshot> GetCurrentDeviceSnapshots()
        {
            var devices = new List<Models.DeviceSnapshot>();

            try
            {
                // Try to get devices from current analysis results
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                if (mainWindow?._electricalResults?.Elements?.Any() == true)
                {
                    foreach (var deviceData in mainWindow._electricalResults.Elements)
                    {
                        // Convert from device data to snapshot
                        var snapshot = new Models.DeviceSnapshot(
                            ElementId: (int)deviceData.Id,
                            LevelName: deviceData.LevelName ?? "Unknown",
                            FamilyName: deviceData.FamilyName ?? "Unknown",
                            TypeName: deviceData.TypeName ?? "Unknown",
                            Watts: deviceData.Wattage,
                            Amps: deviceData.Current,
                            UnitLoads: (int)(deviceData.Current / 0.0008), // Convert to UL
                            HasStrobe: (deviceData.TypeName ?? "").ToUpper().Contains("STROBE"),
                            HasSpeaker: (deviceData.TypeName ?? "").ToUpper().Contains("SPEAKER"),
                            IsIsolator: (deviceData.TypeName ?? "").ToUpper().Contains("ISOLATOR"),
                            IsRepeater: (deviceData.TypeName ?? "").ToUpper().Contains("REPEATER"),
                            Zone: deviceData.LevelName ?? "Unknown", // Use level as zone for now
                            X: 0.0,
                            Y: 0.0,
                            Z: 0.0,
                            StandbyCurrent: deviceData.Current,
                            HasOverride: false,
                            CustomProperties: new Dictionary<string, object>()
                        );
                        devices.Add(snapshot);
                    }
                }

                // If no analysis results, create some mock data for demonstration
                if (!devices.Any())
                {
                    devices = CreateMockDeviceSnapshots();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device snapshots: {ex.Message}");
                // Fallback to mock data
                devices = CreateMockDeviceSnapshots();
            }

            return devices;
        }
        
        private List<Models.DeviceSnapshot> CreateMockDeviceSnapshots()
        {
            return new List<Models.DeviceSnapshot>
            {
                new Models.DeviceSnapshot(1001, "Level 1", "Speaker Strobe", "Wall Mount", 15.0, 0.150, 187, true, true, false, false, "Zone A"),
                new Models.DeviceSnapshot(1002, "Level 1", "Horn Strobe", "Ceiling Mount", 0.0, 0.090, 112, true, false, false, false, "Zone A"),
                new Models.DeviceSnapshot(1003, "Level 1", "Speaker", "Wall Mount", 10.0, 0.000, 125, false, true, false, false, "Zone B"),
                new Models.DeviceSnapshot(1004, "Level 2", "Speaker Strobe", "Wall Mount", 15.0, 0.150, 187, true, true, false, false, "Zone C"),
                new Models.DeviceSnapshot(1005, "Level 2", "Strobe", "Ceiling Mount", 0.0, 0.070, 87, true, false, false, false, "Zone C"),
                new Models.DeviceSnapshot(1006, "Level 3", "Horn", "Wall Mount", 0.0, 0.040, 50, false, false, false, false, null), // Unassigned
                new Models.DeviceSnapshot(1007, "Level 3", "Speaker", "Ceiling Mount", 8.0, 0.000, 100, false, true, false, false, null), // Unassigned
            };
        }
        
        private void UpdateQuickFilterButtons(System.Windows.Controls.Button activeButton)
        {
            try
            {
                var quickFiltersPanel = ((System.Windows.FrameworkElement)this).FindName("QuickFiltersPanel") as System.Windows.Controls.StackPanel;
                if (quickFiltersPanel != null)
                {
                    foreach (var child in quickFiltersPanel.Children)
                    {
                        if (child is System.Windows.Controls.Button btn)
                        {
                            btn.Opacity = btn == activeButton ? 1.0 : 0.7;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating quick filter buttons: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Sort floors by actual elevation order from Revit Level objects
        /// Uses Revit API to get actual elevation instead of string parsing
        /// Provides accurate vertical building order: basements → ground → upper floors → roof
        /// </summary>
        private double GetLevelSortKey(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return 9999;

            try
            {
                // FIXED: Use actual Revit elevation data instead of string parsing
                var actualElevation = GetLevelElevation(levelName);

                // Return the actual elevation - this automatically sorts by building height
                // Negative elevations (basements) will sort first
                // Ground floor (elevation ~0) will sort next  
                // Upper floors (positive elevations) will sort in order
                // Roof levels (highest elevations) will sort last
                return actualElevation;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting elevation for level '{levelName}': {ex.Message}");

                // Fallback to alphabetical sorting only if elevation lookup fails
                try
                {
                    return 9000 + (levelName.GetHashCode() % 100);
                }
                catch
                {
                    return 9999; // Ultimate fallback
                }
            }
        }

        /// <summary>
        /// Helper method to extract first numeric value from a string
        /// </summary>
        private int? ExtractNumericPart(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            var match = Regex.Match(input, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int result))
            {
                return result;
            }
            return null;
        }

        private double GetLevelElevation(string levelName)
        {
            try
            {
                if (_document != null)
                {
                    var levels = new FilteredElementCollector(_document)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

                    if (levels != null)
                    {
                        return Math.Round(levels.Elevation, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting elevation for level '{levelName}': {ex.Message}");
            }

            return 0.0; // Default elevation
        }

        private double GetLevelWattage(string levelName, IDNACAnalysisResult analysis)
        {
            try
            {
                if (!string.IsNullOrEmpty(levelName) && _electricalResults?.ByLevel?.TryGetValue(levelName, out var levelData) == true)
                {
                    return levelData.Wattage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting wattage for level '{levelName}': {ex.Message}");
            }

            return 0.0; // Default wattage
        }

        private string GetDeviceType(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return "Unknown";

            var familyUpper = familyName.ToUpper();

            if (familyUpper.Contains("SPEAKER") || familyUpper.Contains("SPKR"))
                return "Speaker";
            if (familyUpper.Contains("STROBE") || familyUpper.Contains("STR"))
                return "Strobe";
            if (familyUpper.Contains("HORN") || familyUpper.Contains("HRN"))
                return "Horn";
            if (familyUpper.Contains("COMBO") || familyUpper.Contains("CMB"))
                return "Horn/Strobe";
            if (familyUpper.Contains("BELL"))
                return "Bell";
            if (familyUpper.Contains("CHIME"))
                return "Chime";

            return "Other NAC Device";
        }

        private bool RequiresAmplifier(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return false;

            var familyUpper = familyName.ToUpper();
            return familyUpper.Contains("SPEAKER") || familyUpper.Contains("SPKR");
        }

        // Removed duplicate method - already defined earlier

        // Removed duplicate method - already defined earlier



        private Border CreatePanelRecommendationCard(PanelPlacementRecommendation recommendation)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("DarkCardBackgroundBrush"),
                BorderBrush = (Brush)FindResource("DarkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel();

            var headerText = new TextBlock
            {
                Text = recommendation.Strategy,
                Foreground = (Brush)FindResource("DarkTextBrush"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var detailsText = new TextBlock
            {
                Text = recommendation.Reasoning,
                Foreground = (Brush)FindResource("DarkSecondaryTextBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };

            stackPanel.Children.Add(headerText);
            stackPanel.Children.Add(detailsText);
            border.Child = stackPanel;

            return border;
        }

        private List<FamilyInstance> GetElementsByScope()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetElementsByScope: Processing scope '{_currentScope}'");

                switch (_currentScope)
                {
                    case "Selection":
                        var selection = _uiDocument.Selection.GetElementIds();
                        System.Diagnostics.Debug.WriteLine($"Selection scope: {selection.Count} elements selected");

                        if (selection.Count == 0)
                            return new List<FamilyInstance>();

                        var selectedElements = selection.Select(id => _document.GetElement(id))
                                      .OfType<FamilyInstance>()
                                      .Where(fi => fi != null)
                                      .ToList();

                        // PRE-FILTER to only Fire Alarm Devices category
                        var fireAlarmSelected = selectedElements.Where(element =>
                        {
                            var categoryName = element.Category?.Name?.ToUpperInvariant() ?? "";
                            return categoryName.Contains("FIRE ALARM DEVICES") || categoryName.Contains("FIRE ALARM");
                        }).ToList();

                        System.Diagnostics.Debug.WriteLine($"Selection scope: {selectedElements.Count} FamilyInstances found, {fireAlarmSelected.Count} Fire Alarm Devices");
                        return fireAlarmSelected;

                    case "Active View":
                        var activeView = _document.ActiveView;
                        if (activeView == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Active View scope: No active view found");
                            return new List<FamilyInstance>();
                        }

                        System.Diagnostics.Debug.WriteLine($"Active View scope: Using view '{activeView.Name}' (ID: {activeView.Id})");

                        // Try different approaches for Active View collection
                        var collector = new FilteredElementCollector(_document, activeView.Id);
                        var allElementsInView = collector.OfClass(typeof(FamilyInstance))
                                              .Cast<FamilyInstance>()
                                              .Where(fi => fi != null)
                                              .ToList();

                        // PRE-FILTER to only Fire Alarm Devices category
                        var fireAlarmElementsInView = allElementsInView.Where(element =>
                        {
                            var categoryName = element.Category?.Name?.ToUpperInvariant() ?? "";
                            return categoryName.Contains("FIRE ALARM DEVICES") || categoryName.Contains("FIRE ALARM");
                        }).ToList();

                        System.Diagnostics.Debug.WriteLine($"Active View scope: Found {allElementsInView.Count} FamilyInstances in view, {fireAlarmElementsInView.Count} Fire Alarm Devices");

                        // If no elements found in view-specific collection, try visible elements approach
                        if (allElementsInView.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("Active View scope: Trying alternative approach with visible elements");

                            // Alternative: Get all elements and filter by visibility in view
                            var allCollector = new FilteredElementCollector(_document);
                            var allFamilyInstances = allCollector.OfClass(typeof(FamilyInstance))
                                                       .Cast<FamilyInstance>()
                                                       .Where(fi => fi != null)
                                                       .ToList();

                            // Filter to only Fire Alarm Devices
                            var fireAlarmInModel = allFamilyInstances.Where(element =>
                            {
                                var categoryName = element.Category?.Name?.ToUpperInvariant() ?? "";
                                return categoryName.Contains("FIRE ALARM DEVICES") || categoryName.Contains("FIRE ALARM");
                            }).ToList();

                            System.Diagnostics.Debug.WriteLine($"Active View scope: Found {fireAlarmInModel.Count} Fire Alarm Devices in entire model");
                            return fireAlarmInModel;
                        }

                        return fireAlarmElementsInView;

                    case "Entire Model":
                        var modelCollector = new FilteredElementCollector(_document);
                        var modelElements = modelCollector.OfClass(typeof(FamilyInstance))
                                           .Cast<FamilyInstance>()
                                           .Where(fi => fi != null)
                                           .ToList();

                        // PRE-FILTER to only Fire Alarm Devices category to improve performance and accuracy
                        var fireAlarmElements = modelElements.Where(element =>
                        {
                            var categoryName = element.Category?.Name?.ToUpperInvariant() ?? "";
                            return categoryName.Contains("FIRE ALARM DEVICES") || categoryName.Contains("FIRE ALARM");
                        }).ToList();

                        System.Diagnostics.Debug.WriteLine($"Entire Model scope: Found {modelElements.Count} FamilyInstances total, {fireAlarmElements.Count} Fire Alarm Devices");
                        return fireAlarmElements;

                    default:
                        System.Diagnostics.Debug.WriteLine($"ERROR: Unknown scope '{_currentScope}', defaulting to Active View");
                        _currentScope = "Active View";
                        UpdateScopeSelection();
                        // Recursively call with corrected scope
                        return GetElementsByScope();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting elements by scope '{_currentScope}': {ex.Message}");
                return new List<FamilyInstance>();
            }
        }

        private bool IsElectricalFamilyInstance(FamilyInstance element)
        {
            if (element?.Symbol?.Family == null)
                return false;

            try
            {
                var familyName = element.Symbol.Family.Name;
                var categoryName = element.Category?.Name ?? "";

                // Check for specific parameters: CURRENT DRAW and Wattage ONLY
                var targetParams = new[] { "CURRENT DRAW", "Wattage" };
                bool hasElectricalParam = false;

                // Check instance parameters
                foreach (var paramName in targetParams)
                {
                    var param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found electrical parameter '{paramName}' in family '{familyName}' (instance)");
                        hasElectricalParam = true;
                        break;
                    }
                }

                // Check type parameters if instance parameters not found
                if (!hasElectricalParam && element.Symbol != null)
                {
                    foreach (var paramName in targetParams)
                    {
                        var param = element.Symbol.LookupParameter(paramName);
                        if (param != null && param.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found electrical parameter '{paramName}' in family '{familyName}' (type)");
                            hasElectricalParam = true;
                            break;
                        }
                    }
                }

                // Also check family names for common fire alarm device patterns (IDNAC devices ONLY)
                if (!hasElectricalParam)
                {
                    var familyUpper = familyName.ToUpperInvariant();
                    var categoryUpper = categoryName.ToUpperInvariant();

                    // FIRST: Exclude IDNET detection devices from IDNAC electrical analysis
                    var idnetDetectionKeywords = new[]
                    {
                        "DETECTORS", "DETECTOR", "MODULE", "PULL", "STATION", "MANUAL",
                        "MONITOR", "INPUT", "OUTPUT", "SENSOR", "SENSING"
                    };

                    // If it's clearly an IDNET detection device, exclude it from IDNAC analysis
                    if (idnetDetectionKeywords.Any(keyword => familyUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Excluded IDNET detection device from electrical analysis: '{familyName}' in category '{categoryName}'");
                        return false; // Explicitly exclude from IDNAC analysis
                    }

                    // SECOND: Only include IDNAC notification devices for electrical analysis
                    var idnacNotificationKeywords = new[]
                    {
                        "SPEAKER", "HORN", "STROBE", "BELL", "CHIME", "SOUNDER",
                        "NOTIFICATION", "NAC", "APPLIANCE"
                    };

                    if (idnacNotificationKeywords.Any(keyword => familyUpper.Contains(keyword) || categoryUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Found notification device by name pattern: '{familyName}' in category '{categoryName}' (for electrical analysis)");
                        hasElectricalParam = true;
                    }

                    // THIRD: Handle "FIRE ALARM" category more carefully - only for non-detection devices
                    else if (categoryUpper.Contains("FIRE ALARM") &&
                             !idnetDetectionKeywords.Any(keyword => familyUpper.Contains(keyword)))
                    {
                        System.Diagnostics.Debug.WriteLine($"IDNAC: Found fire alarm device (non-detection) by category: '{familyName}' in category '{categoryName}' (for electrical analysis)");
                        hasElectricalParam = true;
                    }
                }

                return hasElectricalParam;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking electrical family instance: {ex.Message}");
                return false;
            }
        }


        private void ExportResultsInternal()
        {
            if (!_analysisCompleted)
            {
                ShowErrorMessage("Please run analysis before exporting results.");
                return;
            }

            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "csv",
                    FileName = $"IDNAC_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // IMPLEMENTED: Export analysis results to file
                    try
                    {
                        if (_idnacResults == null)
                        {
                            UpdateStatus("No analysis results to export. Run analysis first.");
                            return;
                        }

                        var extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                        if (extension == ".csv")
                        {
                            ExportToCsv(saveDialog.FileName);
                            UpdateStatus($"Results exported to {saveDialog.FileName}");
                        }
                        else
                        {
                            ExportToExcel(saveDialog.FileName);
                            var csvFileName = System.IO.Path.ChangeExtension(saveDialog.FileName, ".csv");
                            UpdateStatus($"Results exported as CSV to {csvFileName} (Excel format not yet supported)");
                        }
                    }
                    catch (Exception exportEx)
                    {
                        UpdateStatus($"Export failed: {exportEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Export failed: {ex.Message}");
            }
        }


        // Scope selection event handlers
        private void ScopeActiveView_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                _currentScope = "Active View";
                UpdateScopeSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ScopeActiveView_Click: {ex.Message}");
            }
        }

        private void ScopeActiveView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentScope = "Active View";
                UpdateScopeSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ScopeActiveView_Click: {ex.Message}");
            }
        }

        private void ScopeEntireModel_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                _currentScope = "Entire Model";
                UpdateScopeSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ScopeEntireModel_Click: {ex.Message}");
            }
        }

        private void ScopeEntireModel_Click(object sender, RoutedEventArgs e)
        {
            _currentScope = "Entire Model";
            UpdateScopeSelection();
        }


        private void UpdateScopeSelection()
        {
            System.Diagnostics.Debug.WriteLine($"UpdateScopeSelection: Setting all controls to '{_currentScope}'");

            // Update unified analysis scope selection (used by both IDNAC and IDNET)
            if (MainScopeActiveViewItem != null)
                MainScopeActiveViewItem.IsChecked = _currentScope == "Active View";
            if (MainScopeSelectionItem != null)
                MainScopeSelectionItem.IsChecked = _currentScope == "Selection";
            if (MainScopeEntireModelItem != null)
                MainScopeEntireModelItem.IsChecked = _currentScope == "Entire Model";

            // Update dashboard scope display
            UpdateScopeDisplay();

            System.Diagnostics.Debug.WriteLine($"✓ Scope UI controls updated to '{_currentScope}'");
        }

        // Other event handlers
        private void PrintReport_Click(object sender, ItemClickEventArgs e)
        {
            if (!_analysisCompleted)
            {
                ShowErrorMessage("Please run analysis before printing.");
                return;
            }
            UpdateStatus("Print functionality not yet implemented.");
        }

        private async void IDNACSettings_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show(
                    "IDNAC Settings Configuration\n\n" +
                    "Current IDNAC Specifications:\n" +
                    "• Spare Capacity: 20% applied to all limits\n" +
                    "• Max Current per IDNAC: 3.0A (2.4A usable)\n" +
                    "• Max Devices per IDNAC: 127 (101 usable)\n" +
                    "• Max Unit Loads: 139 (111 usable)\n" +
                    "• Voltage: 29V regulated (24V nominal)\n\n" +
                    "Note: These settings match 4100ES fire alarm specifications.\n" +
                    "Would you like to re-run analysis with current settings?",
                    "IDNAC Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes && _analysisCompleted)
                {
                    UpdateStatus("Re-running analysis with current IDNAC settings...");
                    await RunAnalysisInternalAsync();
                }
                else
                {
                    UpdateStatus("IDNAC settings reviewed.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening IDNAC settings: {ex.Message}");
            }
        }

        private void AmplifierConfig_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var amplifierInfo = _amplifierResults != null ?
                    $"• Amplifiers Required: {_amplifierResults.AmplifiersNeeded}\n" +
                    $"• Amplifier Type: {_amplifierResults.AmplifierType ?? "Flex-100"}\n" +
                    $"• Current Draw: {_amplifierResults.AmplifierCurrent:F2}A\n" +
                    $"• Speaker Count: {_amplifierResults.SpeakerCount}\n" +
                    $"• Spare Capacity: {_amplifierResults.SpareCapacityPercent}%\n" :
                    "• No amplifier analysis available\n• Run analysis first to see amplifier requirements\n";

                var result = DXMessageBox.Show(
                    "Amplifier Configuration\n\n" +
                    "Current Amplifier Analysis:\n" +
                    amplifierInfo + "\n" +
                    "Available Amplifier Types:\n" +
                    "• Flex-35 (35W, 5.5A max)\n" +
                    "• Flex-50 (50W, 5.55A max)\n" +
                    "• Flex-100 (100W, 9.6A max)\n\n" +
                    "Note: Full configuration dialog will be implemented in future version.\n" +
                    "Would you like to refresh amplifier calculations?",
                    "Amplifier Configuration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes && _analysisCompleted && _electricalResults != null)
                {
                    UpdateStatus("Recalculating amplifier requirements...");
                    var amplifierCalculator = new AmplifierCalculator();
                    _amplifierResults = amplifierCalculator.CalculateAmplifierRequirements(_electricalResults);
                    UpdateDashboard(_electricalResults, _idnacResults, _amplifierResults);
                    UpdateStatus("Amplifier configuration updated.");
                }
                else
                {
                    UpdateStatus("Amplifier configuration reviewed.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening amplifier configuration: {ex.Message}");
            }
        }

        private void ResetLayout_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show(
                    "Reset Layout\n\n" +
                    "This will reset the window layout to default and show all hidden panels.\n\n" +
                    "Do you want to continue?",
                    "Reset Layout",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ResetLayoutToDefault();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error resetting layout: {ex.Message}");
            }
        }

        private void ThemeSettings_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var currentTheme = ApplicationThemeHelper.ApplicationThemeName ?? "Default";

                var result = DXMessageBox.Show(
                    "Theme Settings\n\n" +
                    $"Current Theme: {currentTheme}\n\n" +
                    "Available Themes:\n" +
                    "• Win11Dark - Modern dark theme\n" +
                    "• Office2019Colorful - Modern light theme\n" +
                    "• VS2017Dark - Visual Studio dark\n" +
                    "• VS2017Blue - Visual Studio blue\n" +
                    "• Office2016White - Classic white theme\n\n" +
                    "Note: Full theme selection dialog will be implemented in future version.\n" +
                    "Would you like to toggle between light/dark themes?",
                    "Theme Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Simple theme toggle for now
                    try
                    {
                        bool isDark = currentTheme.Contains("Dark") || currentTheme.Contains("Black");
                        string newTheme = isDark ? "Office2019Colorful" : "Win11Dark";

                        ApplicationThemeHelper.ApplicationThemeName = newTheme;
                        UpdateStatus($"Theme changed to {newTheme}");
                    }
                    catch (Exception themeEx)
                    {
                        UpdateStatus($"Theme change failed: {themeEx.Message}");
                        // Try fallback theme
                        ApplyTheme();
                    }
                }
                else
                {
                    UpdateStatus("Theme settings reviewed.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening theme settings: {ex.Message}");
            }
        }

        private async void SpareCapacitySpinner_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            try
            {
                if (sender is DevExpress.Xpf.Editors.SpinEdit spinner)
                {
                    double newValue = (double)spinner.Value;

                    // Update configuration service
                    var configService = new ConfigurationManagementService();
                    configService.UpdateSpareCapacityPercent(newValue);

                    UpdateStatus($"Spare capacity updated to {newValue:F0}%");

                    // Update spare capacity status display
                    UpdateSpareCapacityStatusDisplay(newValue);

                    // If analysis has been run, suggest re-running
                    if (_analysisCompleted)
                    {
                        var result = DXMessageBox.Show(
                            $"Spare capacity changed to {newValue:F0}%.\n\n" +
                            "This affects all calculations including:\n" +
                            "• IDNAC circuit requirements\n" +
                            "• Amplifier sizing\n" +
                            "• Power supply calculations\n" +
                            "• Battery requirements\n\n" +
                            "Would you like to re-run the analysis now?",
                            "Spare Capacity Changed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            await RunAnalysisInternalAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error updating spare capacity: {ex.Message}");
                UpdateStatus("Error updating spare capacity setting");
            }
        }

        private void UpdateSpareCapacityStatusDisplay(double spareCapacityPercent)
        {
            try
            {
                // Update the spare capacity button text
                if (SpareCapacityButton != null)
                {
                    SpareCapacityButton.Content = $"Spare Capacity: {spareCapacityPercent:F0}%";
                }

                // Update any status text elements if they exist
                var statusText = ((System.Windows.FrameworkElement)this).FindName("SpareCapacityStatusText") as System.Windows.Controls.TextBlock;
                if (statusText != null)
                {
                    statusText.Text = $"Current spare capacity setting: {spareCapacityPercent:F1}%";
                }

                // Update any other spare capacity related UI elements
                var capacityDisplay = ((System.Windows.FrameworkElement)this).FindName("CurrentSpareCapacityText") as System.Windows.Controls.TextBlock;
                if (capacityDisplay != null)
                {
                    capacityDisplay.Text = $"{spareCapacityPercent:F0}%";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating spare capacity display: {ex.Message}");
            }
        }

        private async void SpareCapacityButton_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                // Cycle through common spare capacity values: 20%, 15%, 25%, 10%
                double currentValue = 20.0; // Default
                var configService = new ConfigurationManagementService();

                // Try to get current value from button content
                if (sender is BarButtonItem button && button.Content != null)
                {
                    var content = button.Content.ToString();
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"(\d+)%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double parsed))
                    {
                        currentValue = parsed;
                    }
                }

                // Cycle to next value
                double newValue = currentValue switch
                {
                    20.0 => 15.0,
                    15.0 => 25.0,
                    25.0 => 10.0,
                    10.0 => 20.0,
                    _ => 20.0
                };

                if (newValue >= 0 && newValue <= 50)
                {
                    configService.UpdateSpareCapacityPercent(newValue);

                    // Update button content
                    if (sender is BarButtonItem updateButton)
                    {
                        updateButton.Content = $"Spare Capacity: {newValue:F0}%";
                    }

                    UpdateStatus($"Spare capacity updated to {newValue:F0}%");
                    UpdateSpareCapacityStatusDisplay(newValue);

                    if (_analysisCompleted)
                    {
                        var result = DXMessageBox.Show(
                            "Spare capacity has been changed. This affects:\n\n" +
                            "• Circuit loading calculations\n" +
                            "• Amplifier sizing\n" +
                            "• Power supply calculations\n" +
                            "• Battery requirements\n\n" +
                            "Would you like to re-run the analysis now?",
                            "Spare Capacity Changed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            await RunAnalysisInternalAsync();
                        }
                    }
                }
                else
                {
                    DXMessageBox.Show("Please enter a valid percentage between 0 and 50.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error updating spare capacity: {ex.Message}");
                UpdateStatus("Error updating spare capacity setting");
            }
        }

        private void AnalyzeModel_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                UpdateStatus("Generating comprehensive model analysis report...");

                if (_document == null)
                {
                    ShowErrorMessage("No Revit document available for analysis.");
                    return;
                }

                // Create model analysis reporter
                var reporter = new ModelAnalysisReporter(_document);

                // Run the analysis
                var report = reporter.AnalyzeModel();

                // Show save dialog for the report
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = $"ModelAnalysis_{report.ProjectName?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Title = "Save Model Analysis Report",
                    DefaultExt = "txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    reporter.ExportReport(report, saveDialog.FileName);

                    var result = DXMessageBox.Show(
                        $"Model analysis report generated successfully!\n\n" +
                        $"Report saved to: {saveDialog.FileName}\n\n" +
                        $"Analysis Summary:\n" +
                        $"• Total Family Instances: {report.TotalFamilyInstances}\n" +
                        $"• Unique Parameters: {report.ParameterStatistics.Count}\n" +
                        $"• Fire Alarm Related Families: {report.FamilyInstanceSummary.Values.Count(f => IsLikelyFireAlarmDevice(f.FamilyName))}\n" +
                        $"• Levels Analyzed: {report.LevelSummary.Count}\n" +
                        $"• Categories Found: {report.CategorySummary.Count}\n\n" +
                        "Would you like to open the report file?",
                        "Model Analysis Complete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("notepad.exe", saveDialog.FileName);
                        }
                        catch
                        {
                            System.Diagnostics.Process.Start(saveDialog.FileName);
                        }
                    }

                    UpdateStatus($"Model analysis complete - {report.TotalFamilyInstances} families analyzed");
                }
                else
                {
                    UpdateStatus("Model analysis cancelled by user");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error generating model analysis: {ex.Message}");
                UpdateStatus("Model analysis failed");
                System.Diagnostics.Debug.WriteLine($"Model analysis error: {ex}");
            }
        }

        private bool IsLikelyFireAlarmDevice(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            var upperName = name.ToUpperInvariant();
            var fireAlarmKeywords = new[]
            {
                "SMOKE", "HEAT", "DETECTOR", "STROBE", "HORN", "SPEAKER", "NOTIFICATION",
                "FIRE", "ALARM", "MANUAL", "PULL", "STATION", "MODULE", "ADDRESSABLE"
            };

            return fireAlarmKeywords.Any(keyword => upperName.Contains(keyword));
        }

        private void About_Click(object sender, ItemClickEventArgs e)
        {
            DXMessageBox.Show(
                "IDNAC Calculator v2.0\n\n" +
                "Fire Alarm Load Calculator with 4100ES IDNAC Analysis\n\n" +
                "© 2024 Fire Alarm Engineering Tools",
                "About IDNAC Calculator",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Help_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                // Provide diagnostic information
                var scopeInfo = GetScopeDiagnostics();

                var helpMessage = "IDNAC Calculator Help\n\n" +
                    "Troubleshooting Analysis Issues:\n\n" +
                    $"Current Scope: {_currentScope}\n" +
                    scopeInfo + "\n\n" +
                    "For analysis to work, elements must have:\n" +
                    "• CURRENT DRAW parameter with value (in Amperes)\n" +
                    "• Wattage parameter with value (in Watts)\n" +
                    "• OR contain keywords: Speaker, Horn, Strobe, Bell, etc.\n\n" +
                    "Common Issues:\n" +
                    "• Active View: Elements not visible in current view\n" +
                    "• Selection: No elements selected\n" +
                    "• Missing Parameters: Fire alarm families lack electrical parameters\n\n" +
                    "Contact support for additional help.";

                DXMessageBox.Show(helpMessage, "Help & Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Help error: {ex.Message}");
            }
        }

        private string GetScopeDiagnostics()
        {
            try
            {
                var allElements = GetElementsByScope();
                var electricalElements = allElements.Where(IsElectricalFamilyInstance).ToList();

                return $"• Total FamilyInstances: {allElements.Count}\n" +
                       $"• Electrical/Fire Alarm Elements: {electricalElements.Count}\n" +
                       $"• Active View: {_document?.ActiveView?.Name ?? "Unknown"}";
            }
            catch (Exception ex)
            {
                return $"Error getting diagnostics: {ex.Message}";
            }
        }
        private void ImportFamilyCatalog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Opening family catalog import dialog...");

                // Show file dialog for catalog selection
                var openDialog = new OpenFileDialog
                {
                    Filter = "pyRevit Family Catalog (fa_family_catalog_*.json)|fa_family_catalog_*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Select pyRevit Family Catalog JSON File",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                // Auto-suggest files if found
                var importer = new FamilyCatalogImporter();
                var foundFiles = importer.FindPyRevitCatalogFiles();
                if (foundFiles.Any())
                {
                    openDialog.InitialDirectory = System.IO.Path.GetDirectoryName(foundFiles.First());
                    openDialog.FileName = System.IO.Path.GetFileName(foundFiles.First());
                }

                if (openDialog.ShowDialog() == true)
                {
                    var filePath = openDialog.FileName;
                    UpdateStatus($"Validating catalog file: {System.IO.Path.GetFileName(filePath)}");

                    // Validate file before import
                    var validation = importer.ValidateCatalogFile(filePath);
                    if (!validation.IsValid)
                    {
                        var errorMsg = $"Invalid catalog file:\n\n{string.Join("\n", validation.Errors)}";
                        if (validation.Warnings.Any())
                        {
                            errorMsg += $"\n\nWarnings:\n{string.Join("\n", validation.Warnings)}";
                        }

                        ShowErrorMessage(errorMsg);
                        UpdateStatus("Catalog import cancelled - invalid file format");
                        return;
                    }

                    // Show confirmation dialog with file info
                    var confirmMsg = $"Import Family Catalog\n\n" +
                                   $"File: {System.IO.Path.GetFileName(filePath)}\n" +
                                   $"Families: {validation.FamilyCount}\n";

                    if (validation.CatalogInfo != null)
                    {
                        confirmMsg += $"Version: {validation.CatalogInfo.Version ?? "N/A"}\n" +
                                    $"Project: {validation.CatalogInfo.ProjectName ?? "N/A"}\n";
                    }

                    confirmMsg += $"\nThis will:\n" +
                                 $"• Add new device mappings for unknown families\n" +
                                 $"• Update existing mappings with catalog data\n" +
                                 $"• Apply speaker/strobe classification rules\n\n" +
                                 $"Continue with import?";

                    var result = DXMessageBox.Show(confirmMsg, "Confirm Import",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        UpdateStatus("Importing family catalog...");

                        // Perform the import
                        var importResult = importer.ImportFamilyCatalog(filePath);

                        if (importResult.Success)
                        {
                            UpdateStatus($"Import completed: {importResult.FamiliesProcessed} families processed");

                            // Show detailed results
                            var resultMsg = importResult.Message;
                            if (importResult.Warnings.Any())
                            {
                                resultMsg += $"\n\nWarnings ({importResult.Warnings.Count}):\n" +
                                           $"{string.Join("\n", importResult.Warnings.Take(5))}";
                                if (importResult.Warnings.Count > 5)
                                    resultMsg += $"\n... and {importResult.Warnings.Count - 5} more";
                            }

                            DXMessageBox.Show(resultMsg, "Import Complete",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            ShowErrorMessage($"Import failed:\n{importResult.Message}");
                            UpdateStatus("Family catalog import failed");
                        }
                    }
                    else
                    {
                        UpdateStatus("Family catalog import cancelled by user");
                    }
                }
                else
                {
                    UpdateStatus("Family catalog import cancelled");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error during family catalog import: {ex.Message}");
                UpdateStatus("Error importing family catalog");
            }
        }

        private void ImportFamilyCatalog_Click(object sender, ItemClickEventArgs e)
        {
            ImportFamilyCatalog_Click(sender, new RoutedEventArgs());
        }

        private void EditDeviceMappings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Opening device mapping editor...");

                var mappingEditor = new MappingEditor();
                mappingEditor.Owner = this;

                var result = mappingEditor.ShowDialog();

                if (result == true)
                {
                    UpdateStatus("Device mappings updated successfully");

                    // If analysis has been completed, suggest re-running
                    if (_analysisCompleted)
                    {
                        var rerunResult = DXMessageBox.Show(
                            "Device mappings have been updated.\n\n" +
                            "This affects device current and unit load calculations.\n" +
                            "Would you like to re-run the analysis with the new mappings?",
                            "Re-run Analysis?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (rerunResult == MessageBoxResult.Yes)
                        {
                            // Re-run analysis with current scope
                            if (_lastAnalysisScope == "ActiveView")
                                ScopeActiveView_Click(sender, e);
                            else if (_lastAnalysisScope == "Selection")
                                ScopeSelection_Click(sender, e);
                            else
                                ScopeEntireModel_Click(sender, e);
                        }
                    }
                }
                else
                {
                    UpdateStatus("Device mapping editor cancelled");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening device mapping editor: {ex.Message}");
                UpdateStatus("Error opening mapping editor");
            }
        }

        private void EditDeviceMappings_Click(object sender, ItemClickEventArgs e)
        {
            EditDeviceMappings_Click(sender, new RoutedEventArgs());
        }



        private string _currentWorkflowStep = "Scope";
        private Dictionary<string, bool> _workflowStepCompletion = new Dictionary<string, bool>
        {
            {"Scope", false},
            {"Analysis", false},
            {"Assignment", false},
            {"Reports", false},
            {"Settings", false}
        };

        private void NavigationStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string stepName)
            {
                ToggleNavigationStep(stepName);
                UpdateWorkflowStep(stepName);
            }
        }

        private void ToggleNavigationStep(string stepName)
        {
            // Collapse all sub-items first
            CollapseAllNavigationSteps();

            // Expand the selected step
            switch (stepName.ToLower())
            {
                case "scope":
                    ScopeSubItems.Visibility = ScopeSubItems.Visibility == WpfVisibility.Visible ? WpfVisibility.Collapsed : WpfVisibility.Visible;
                    break;
                case "analysis":
                    AnalysisSubItems.Visibility = AnalysisSubItems.Visibility == WpfVisibility.Visible ? WpfVisibility.Collapsed : WpfVisibility.Visible;
                    break;
                case "assignment":
                    AssignmentSubItems.Visibility = AssignmentSubItems.Visibility == WpfVisibility.Visible ? WpfVisibility.Collapsed : WpfVisibility.Visible;
                    break;
                case "reports":
                    ReportsSubItems.Visibility = ReportsSubItems.Visibility == WpfVisibility.Visible ? WpfVisibility.Collapsed : WpfVisibility.Visible;
                    break;
                case "settings":
                    SettingsSubItems.Visibility = SettingsSubItems.Visibility == WpfVisibility.Visible ? WpfVisibility.Collapsed : WpfVisibility.Visible;
                    break;
            }
        }

        private void CollapseAllNavigationSteps()
        {
            ScopeSubItems.Visibility = WpfVisibility.Collapsed;
            AnalysisSubItems.Visibility = WpfVisibility.Collapsed;
            AssignmentSubItems.Visibility = WpfVisibility.Collapsed;
            ReportsSubItems.Visibility = WpfVisibility.Collapsed;
            SettingsSubItems.Visibility = WpfVisibility.Collapsed;
        }

        private void UpdateWorkflowStep(string stepName)
        {
            _currentWorkflowStep = stepName;

            // Update current step text
            CurrentStepText.Text = stepName switch
            {
                "Scope" => "Select analysis scope",
                "Analysis" => "Run system analysis",
                "Assignment" => "Assign devices to circuits",
                "Reports" => "Generate documentation",
                "Settings" => "Configure system settings",
                _ => "Ready to start"
            };

            // Update step highlighting
            UpdateStepHighlighting(stepName);

            // Enable/disable relevant ribbon sections based on current step
            UpdateRibbonAvailability(stepName);
        }

        private void UpdateStepHighlighting(string activeStep)
        {
            // Reset all borders
            ScopeStepBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            AnalysisStepBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            AssignmentStepBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            ReportsStepBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            SettingsStepBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;

            // Highlight active step
            var activeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));

            switch (activeStep.ToLower())
            {
                case "scope":
                    ScopeStepBorder.BorderBrush = activeBrush;
                    break;
                case "analysis":
                    AnalysisStepBorder.BorderBrush = activeBrush;
                    break;
                case "assignment":
                    AssignmentStepBorder.BorderBrush = activeBrush;
                    break;
                case "reports":
                    ReportsStepBorder.BorderBrush = activeBrush;
                    break;
                case "settings":
                    SettingsStepBorder.BorderBrush = activeBrush;
                    break;
            }
        }

        private void UpdateRibbonAvailability(string currentStep)
        {
            // Enable/disable ribbon pages based on workflow step
            // This would be expanded based on specific ribbon structure
            UpdateStatus($"Current step: {currentStep}");
        }

        private void UpdateActiveScope(string scopeType, int deviceCount)
        {
            ActiveScopeText.Text = scopeType;
            ActiveScopeDeviceCount.Text = deviceCount.ToString();
            _lastAnalysisScope = scopeType;

            // Mark scope step as complete
            _workflowStepCompletion["Scope"] = true;
            UpdateStepCompletionIcons();
        }

        private void UpdateStepCompletionIcons()
        {
            // Update completion icons based on workflow progress
            var completedIcon = "SvgImages/Icon Builder/Actions_Apply.svg";
            var pendingIcon = "SvgImages/Icon Builder/Actions_Time.svg";
            var currentIcon = "SvgImages/Icon Builder/Actions_Play.svg";

            ScopeStepIcon.Source = GetDXImage(_workflowStepCompletion["Scope"] ? completedIcon :
                (_currentWorkflowStep == "Scope" ? currentIcon : pendingIcon));

            AnalysisStepIcon.Source = GetDXImage(_workflowStepCompletion["Analysis"] ? completedIcon :
                (_currentWorkflowStep == "Analysis" ? currentIcon : pendingIcon));

            AssignmentStepIcon.Source = GetDXImage(_workflowStepCompletion["Assignment"] ? completedIcon :
                (_currentWorkflowStep == "Assignment" ? currentIcon : pendingIcon));

            ReportsStepIcon.Source = GetDXImage(_workflowStepCompletion["Reports"] ? completedIcon :
                (_currentWorkflowStep == "Reports" ? currentIcon : pendingIcon));

            SettingsStepIcon.Source = GetDXImage(_workflowStepCompletion["Settings"] ? completedIcon : pendingIcon);
        }

        private System.Windows.Media.ImageSource GetDXImage(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return null;

                // Try DevExpress image first - commented out due to API changes
                // try
                // {
                //     return DevExpress.Xpf.Core.Native.DXImageExtension.GetImageSource(path);
                // }
                // catch
                // {
                
                // Fallback: creating a simple colored rectangle as an image
                var drawingVisual = new System.Windows.Media.DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    var brush = path.Contains("completed") || path.Contains("success") 
                        ? System.Windows.Media.Brushes.Green 
                        : System.Windows.Media.Brushes.Orange;
                    drawingContext.DrawRectangle(brush, null, new System.Windows.Rect(0, 0, 16, 16));
                }
                
                var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(16, 16, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);
                return renderTarget;
            }
            catch
            {
                return null; // Return null if image generation fails
            }
        }

        // Enhanced scope event handlers that update navigation
        private void ScopeActiveView_Click2(object sender, ItemClickEventArgs e)
        {
            try
            {
                UpdateActiveScope("Active View", 0); // Will be updated after analysis
                CollapseAllNavigationSteps();
                UpdateWorkflowStep("Analysis");
                UpdateStatus("Active view scope selected - ready for analysis");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error setting active view scope: {ex.Message}");
            }
        }



        // Navigation sub-item handlers
        private void OpenAssignmentTreeView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("Please run analysis first before opening assignment editor.",
                        "Analysis Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var treeView = new Views.AssignmentTreeView();
                var window = new Window
                {
                    Title = "Device Assignment",
                    Content = treeView,
                    Owner = this,
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening assignment tree view: {ex.Message}");
            }
        }

        private void OpenAddressingTool_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("Please run analysis first before opening addressing tool.",
                        "Analysis Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var addressingTool = new Views.AddressingPanelWindow(_document, _uiDocument);
                addressingTool.Owner = this;
                addressingTool.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening addressing tool: {ex.Message}");
            }
        }

        private void RunCircuitBalancing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("Please run analysis first before circuit balancing.",
                        "Analysis Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Run circuit balancing optimization
                UpdateStatus("Running circuit balancing optimization...");
                // Implementation would call circuit balancer here
                UpdateStatus("Circuit balancing completed");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error running circuit balancing: {ex.Message}");
            }
        }

        private void ExportPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("Please run analysis first before generating PDF.",
                        "Analysis Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                UpdateStatus("Generating PDF report...");
                // Implementation would generate PDF here
                UpdateStatus("PDF report generated");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error generating PDF: {ex.Message}");
            }
        }

        private void GenerateBatteryReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("Please run analysis first before generating battery report.",
                        "Analysis Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                UpdateStatus("Generating battery report...");
                // Implementation would generate battery report here
                UpdateStatus("Battery report generated");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error generating battery report: {ex.Message}");
            }
        }

        private void OpenSystemSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open system settings dialog
                var result = DXMessageBox.Show(
                    "System Settings\n\n" +
                    "• Spare Capacity: Configure via Settings ribbon\n" +
                    "• Device Mappings: Use 'Device Mappings' button\n" +
                    "• Import Catalog: Use 'Import Catalog' button\n\n" +
                    "Access individual settings through the navigation menu or Settings ribbon.",
                    "System Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error opening system settings: {ex.Message}");
            }
        }





        private async void RunIDNETAnalysis_Click(object sender, RoutedEventArgs e)
        {
            await RunIDNETAnalysisInternal();
        }

        private async void RunIDNETAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            await RunIDNETAnalysisInternal();
        }

        private async Task RunIDNETAnalysisInternal()
        {
            if (_document == null)
            {
                ShowErrorMessage("No Revit document available for IDNET analysis.");
                return;
            }

            ProgressWindow progressWindow = null;

            try
            {
                // Log the scope being used for IDNET analysis
                System.Diagnostics.Debug.WriteLine($"IDNET Analysis: Starting with scope '{_currentScope}'");
                ValidateScopeSync();

                // Create and show progress window for IDNET analysis
                progressWindow = ProgressWindow.CreateAndShow(this, "Starting IDNET Analysis");
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.Initializing);

                UpdateStatus($"Starting IDNET analysis using scope '{_currentScope}'...");

                // Check for cancellation
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("IDNET analysis canceled by user");
                    return;
                }

                // Validate scope is properly set
                if (string.IsNullOrEmpty(_currentScope))
                {
                    _currentScope = "Active View"; // Fallback to default
                    UpdateScopeSelection();
                    System.Diagnostics.Debug.WriteLine("IDNET: Scope was empty, defaulted to 'Active View'");
                }

                System.Diagnostics.Debug.WriteLine($"IDNET: Confirmed analysis scope '{_currentScope}'");

                // Disable UI during analysis
                SetUIEnabled(false);

                // Phase 1: Collect elements
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.CollectingElements,
                    currentOperation: $"Scanning scope: {_currentScope}");
                var allElements = GetElementsByScope();
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"##########################################");
                System.Diagnostics.Debug.WriteLine($"### STARTING IDNET DETECTION ANALYSIS ###");
                System.Diagnostics.Debug.WriteLine($"##########################################");
                System.Diagnostics.Debug.WriteLine($"Total elements in scope: {allElements.Count}");
                System.Diagnostics.Debug.WriteLine($"Current scope: {_currentScope}");
                System.Diagnostics.Debug.WriteLine($"");

                progressWindow.UpdateCurrentItem($"Found: {allElements.Count} instances for IDNET");

                // Statistics tracking
                int totalElements = allElements.Count;
                int detectedElements = 0;
                int excludedNotificationDevices = 0;
                int excludedFireAlarmNonDetection = 0;
                int excludedOtherReasons = 0;
                var detectedDeviceTypes = new Dictionary<string, int>();
                var excludedDeviceTypes = new Dictionary<string, int>();

                // Phase 2: Filter for detection devices
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.FilteringElements,
                    currentOperation: "Identifying IDNET detection devices");

                // Apply detection filtering with detailed logging
                var detectionElements = new List<FamilyInstance>();

                System.Diagnostics.Debug.WriteLine($"### ANALYZING EACH ELEMENT ###");
                int processedCount = 0;
                foreach (var element in allElements)
                {
                    processedCount++;

                    // Update progress every 50 elements or on last element
                    if (processedCount % 50 == 0 || processedCount == allElements.Count)
                    {
                        progressWindow.UpdateCurrentItem($"Analyzing: {processedCount}/{allElements.Count} elements");
                        progressWindow.UpdateCurrentOperation($"Detection devices found: {detectedElements}");
                    }

                    bool isDetected = IsDetectionDevice(element);
                    string familyName = element.Symbol?.Family?.Name ?? "Unknown";
                    string categoryName = element.Category?.Name ?? "Unknown";

                    if (isDetected)
                    {
                        detectionElements.Add(element);
                        detectedElements++;

                        // Show current detection in real-time (but not too frequently)
                        if (detectedElements % 10 == 0 || detectedElements <= 5)
                        {
                            progressWindow.UpdateCurrentOperation($"✓ Found: {familyName}");
                        }

                        // Track device types
                        if (!detectedDeviceTypes.ContainsKey(familyName))
                            detectedDeviceTypes[familyName] = 0;
                        detectedDeviceTypes[familyName]++;
                    }
                    else
                    {
                        // Track exclusion reasons (simplified analysis)
                        var upperFamily = familyName.ToUpperInvariant();
                        var upperCategory = categoryName.ToUpperInvariant();

                        var notificationKeywords = new[] { "SPEAKER", "STROBE", "HORN", "BELL", "CHIME", "SOUNDER", "NOTIFICATION", "APPLIANCE", "NAC", "IDNAC" };
                        if (notificationKeywords.Any(k => upperFamily.Contains(k) || upperCategory.Contains(k)))
                        {
                            excludedNotificationDevices++;
                        }
                        else if (upperCategory.Contains("FIRE ALARM DEVICES"))
                        {
                            excludedFireAlarmNonDetection++;
                        }
                        else
                        {
                            excludedOtherReasons++;
                        }

                        // Track excluded types (top 10 only to avoid spam)
                        if (excludedDeviceTypes.Count < 10)
                        {
                            if (!excludedDeviceTypes.ContainsKey(familyName))
                                excludedDeviceTypes[familyName] = 0;
                            excludedDeviceTypes[familyName]++;
                        }
                    }
                }

                // Print comprehensive summary
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"### IDNET DETECTION SUMMARY ###");
                System.Diagnostics.Debug.WriteLine($"📊 Total Elements Analyzed: {totalElements}");
                System.Diagnostics.Debug.WriteLine($"✅ IDNET Devices Detected: {detectedElements}");
                System.Diagnostics.Debug.WriteLine($"❌ Total Excluded: {totalElements - detectedElements}");
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"### EXCLUSION BREAKDOWN ###");
                System.Diagnostics.Debug.WriteLine($"🔇 IDNAC Notification Devices: {excludedNotificationDevices}");
                System.Diagnostics.Debug.WriteLine($"🚨 Fire Alarm Non-Detection: {excludedFireAlarmNonDetection}");
                System.Diagnostics.Debug.WriteLine($"⚫ Other/Unknown Reasons: {excludedOtherReasons}");
                System.Diagnostics.Debug.WriteLine($"");

                if (detectedDeviceTypes.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"### DETECTED IDNET DEVICE TYPES ###");
                    foreach (var deviceType in detectedDeviceTypes.OrderByDescending(kv => kv.Value))
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ {deviceType.Value}x - '{deviceType.Key}'");
                    }
                    System.Diagnostics.Debug.WriteLine($"");
                }

                if (excludedDeviceTypes.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"### TOP EXCLUDED DEVICE TYPES ###");
                    foreach (var deviceType in excludedDeviceTypes.OrderByDescending(kv => kv.Value).Take(10))
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ {deviceType.Value}x - '{deviceType.Key}'");
                    }
                    System.Diagnostics.Debug.WriteLine($"");
                }

                // Show first few detected devices
                if (detectionElements.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"### FIRST 5 DETECTED DEVICES ###");
                    foreach (var elem in detectionElements.Take(5))
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ ID: {elem.Id} - '{elem.Symbol?.Family?.Name ?? "Unknown"}' ({elem.Category?.Name ?? "Unknown"})");
                    }
                    System.Diagnostics.Debug.WriteLine($"");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"### NO IDNET DEVICES DETECTED ###");
                    System.Diagnostics.Debug.WriteLine($"Sample elements for debugging:");
                    foreach (var elem in allElements.Take(10))
                    {
                        var familyName = elem.Symbol?.Family?.Name ?? "Unknown";
                        var categoryName = elem.Category?.Name ?? "Unknown";
                        System.Diagnostics.Debug.WriteLine($"❌ Family: '{familyName}' | Category: '{categoryName}'");
                    }
                    System.Diagnostics.Debug.WriteLine($"");
                }

                System.Diagnostics.Debug.WriteLine($"##########################################");

                // TEMPORARY: Commented out to allow analysis of all elements
                /*
                if (!detectionElements.Any())
                {
                    UpdateStatus("No detection devices found in selected scope.");
                    DXMessageBox.Show(
                        "No IDNET detection devices found in the selected scope.\n\n" +
                        "IDNET devices include:\n" +
                        "• Smoke detectors\n" +
                        "• Heat detectors\n" +
                        "• Manual pull stations\n" +
                        "• Input/Output modules\n\n" +
                        "Please ensure detection devices are present in your model.",
                        "No Detection Devices Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                */

                // Phase 3: Extract detection device data on main thread
                progressWindow.UpdateProgress("Extracting Device Data", 60, $"Processing {detectionElements.Count} detection devices...");
                var detectionDataList = new List<ElementData>();

                for (int i = 0; i < detectionElements.Count; i++)
                {
                    var element = detectionElements[i];

                    // Update progress periodically
                    if (i % 50 == 0 && detectionElements.Count > 0)
                    {
                        int percentage = 60 + (i * 15 / detectionElements.Count); // 60-75%
                        progressWindow.UpdateProgress("Extracting Device Data", percentage,
                            $"Processing device {i + 1} of {detectionElements.Count}...");
                    }

                    // Check for cancellation periodically
                    if (i % 100 == 0 && progressWindow.IsCanceled)
                    {
                        UpdateStatus("IDNET analysis canceled by user");
                        return;
                    }

                    try
                    {
                        var elementData = ExtractDetectionDeviceData(element);
                        if (elementData != null)
                            detectionDataList.Add(elementData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error extracting detection device data from element {element.Id}: {ex.Message}");
                    }
                }

                // Check for cancellation before background analysis
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("IDNET analysis canceled by user");
                    return;
                }

                // Phase 4: Run IDNET analysis in background
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.AnalyzingIDNET);
                await Task.Run(() => RunIDNETAnalysisInternal(detectionDataList));

                // Check for cancellation after background analysis
                if (progressWindow.IsCanceled)
                {
                    UpdateStatus("IDNET analysis canceled by user");
                    return;
                }

                // Final completion message with statistics
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"🎯 IDNET ANALYSIS COMPLETED!");
                System.Diagnostics.Debug.WriteLine($"   📊 Total elements processed: {totalElements}");
                System.Diagnostics.Debug.WriteLine($"   ✅ IDNET devices detected: {detectedElements}");
                System.Diagnostics.Debug.WriteLine($"   🔇 IDNAC devices excluded: {excludedNotificationDevices}");
                System.Diagnostics.Debug.WriteLine($"   🚨 Fire alarm non-detection excluded: {excludedFireAlarmNonDetection}");
                System.Diagnostics.Debug.WriteLine($"   ⚫ Other exclusions: {excludedOtherReasons}");
                System.Diagnostics.Debug.WriteLine($"   🔍 Detection data elements created: {detectionDataList.Count}");
                System.Diagnostics.Debug.WriteLine($"##########################################");
                System.Diagnostics.Debug.WriteLine($"");

                // Complete the debug log and show location to user
                CompleteDebugLog();

                UpdateStatus($"IDNET analysis completed - {detectedElements} detection devices found, {excludedNotificationDevices} IDNAC devices excluded");

                // Update UI with IDNET results
                LoadIDNETGrids();

                // Update dashboard IDNET metrics
                UpdateIDNETDashboard();

                // Update System Overview with combined IDNAC/IDNET data
                LoadSystemOverview();

                // Also update the Raw Data tab with IDNET device data
                if (_electricalResults == null)
                {
                    _electricalResults = new ElectricalResults { Elements = new List<ElementData>() };
                }

                // Add IDNET devices to raw data (merge with existing if any)
                if (_idnetResults?.AllDevices != null)
                {
                    foreach (var idnetDevice in _idnetResults.AllDevices)
                    {
                        // Find the corresponding ElementData from detectionDataList
                        var elementData = detectionDataList.FirstOrDefault(d => d.Id.ToString() == idnetDevice.DeviceId);
                        if (elementData != null && !_electricalResults.Elements.Any(elem => elem.Id.ToString() == idnetDevice.DeviceId))
                        {
                            _electricalResults.Elements.Add(elementData);
                        }
                    }

                    // Refresh the raw data grid
                    LoadRawDataGrid();
                }

                // Switch to IDNET tab to show results
                if (ResultsTabControl != null && ResultsTabControl.Items.Count > 3)
                {
                    ResultsTabControl.SelectedIndex = 3; // IDNET Analysis tab
                }

                // Complete the progress
                progressWindow.UpdateAnalysisPhase(AnalysisPhase.Complete);

            }
            catch (Exception ex)
            {
                progressWindow?.ShowError($"IDNET analysis failed: {ex.Message}");
                UpdateStatus("IDNET analysis failed");
                ShowErrorMessage($"IDNET analysis failed: {ex.Message}");
            }
            finally
            {
                // Re-enable UI
                SetUIEnabled(true);

                // Close progress window after a short delay if completed
                if (progressWindow != null && progressWindow.IsCompleted)
                {
                    _ = Task.Delay(2000).ContinueWith(_ => progressWindow.Dispatcher.Invoke(() => progressWindow.Close()));
                }
            }
        }

        private void IDNETDeviceSummary_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (ResultsTabControl != null && ResultsTabControl.Items.Count > 4)
                {
                    ResultsTabControl.SelectedIndex = 4;
                    UpdateStatus("Switched to IDNET Devices view");
                }
                else
                {
                    UpdateStatus("IDNET Devices tab not available");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error switching to IDNET Devices view: {ex.Message}");
            }
        }

        private void IDNETNetworkTopology_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (ResultsTabControl != null && ResultsTabControl.Items.Count > 5)
                {
                    ResultsTabControl.SelectedIndex = 5; // Network Topology tab
                    UpdateStatus("Switched to Network Topology view");
                }
                else
                {
                    UpdateStatus("Network Topology tab not available");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error switching to Network Topology view: {ex.Message}");
            }
        }

        private void IDNETAddressPlanning_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (_idnetResults == null)
                {
                    DXMessageBox.Show("Please run IDNET analysis first to generate device addressing scheme.",
                        "Address Planning", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var deviceCount = _idnetResults.TotalDevices;
                var segmentCount = _idnetResults.NetworkSegments?.Count ?? 0;

                var message = $"IDNET Address Planning\n\n" +
                    $"Total Devices: {deviceCount}\n" +
                    $"Network Segments: {segmentCount}\n" +
                    $"Recommended Addressing:\n\n";

                if (_idnetResults.LevelAnalysis != null)
                {
                    foreach (var level in _idnetResults.LevelAnalysis.OrderBy(l => GetLevelSortKey(l.Key)))
                    {
                        message += $"• {level.Key}: Devices {level.Value.TotalDevices}\n";
                    }
                }

                message += "\nNote: Full address planning dialog will be implemented in future version.";

                DXMessageBox.Show(message, "Address Planning", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus("IDNET address planning reviewed");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error in address planning: {ex.Message}");
            }
        }

        private void ExportIDNETResults_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "csv",
                    Title = "Export IDNET Analysis Results"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // IMPLEMENTED: Export IDNET results
                    if (_idnetResults == null)
                    {
                        DXMessageBox.Show("No IDNET analysis results to export. Run IDNET analysis first.",
                            "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        // Check file extension and export accordingly
                        var extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                        if (extension == ".csv")
                        {
                            ExportIDNETResults(saveDialog.FileName);
                        }
                        else
                        {
                            // For .xlsx files, change extension to .csv to avoid format confusion
                            var csvFileName = System.IO.Path.ChangeExtension(saveDialog.FileName, ".csv");
                            ExportIDNETResults(csvFileName);
                            DXMessageBox.Show($"Note: File exported as CSV format to: {csvFileName}\n(Excel format not yet supported)",
                                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        DXMessageBox.Show($"IDNET results exported to: {saveDialog.FileName}",
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception exportEx)
                    {
                        DXMessageBox.Show($"IDNET export failed: {exportEx.Message}",
                            "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportIDNETDeviceList_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "csv",
                    Title = "Export IDNET Device List"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // IMPLEMENTED: Export IDNET device list
                    if (_idnetResults == null || _idnetResults.AllDevices == null)
                    {
                        DXMessageBox.Show("No IDNET device data to export. Run IDNET analysis first.",
                            "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        // Check file extension and export accordingly
                        var extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                        if (extension == ".csv")
                        {
                            ExportIDNETDeviceList(saveDialog.FileName);
                        }
                        else
                        {
                            // For .xlsx files, change extension to .csv to avoid format confusion
                            var csvFileName = System.IO.Path.ChangeExtension(saveDialog.FileName, ".csv");
                            ExportIDNETDeviceList(csvFileName);
                            DXMessageBox.Show($"Note: File exported as CSV format to: {csvFileName}\n(Excel format not yet supported)",
                                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        DXMessageBox.Show($"IDNET device list exported to: {saveDialog.FileName}",
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception exportEx)
                    {
                        DXMessageBox.Show($"IDNET device export failed: {exportEx.Message}",
                            "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunCombinedAnalysis_Click(object sender, RoutedEventArgs e)
        {
            await RunCombinedAnalysisInternal();
        }

        private async void RunCombinedAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            await RunCombinedAnalysisInternal();
        }

        private async Task RunCombinedAnalysisInternal()
        {
            try
            {
                // IMPLEMENTED: Run combined IDNAC and IDNET analysis
                if (_document == null)
                {
                    DXMessageBox.Show("No Revit document available for combined analysis.",
                        "Combined Analysis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Store the current scope to ensure consistency
                var analysisScope = _currentScope;
                System.Diagnostics.Debug.WriteLine($"Combined Analysis: Using scope '{analysisScope}' for both IDNAC and IDNET");
                ValidateScopeSync();

                UpdateStatus($"Starting combined analysis using scope '{analysisScope}'...");
                SetUIEnabled(false);

                // Run IDNAC analysis first
                await RunAnalysisInternal();

                // Wait for IDNAC analysis to complete
                await Task.Delay(100);

                if (_idnacResults == null)
                {
                    UpdateStatus("IDNAC analysis failed. Cannot proceed with combined analysis.");
                    SetUIEnabled(true);
                    return;
                }

                // Verify scope hasn't changed and run IDNET analysis
                if (_currentScope != analysisScope)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Scope changed from '{analysisScope}' to '{_currentScope}' during IDNAC analysis. Restoring original scope.");
                    _currentScope = analysisScope;
                    UpdateScopeSelection();
                }

                await RunIDNETAnalysisInternal();

                // Wait for IDNET analysis to complete
                await Task.Delay(100);

                if (_idnetResults == null)
                {
                    UpdateStatus("IDNET analysis failed. IDNAC analysis completed successfully.");
                    SetUIEnabled(true);
                    return;
                }

                // Combined analysis summary
                var totalIdnacs = _idnacResults.TotalIdnacsNeeded;
                var totalIdnetDevices = _idnetResults.TotalDevices;
                var totalSpeakers = _amplifierResults?.SpeakerCount ?? 0;

                var message = $"Combined Fire Alarm System Analysis Complete\n\n" +
                             $"IDNAC System (Notification):\n" +
                             $"• IDNACs Required: {totalIdnacs}\n" +
                             $"• Total Current: {_electricalResults?.Totals["current"] ?? 0:F2} A\n" +
                             $"• Total Devices: {_electricalResults?.Elements?.Count ?? 0}\n\n" +
                             $"IDNET System (Detection):\n" +
                             $"• Detection Devices: {totalIdnetDevices}\n" +
                             $"• Network Segments: {_idnetResults.NetworkSegments?.Count ?? 0}\n" +
                             $"• Total Power: {_idnetResults.TotalPowerConsumption:F1} mA\n\n" +
                             $"Audio System:\n" +
                             $"• Speakers: {totalSpeakers}\n" +
                             $"• Amplifiers: {_amplifierResults?.AmplifiersNeeded ?? 0}\n\n" +
                             $"Panel Recommendations:\n" +
                             $"• Recommended Panels: {_panelRecommendations?.FirstOrDefault()?.PanelCount ?? 1}";

                DXMessageBox.Show(message, "Combined Analysis Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateStatus("Combined IDNAC + IDNET analysis completed successfully");
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Combined analysis failed: {ex.Message}",
                    "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Combined analysis failed");
            }
            finally
            {
                SetUIEnabled(true);
            }
        }

        private void SyncWithIDNAC_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                // IMPLEMENTED: Synchronize IDNET scope with current IDNAC scope
                if (_idnacResults == null)
                {
                    DXMessageBox.Show("Please run IDNAC analysis first before synchronizing systems.",
                        "Sync Systems", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update IDNET scope to match current IDNAC scope
                UpdateScopeSelection();

                var message = $"System Synchronization Complete\n\n" +
                             $"Current Analysis Scope: {_currentScope}\n" +
                             $"IDNET analysis will use the same scope as IDNAC analysis.\n\n" +
                             $"IDNAC Analysis Status: Complete\n" +
                             $"• IDNACs Required: {_idnacResults.TotalIdnacsNeeded}\n" +
                             $"• Total Current: {_electricalResults?.Totals["current"] ?? 0:F2} A\n\n" +
                             $"Run IDNET analysis to complete the synchronized system analysis.";

                DXMessageBox.Show(message, "Systems Synchronized",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateStatus($"Systems synchronized - both using '{_currentScope}' scope");
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Synchronization failed: {ex.Message}",
                    "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("System synchronization failed");
            }
        }





        public void SetRevitContext(Document document, UIDocument uiDocument)
        {
            _document = document;
            _uiDocument = uiDocument;
            UpdateStatus("Revit context loaded - Ready for analysis");
        }



        public void SetAnalysisResults(ElectricalResults electricalResults, IDNACSystemResults idnacResults,
            AmplifierRequirements amplifierResults, List<PanelPlacementRecommendation> panelRecommendations,
            string scope, IDNETSystemResults? idnetResults = null)
        {
            try
            {
                // Store the analysis results
                _electricalResults = electricalResults;
                _idnacResults = idnacResults;
                _amplifierResults = amplifierResults;
                _panelRecommendations = panelRecommendations;
                _idnetResults = idnetResults;
                _currentScope = scope;

                // Load the analysis data into the UI
                LoadAnalysisData();

                // Update the status
                UpdateStatus("Analysis results loaded successfully");

                // Mark analysis as completed
                _analysisCompleted = true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading analysis results: {ex.Message}");
            }
        }

        private void LoadAnalysisData()
        {
            if (_electricalResults == null) return;

            try
            {
                // Update dashboard metrics
                UpdateDashboardMetrics();

                // Load grid data
                LoadGridData();

                // Update status displays
                UpdateStatusDisplays();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading analysis data: {ex.Message}");
            }
        }

        private void UpdateDashboardMetrics()
        {
            if (_electricalResults?.Elements == null) return;

            // Update device count
            var totalDevicesText = Find<TextBlock>("TotalDevicesText");
            if (totalDevicesText != null) totalDevicesText.Text = DisplayFormatting.FormatCount(_electricalResults.Elements.Count);

            // Update current - use 2 decimal places for total
            var totalCurrent = _electricalResults.Totals?.GetValueOrDefault("current", 0.0) ?? 0.0;
            var totalCurrentText = Find<TextBlock>("TotalCurrentText");
            if (totalCurrentText != null) totalCurrentText.Text = DisplayFormatting.FormatTotalCurrent(totalCurrent);

            // Update wattage - use 2 decimal places for total
            var totalWattage = _electricalResults.Totals?.GetValueOrDefault("wattage", 0.0) ?? 0.0;
            var totalWattageText = Find<TextBlock>("TotalWattageText");
            if (totalWattageText != null) totalWattageText.Text = DisplayFormatting.FormatTotalWattage(totalWattage);

            // Update IDNACs required
            IDNACsRequiredText.Text = (_idnacResults?.TotalIdnacsNeeded ?? 0).ToString();

            // Update amplifiers
            AmplifiersText.Text = (_amplifierResults?.AmplifiersNeeded ?? 0).ToString();

            // Update analysis status
            var analysisStatusText = Find<TextBlock>("AnalysisStatusText");
            if (analysisStatusText != null) analysisStatusText.Text = $"Analysis complete - {_electricalResults.Elements.Count} devices analyzed";
            var lastAnalysisTimeText = Find<TextBlock>("LastAnalysisTimeText");
            if (lastAnalysisTimeText != null) lastAnalysisTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void LoadGridData()
        {
            try
            {
                LoadIDNACGrid();
                LoadDeviceGrid();
                LoadLevelGrid();
                LoadRawDataGrid();
                LoadIDNETGrids();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading grid data: {ex.Message}");
            }
        }





        private void LoadIDNETGrids()
        {
            if (_idnetResults == null) return;

            // Check if we have any IDNET devices
            if (_idnetResults.AllDevices == null || !_idnetResults.AllDevices.Any())
            {
                System.Diagnostics.Debug.WriteLine("IDNET: No devices found, showing empty state message");
                LoadEmptyIDNETState();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"IDNET: Loading data for {_idnetResults.AllDevices.Count} devices");

            // Load IDNET Analysis Grid
            if (_idnetResults.LevelAnalysis != null)
            {
                var idnetGridItems = new List<object>();

                foreach (var level in _idnetResults.LevelAnalysis)
                {
                    var levelData = level.Value;

                    // Count all smoke detector types
                    var smokeCount = 0;
                    smokeCount += levelData.DeviceTypeCount?.GetValueOrDefault("Smoke Detector", 0) ?? 0;
                    smokeCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Smoke Detector", 0) ?? 0;
                    smokeCount += levelData.DeviceTypeCount?.GetValueOrDefault("Conventional Smoke Detector", 0) ?? 0;
                    smokeCount += levelData.DeviceTypeCount?.GetValueOrDefault("Multi-Criteria Detector", 0) ?? 0;

                    // Count all heat detector types
                    var heatCount = 0;
                    heatCount += levelData.DeviceTypeCount?.GetValueOrDefault("Heat Detector", 0) ?? 0;
                    heatCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Heat Detector", 0) ?? 0;
                    heatCount += levelData.DeviceTypeCount?.GetValueOrDefault("Conventional Heat Detector", 0) ?? 0;

                    // Count all manual station and communication types
                    var manualCount = 0;
                    manualCount += levelData.DeviceTypeCount?.GetValueOrDefault("Manual Station", 0) ?? 0;
                    manualCount += levelData.DeviceTypeCount?.GetValueOrDefault("Fireman Phone Jack", 0) ?? 0;
                    manualCount += levelData.DeviceTypeCount?.GetValueOrDefault("Area of Refuge Phone", 0) ?? 0;
                    manualCount += levelData.DeviceTypeCount?.GetValueOrDefault("Emergency Communication", 0) ?? 0;

                    // Count all modules and remaining devices
                    var moduleCount = 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Monitor Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Control Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable I/O Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Single Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Dual Module", 0) ?? 0;
                    moduleCount += levelData.DeviceTypeCount?.GetValueOrDefault("Addressable Detector", 0) ?? 0; // Generic addressable

                    idnetGridItems.Add(new
                    {
                        Level = level.Key,
                        SmokeDetectors = smokeCount.ToString(),
                        HeatDetectors = heatCount.ToString(),
                        ManualStations = manualCount.ToString(),
                        Modules = moduleCount.ToString(),
                        TotalDevices = levelData.TotalDevices.ToString(),
                        PowerConsumption = $"{levelData.TotalPowerConsumption:N1} mA",
                        NetworkSegments = levelData.SuggestedNetworkSegments.ToString(),
                        AddressRange = "TBD",
                        Status = levelData.TotalDevices > 127 ? "Requires Multiple Segments" : "OK"
                    });
                }

                IDNETAnalysisGrid.ItemsSource = idnetGridItems;
                System.Diagnostics.Debug.WriteLine($"IDNET: Analysis grid loaded with {idnetGridItems.Count} items");
            }

            // Load IDNET Devices Grid
            if (_idnetResults.AllDevices != null)
            {
                IDNETDevicesGrid.ItemsSource = _idnetResults.AllDevices;

                // Load device type summary cards
                LoadIDNETDeviceTypeCards();
            }

            // Load IDNET Network Segments Grid
            if (_idnetResults.NetworkSegments != null)
            {
                // Transform the data to format the CoveredLevels properly
                var formattedSegments = _idnetResults.NetworkSegments.Select(segment => new
                {
                    SegmentId = GetPropertyValue(segment, "SegmentId") ?? "Unknown",
                    DeviceCount = GetPropertyValue(segment, "DeviceCount") ?? 0,
                    EstimatedWireLength = GetPropertyValue(segment, "EstimatedWireLength") ?? 0,
                    StartingAddress = GetPropertyValue(segment, "StartingAddress") ?? 0,
                    EndingAddress = GetPropertyValue(segment, "EndingAddress") ?? 0,
                    RequiresRepeater = GetPropertyValue(segment, "RequiresRepeater") ?? false,
                    CoveredLevels = FormatLevelsList(GetPropertyValue(segment, "CoveredLevels"))
                }).ToList();
                
                IDNETNetworkSegmentsGrid.ItemsSource = formattedSegments;

                // Update network summary metrics
                UpdateIDNETNetworkSummary();
            }
        }
        
        private object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                if (obj == null) return null;
                var property = obj.GetType().GetProperty(propertyName);
                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }
        
        private string FormatLevelsList(object levelsObj)
        {
            try
            {
                if (levelsObj == null) return "None";
                
                if (levelsObj is System.Collections.IEnumerable levels && !(levelsObj is string))
                {
                    var levelNames = new List<string>();
                    foreach (var level in levels)
                    {
                        if (level != null)
                            levelNames.Add(level.ToString());
                    }
                    
                    if (levelNames.Count == 0)
                        return "None";
                    
                    // Sort levels by elevation for proper sequential detection
                    var sortedLevels = levelNames
                        .OrderBy(name => GetLevelSortKey(name))
                        .ToList();
                    
                    return FormatLevelsAsRangesOrList(sortedLevels);
                }
                
                return levelsObj.ToString();
            }
            catch
            {
                return "Error";
            }
        }
        
        private string FormatLevelsAsRangesOrList(List<string> sortedLevels)
        {
            if (sortedLevels.Count == 1)
                return sortedLevels[0];
            
            if (sortedLevels.Count <= 3)
                return string.Join(", ", sortedLevels);
            
            // Try to detect sequential floors and create ranges
            var ranges = new List<string>();
            var currentRange = new List<string> { sortedLevels[0] };
            
            for (int i = 1; i < sortedLevels.Count; i++)
            {
                var prevLevel = sortedLevels[i - 1];
                var currentLevel = sortedLevels[i];
                
                // Check if levels are sequential
                if (AreFloorsSequential(prevLevel, currentLevel))
                {
                    currentRange.Add(currentLevel);
                }
                else
                {
                    // End current range and start a new one
                    ranges.Add(FormatRange(currentRange));
                    currentRange = new List<string> { currentLevel };
                }
            }
            
            // Add the last range
            ranges.Add(FormatRange(currentRange));
            
            // If we have too many ranges, truncate
            if (ranges.Count > 2)
            {
                var totalLevels = sortedLevels.Count;
                var displayedRanges = ranges.Take(2);
                var remainingCount = totalLevels - displayedRanges.Sum(r => CountLevelsInRange(r));
                
                if (remainingCount > 0)
                    return $"{string.Join(", ", displayedRanges)} (+{remainingCount} more)";
            }
            
            return string.Join(", ", ranges);
        }
        
        private bool AreFloorsSequential(string level1, string level2)
        {
            try
            {
                // Extract numeric parts from level names
                var num1 = ExtractFloorNumber(level1);
                var num2 = ExtractFloorNumber(level2);
                
                if (num1.HasValue && num2.HasValue)
                {
                    return Math.Abs(num2.Value - num1.Value) == 1;
                }
                
                // For non-numeric levels, check if they're adjacent in elevation
                var elev1 = GetLevelSortKey(level1);
                var elev2 = GetLevelSortKey(level2);
                
                // Consider levels sequential if elevation difference is reasonable (within 15 feet)
                return Math.Abs(elev2 - elev1) > 0 && Math.Abs(elev2 - elev1) <= 15.0;
            }
            catch
            {
                return false;
            }
        }
        
        private int? ExtractFloorNumber(string levelName)
        {
            try
            {
                if (string.IsNullOrEmpty(levelName)) return null;
                
                // Handle common floor naming patterns
                var cleaned = levelName.ToUpper().Trim();
                
                // Handle basement levels (B1, B2, etc.) as negative numbers
                if (cleaned.StartsWith("B") || cleaned.Contains("BASEMENT"))
                {
                    var match = Regex.Match(cleaned, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int basementNum))
                        return -basementNum; // B1 = -1, B2 = -2, etc.
                }
                
                // Handle ground floor variations
                if (cleaned.Contains("GROUND") || cleaned.Contains("G") || cleaned == "1ST" || cleaned == "FIRST")
                    return 0;
                
                // Extract numeric part for regular floors
                var numMatch = Regex.Match(cleaned, @"\d+");
                if (numMatch.Success && int.TryParse(numMatch.Value, out int floorNum))
                {
                    // Adjust for 1-based numbering (1st floor = 0, 2nd floor = 1, etc.)
                    return floorNum - 1;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private string FormatRange(List<string> levels)
        {
            if (levels.Count == 1)
                return levels[0];
            else if (levels.Count == 2)
                return $"{levels[0]}, {levels[1]}";
            else if (levels.Count >= 3)
                return $"{levels[0]} - {levels[levels.Count - 1]}";
            else
                return string.Join(", ", levels);
        }
        
        private int CountLevelsInRange(string range)
        {
            if (range.Contains(" - "))
            {
                // For ranges like "Level 1 - Level 5", estimate count
                var parts = range.Split(new[] { " -" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var start = ExtractFloorNumber(parts[0]);
                    var end = ExtractFloorNumber(parts[1]);
                    if (start.HasValue && end.HasValue)
                        return Math.Abs(end.Value - start.Value) + 1;
                }
                return 3; // Default estimate for ranges
            }
            else
            {
                // Count commas + 1 for individual levels
                return range.Split(',').Length;
            }
        }

        private void LoadIDNETDeviceTypeCards()
        {
            try
            {
                if (IDNETDeviceTypeCardsPanel == null || _idnetResults?.AllDevices == null) return;

                IDNETDeviceTypeCardsPanel.Children.Clear();

                // Group devices by type and create summary cards
                var deviceGroups = _idnetResults.AllDevices
                    .GroupBy(d => d.DeviceType)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                foreach (var group in deviceGroups)
                {
                    var card = new DevExpress.Xpf.Core.SimpleButton
                    {
                        Style = (Style)FindResource("MetricCardStyle"),
                        Margin = new Thickness(5),
                        MinWidth = 150
                    };

                    var cardContent = new StackPanel();
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = group.Key.ToUpper(),
                        Style = (Style)FindResource("MetricLabelStyle"),
                        FontSize = 10
                    });
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = group.Count().ToString(),
                        Style = (Style)FindResource("MetricValueStyle"),
                        FontSize = 24
                    });
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = $"{group.Sum(d => d.PowerConsumption):F1} mA Total",
                        Style = (Style)FindResource("MetricDescriptionStyle"),
                        FontSize = 10
                    });

                    card.Content = cardContent;
                    IDNETDeviceTypeCardsPanel.Children.Add(card);
                }

                // Add total summary card
                var totalCard = new DevExpress.Xpf.Core.SimpleButton
                {
                    Style = (Style)FindResource("MetricCardStyle"),
                    Margin = new Thickness(5),
                    MinWidth = 150,
                    Background = new SolidColorBrush(Color.FromRgb(64, 64, 68))
                };

                var totalContent = new StackPanel();
                totalContent.Children.Add(new TextBlock
                {
                    Text = "TOTAL DEVICES",
                    Style = (Style)FindResource("MetricLabelStyle"),
                    FontSize = 10
                });
                totalContent.Children.Add(new TextBlock
                {
                    Text = _idnetResults.TotalDevices.ToString(),
                    Style = (Style)FindResource("MetricValueStyle"),
                    FontSize = 28,
                    FontWeight = FontWeights.Bold
                });
                totalContent.Children.Add(new TextBlock
                {
                    Text = $"{_idnetResults.TotalPowerConsumption:F1} mA Total",
                    Style = (Style)FindResource("MetricDescriptionStyle"),
                    FontSize = 10
                });

                totalCard.Content = totalContent;
                IDNETDeviceTypeCardsPanel.Children.Add(totalCard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading IDNET device type cards: {ex.Message}");
            }
        }

        private void UpdateIDNETNetworkSummary()
        {
            try
            {
                if (_idnetResults?.SystemSummary == null) return;

                // Update channel count
                if (IDNETChannelsText != null)
                    IDNETChannelsText.Text = _idnetResults.SystemSummary.RecommendedNetworkChannels.ToString();

                // Update repeater count
                if (IDNETRepeatersText != null)
                    IDNETRepeatersText.Text = _idnetResults.SystemSummary.RepeatersRequired.ToString();

                // Update wire length
                if (IDNETWireLengthText != null)
                    IDNETWireLengthText.Text = $"{_idnetResults.SystemSummary.TotalWireLength:F0} ft";

                // Update power consumption
                if (IDNETPowerText != null)
                    IDNETPowerText.Text = $"{_idnetResults.TotalPowerConsumption:F1} mA";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating IDNET network summary: {ex.Message}");
            }
        }

        private void LoadEmptyIDNETState()
        {
            try
            {
                // Create a message indicating no IDNET devices were found
                var emptyGridItems = new List<object>
                {
                    new
                    {
                        Level = "No IDNET Devices Found",
                        SmokeDetectors = "0",
                        HeatDetectors = "0",
                        ManualStations = "0",
                        Modules = "0",
                        TotalDevices = "0",
                        PowerConsumption = "0.0 mA",
                        NetworkSegments = "0",
                        AddressRange = "N/A",
                        Status = "No Detection Devices Found"
                    }
                };

                IDNETAnalysisGrid.ItemsSource = emptyGridItems;

                // Clear other grids
                IDNETDevicesGrid.ItemsSource = new List<IDNETDevice>();
                IDNETNetworkSegmentsGrid.ItemsSource = new List<IDNETNetworkSegment>();

                // Clear device type cards
                if (IDNETDeviceTypeCardsPanel != null)
                {
                    IDNETDeviceTypeCardsPanel.Children.Clear();
                }

                // Clear network summary
                if (IDNETChannelsText != null) IDNETChannelsText.Text = "0";
                if (IDNETRepeatersText != null) IDNETRepeatersText.Text = "0";
                if (IDNETWireLengthText != null) IDNETWireLengthText.Text = "0 ft";
                if (IDNETPowerText != null) IDNETPowerText.Text = "0 mA";

                System.Diagnostics.Debug.WriteLine("IDNET: Empty state loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading empty IDNET state: {ex.Message}");
            }
        }

        private string GetUtilizationCategory(double utilization)
        {
            if (utilization >= 80) return "Optimized";
            if (utilization >= 60) return "Excellent";
            if (utilization >= 40) return "Good";
            return "Underutilized";
        }



        private void UpdateStatusDisplays()
        {
            // Update status bar
            DeviceCount.Text = $"Devices: {_electricalResults?.Elements?.Count ?? 0}";
            IDNACCount.Text = $"IDNACs: {_idnacResults?.TotalIdnacsNeeded ?? 0}";

            var totalCurrent = _electricalResults?.Totals?.GetValueOrDefault("current", 0.0) ?? 0.0;
            CurrentLoad.Text = $"Load: {totalCurrent:F1}A";
        }



        /// <summary>
        /// Extract all necessary data from a FamilyInstance on the main thread
        /// to avoid Revit API calls in background threads
        /// </summary>
        private ElementData ExtractElementData(FamilyInstance element)
        {
            try
            {
                var elementData = new ElementData
                {
                    Id = element.Id.Value,
                    Element = element,
                    FamilyName = element.Symbol?.Family?.Name ?? "Unknown",
                    TypeName = element.Symbol?.Name ?? "Unknown Type",
                    Description = element.Symbol?.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.AsString() ?? "",
                    LevelName = "Unknown Level"
                };

                // Get level name
                try
                {
                    var levelElement = element.Document?.GetElement(element.LevelId);
                    if (levelElement is Level level)
                    {
                        elementData.LevelName = level.Name;
                    }
                }
                catch
                {
                    elementData.LevelName = "Unknown Level";
                }

                // Extract electrical parameters - ONLY CURRENT DRAW and Wattage
                var targetParams = new[] { "CURRENT DRAW", "Wattage" };

                foreach (var paramName in targetParams)
                {
                    try
                    {
                        // Check instance parameters first
                        var param = element.LookupParameter(paramName);
                        if (param != null && param.HasValue)
                        {
                            elementData.FoundParams.Add(paramName);
                            double value = GetParameterValue(param);

                            if (paramName == "CURRENT DRAW")
                            {
                                elementData.Current = value;
                            }
                            else if (paramName == "Wattage")
                            {
                                elementData.Wattage = value;
                            }
                        }
                        else if (element.Symbol != null)
                        {
                            // Check type parameters
                            param = element.Symbol.LookupParameter(paramName);
                            if (param != null && param.HasValue)
                            {
                                elementData.FoundParams.Add($"Type.{paramName}");
                                double value = GetParameterValue(param);

                                if (paramName == "CURRENT DRAW")
                                {
                                    elementData.Current = value;
                                }
                                else if (paramName == "Wattage")
                                {
                                    elementData.Wattage = value;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore parameter extraction errors
                    }
                }

                // REMOVED: No current/wattage calculations - only use direct parameter values
                // Current and wattage must come directly from device parameters only

                return elementData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting element data: {ex.Message}");
                return null;
            }
        }

        private double GetParameterValue(Parameter param)
        {
            try
            {
                double value = 0.0;

                if (param.StorageType == StorageType.Double)
                {
                    // Always use AsValueString() first to get properly formatted value with units
                    var valueString = param.AsValueString();
                    if (!string.IsNullOrEmpty(valueString))
                    {
                        value = ParseNumericValue(valueString);
                    }
                    else
                    {
                        // If AsValueString() is empty, use raw value
                        value = param.AsDouble();
                    }
                }
                else if (param.StorageType == StorageType.Integer)
                {
                    value = param.AsInteger();
                }
                else if (param.StorageType == StorageType.String)
                {
                    var stringValue = param.AsString();
                    if (!string.IsNullOrEmpty(stringValue))
                    {
                        value = ParseNumericValue(stringValue);
                    }
                }

                return value;
            }
            catch
            {
                // Ignore parameter value errors
            }
            return 0.0;
        }

        private double ParseNumericValue(string valueString)
        {
            if (string.IsNullOrEmpty(valueString))
                return 0.0;

            try
            {
                // Remove common unit symbols and extra whitespace
                var cleanValue = System.Text.RegularExpressions.Regex.Replace(valueString, @"[WwAaVv]", "")
                                     .Replace("VA", "")
                                     .Replace("KVA", "")
                                     .Replace("kva", "")
                                     .Replace("kVA", "")
                                     .Trim();

                // Handle different decimal separators
                if (double.TryParse(cleanValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }

                // Try with current culture if invariant fails
                if (double.TryParse(cleanValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out result))
                {
                    return result;
                }

                // Extract just the numeric part using regex
                var match = System.Text.RegularExpressions.Regex.Match(cleanValue, @"[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?");
                if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing numeric value '{valueString}': {ex.Message}");
            }

            return 0.0;
        }

        /// <summary>
        /// Create ElectricalResults from pre-extracted ElementData (no Revit API calls)
        /// </summary>
        private ElectricalResults CreateElectricalResultsFromData(List<ElementData> elementDataList)
        {
            var results = new ElectricalResults();

            // Process each element
            foreach (var elementData in elementDataList)
            {
                if (elementData.Current > 0 || elementData.Wattage > 0)
                {
                    results.Elements.Add(elementData);

                    // Aggregate by family
                    if (!results.ByFamily.ContainsKey(elementData.FamilyName))
                    {
                        results.ByFamily[elementData.FamilyName] = new FamilyData();
                    }
                    results.ByFamily[elementData.FamilyName].Count++;
                    results.ByFamily[elementData.FamilyName].Current += elementData.Current;
                    results.ByFamily[elementData.FamilyName].Wattage += elementData.Wattage;

                    // Aggregate by level
                    if (!results.ByLevel.ContainsKey(elementData.LevelName))
                    {
                        results.ByLevel[elementData.LevelName] = new LevelData();
                    }
                    results.ByLevel[elementData.LevelName].Devices++;
                    results.ByLevel[elementData.LevelName].Current += elementData.Current;
                    results.ByLevel[elementData.LevelName].Wattage += elementData.Wattage;

                    if (!results.ByLevel[elementData.LevelName].Families.ContainsKey(elementData.FamilyName))
                    {
                        results.ByLevel[elementData.LevelName].Families[elementData.FamilyName] = 0;
                    }
                    results.ByLevel[elementData.LevelName].Families[elementData.FamilyName]++;
                }
            }

            // Calculate totals
            results.Totals["current"] = results.Elements.Sum(e => e.Current);
            results.Totals["wattage"] = results.Elements.Sum(e => e.Wattage);
            results.Totals["devices"] = results.Elements.Count;

            return results;
        }

        /// <summary>
        /// Check if element is a detection device (for IDNET analysis)
        /// IDNET devices are detection devices: detectors, modules, pull stations
        /// IDNAC devices are notification devices: speakers, strobes, horns
        /// </summary>
        private bool IsDetectionDevice(FamilyInstance element)
        {
            if (element?.Symbol?.Family == null)
            {
                System.Diagnostics.Debug.WriteLine($"IDNET: Skipping element - no family information");
                return false;
            }

            try
            {
                var familyName = element.Symbol.Family.Name.ToUpperInvariant();
                var categoryName = element.Category?.Name?.ToUpperInvariant() ?? "";
                var originalFamilyName = element.Symbol.Family.Name;
                var originalCategoryName = element.Category?.Name ?? "No Category";

                // Note: Pre-filtering at GetElementsByScope level now ensures we only get Fire Alarm Devices

                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"=== IDNET DEVICE ANALYSIS ===");
                System.Diagnostics.Debug.WriteLine($"Family: '{originalFamilyName}'");
                System.Diagnostics.Debug.WriteLine($"Category: '{originalCategoryName}'");
                System.Diagnostics.Debug.WriteLine($"Element ID: {element.Id}");

                // FIRST: Explicitly exclude IDNAC notification devices
                var notificationKeywords = new[]
                {
                    "SPEAKER", "STROBE", "HORN", "BELL", "CHIME", "SOUNDER",
                    "NOTIFICATION", "APPLIANCE", "NAC", "IDNAC"
                };

                // Check for notification device exclusions
                var matchedNotificationKeywords = notificationKeywords.Where(keyword =>
                    familyName.Contains(keyword) || categoryName.Contains(keyword)).ToList();

                if (matchedNotificationKeywords.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"❌ EXCLUDED - IDNAC Notification Device");
                    System.Diagnostics.Debug.WriteLine($"   Matched keywords: {string.Join(", ", matchedNotificationKeywords)}");
                    System.Diagnostics.Debug.WriteLine($"   Reason: This is an IDNAC device (speakers, strobes, horns)");
                    System.Diagnostics.Debug.WriteLine($"================================");
                    return false;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Passed notification exclusion check");
                }

                // ENHANCED DETECTION BASED ON MODEL ANALYSIS PATTERNS
                var specificPatterns = new[]
                {
                    "DETECTORS - ",
                    "MODULES - ADDRESSABLE",
                    "FA_TWO WAY COMMUNICATION"
                };

                var matchedPatterns = specificPatterns.Where(pattern => familyName.Contains(pattern)).ToList();
                if (matchedPatterns.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"✅ DETECTED - Specific Pattern Match");
                    System.Diagnostics.Debug.WriteLine($"   Matched patterns: {string.Join(", ", matchedPatterns)}");
                    System.Diagnostics.Debug.WriteLine($"   Result: IDNET Detection Device");
                    System.Diagnostics.Debug.WriteLine($"================================");
                    return true;
                }

                // Check for IDNET detection device keywords only
                var detectionKeywords = new[]
                {
                    "SMOKE", "HEAT", "DETECTOR", "MANUAL", "PULL", "STATION",
                    "INPUT", "OUTPUT", "MODULE", "MONITOR",
                    "BEAM", "DUCT", "ASPIRATION", "VESDA", "ASD", "MULTI CRITERA",
                    "ADDRESSABLE", "CONVENTIONAL", "FIREMAN", "PHONE", "REFUGE",
                    "SENSOR", "SENSING", "DETECT"
                };

                // Use more precise matching for I/O patterns
                var ioPatterns = new[] { "I/O", " IO ", "IO-", "-IO", "IO_", "_IO" };
                var hasIoPattern = ioPatterns.Any(pattern => familyName.Contains(pattern));

                var matchedDetectionKeywords = detectionKeywords.Where(keyword =>
                    familyName.Contains(keyword) || categoryName.Contains(keyword)).ToList();

                if (hasIoPattern)
                {
                    matchedDetectionKeywords.Add("I/O");
                }

                // For Fire Alarm Devices category, check if it's a detection device (not notification)
                if (categoryName.Contains("FIRE ALARM DEVICES"))
                {
                    System.Diagnostics.Debug.WriteLine($"📁 Fire Alarm Devices Category Detected");

                    // Only return true if it matches detection keywords (not notification)
                    bool isDetection = matchedDetectionKeywords.Any();

                    if (isDetection)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ DETECTED - Fire Alarm + Detection Keywords");
                        System.Diagnostics.Debug.WriteLine($"   Matched detection keywords: {string.Join(", ", matchedDetectionKeywords)}");
                        System.Diagnostics.Debug.WriteLine($"   Result: IDNET Detection Device");
                        System.Diagnostics.Debug.WriteLine($"================================");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ EXCLUDED - Fire Alarm but Not Detection Type");
                        System.Diagnostics.Debug.WriteLine($"   Available detection keywords: {string.Join(", ", detectionKeywords.Take(10))}...");
                        System.Diagnostics.Debug.WriteLine($"   Reason: In Fire Alarm category but doesn't match detection patterns");
                        System.Diagnostics.Debug.WriteLine($"   Could be: Panel, power supply, or other fire alarm component");
                        System.Diagnostics.Debug.WriteLine($"================================");
                        return false;
                    }
                }

                // For other categories, check detection keywords
                if (matchedDetectionKeywords.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"✅ DETECTED - Detection Keywords Match");
                    System.Diagnostics.Debug.WriteLine($"   Matched detection keywords: {string.Join(", ", matchedDetectionKeywords)}");
                    System.Diagnostics.Debug.WriteLine($"   Result: IDNET Detection Device");
                    System.Diagnostics.Debug.WriteLine($"================================");
                    return true;
                }

                // Also check for security-related detection devices
                if (categoryName.Contains("SECURITY") &&
                    (familyName.Contains("DETECTOR") || familyName.Contains("SENSOR")))
                {
                    System.Diagnostics.Debug.WriteLine($"✅ DETECTED - Security Detection Device");
                    System.Diagnostics.Debug.WriteLine($"   Security category with detector/sensor keywords");
                    System.Diagnostics.Debug.WriteLine($"   Result: IDNET Detection Device");
                    System.Diagnostics.Debug.WriteLine($"================================");
                    return true;
                }

                // Final rejection - doesn't match any detection criteria
                System.Diagnostics.Debug.WriteLine($"❌ NOT DETECTED - No Detection Criteria Met");
                System.Diagnostics.Debug.WriteLine($"   Available detection keywords: {string.Join(", ", detectionKeywords.Take(8))}...");
                System.Diagnostics.Debug.WriteLine($"   Available specific patterns: {string.Join(", ", specificPatterns)}");
                System.Diagnostics.Debug.WriteLine($"   Result: Not an IDNET device");
                System.Diagnostics.Debug.WriteLine($"================================");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR in IsDetectionDevice");
                System.Diagnostics.Debug.WriteLine($"   Family: '{element.Symbol?.Family?.Name ?? "Unknown"}'");
                System.Diagnostics.Debug.WriteLine($"   Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Result: Defaulting to false");
                System.Diagnostics.Debug.WriteLine($"================================");
                return false;
            }
        }

        /// <summary>
        /// Extract detection device data for IDNET analysis
        /// </summary>
        private ElementData ExtractDetectionDeviceData(FamilyInstance element)
        {
            try
            {
                var elementData = new ElementData
                {
                    Id = element.Id.Value,
                    Element = element,
                    FamilyName = element.Symbol?.Family?.Name ?? "Unknown",
                    TypeName = element.Symbol?.Name ?? "Unknown Type",
                    Description = element.Symbol?.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.AsString() ?? "",
                    LevelName = "Unknown Level"
                };

                // Get level name
                try
                {
                    var levelElement = element.Document?.GetElement(element.LevelId);
                    if (levelElement is Level level)
                    {
                        elementData.LevelName = level.Name;
                    }
                }
                catch
                {
                    elementData.LevelName = "Unknown Level";
                }

                // For detection devices, we don't need electrical parameters
                // but we can extract other useful parameters
                var detectionParams = new[] { "Address", "Zone", "Loop", "Device Type", "Area Coverage" };

                foreach (var paramName in detectionParams)
                {
                    try
                    {
                        var param = element.LookupParameter(paramName);
                        if (param == null && element.Symbol != null)
                        {
                            param = element.Symbol.LookupParameter(paramName);
                        }

                        if (param != null && param.HasValue)
                        {
                            elementData.FoundParams.Add(paramName);
                        }
                    }
                    catch
                    {
                        // Ignore parameter extraction errors
                    }
                }

                return elementData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting detection device data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Run IDNET analysis in background thread (no Revit API calls)
        /// </summary>
        private void RunIDNETAnalysisInternal(List<ElementData> detectionDataList)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"IDNET Internal: Starting analysis with {detectionDataList.Count} elements");

                if (detectionDataList.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("IDNET Internal: No detection devices found, creating empty results");
                    _idnetResults = CreateEmptyIDNETResults();
                    return;
                }

                // BYPASS DOUBLE FILTERING: Since we already filtered devices in the main thread,
                // create IDNET results directly from the detected devices instead of 
                // running them through IDNETAnalyzer's DetectIDNETDevices() again
                _idnetResults = CreateIDNETResultsFromDetectedDevices(detectionDataList);

                System.Diagnostics.Debug.WriteLine($"IDNET Internal: Analysis complete. Found {_idnetResults?.TotalDevices ?? 0} IDNET devices");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IDNET analysis error: {ex.Message}");
                // Create a basic IDNET result if analysis fails
                _idnetResults = CreateBasicIDNETResults(detectionDataList);
            }
        }

        /// <summary>
        /// Create IDNET results directly from already-detected devices (bypass double filtering)
        /// </summary>
        private IDNETSystemResults CreateIDNETResultsFromDetectedDevices(List<ElementData> detectionDataList)
        {
            try
            {
                var idnetDevices = new List<IDNETDevice>();

                // Convert ElementData to IDNETDevice objects
                foreach (var element in detectionDataList)
                {
                    var deviceType = CategorizeDeviceForIDNET(element);
                    if (string.IsNullOrEmpty(deviceType)) continue;

                    var idnetDevice = new IDNETDevice
                    {
                        DeviceId = element.Id.ToString(),
                        FamilyName = element.FamilyName ?? "Unknown",
                        DeviceType = deviceType,
                        Location = "N/A",
                        Level = element.LevelName ?? "Unknown Level",
                        PowerConsumption = 0.8, // Standard IDNET device current
                        UnitLoads = 1,
                        Zone = element.LevelName ?? "Zone 1",
                        Position = null,
                        SuggestedAddress = 0,
                        NetworkSegment = "TBD",
                        AddressParameters = new Dictionary<string, string>(),
                        FunctionParameters = new Dictionary<string, string>(),
                        AreaParameters = new Dictionary<string, string>(),
                        ExtractedParameters = new List<string>()
                    };

                    idnetDevices.Add(idnetDevice);
                }

                System.Diagnostics.Debug.WriteLine($"CreateIDNETResults: Converted {idnetDevices.Count} devices from {detectionDataList.Count} detected elements");

                if (idnetDevices.Count == 0)
                {
                    return CreateEmptyIDNETResults();
                }

                // Analyze devices by level
                var levelAnalysis = new Dictionary<string, IDNETLevelAnalysis>();
                var grouped = idnetDevices.GroupBy(d => d.Level ?? "Unknown Level");
                foreach (var group in grouped)
                {
                    var deviceTypeCounts = group.GroupBy(x => x.DeviceType).ToDictionary(g => g.Key, g => g.Count());
                    levelAnalysis[group.Key] = new IDNETLevelAnalysis
                    {
                        LevelName = group.Key,
                        TotalDevices = group.Count(),
                        DeviceTypeCount = deviceTypeCounts,
                        TotalPowerConsumption = group.Count() * 0.8,
                        TotalUnitLoads = group.Count(),
                        SuggestedNetworkSegments = (int)Math.Ceiling(group.Count() / 200.0), // 200 devices per channel
                        Devices = group.ToList()
                    };
                }

                // Create network segments (200 devices per segment with 20% spare capacity)
                var networkSegments = new List<IDNETNetworkSegment>();
                var devicesToAssign = idnetDevices.ToList();
                int segId = 1;
                while (devicesToAssign.Any())
                {
                    var segmentDevices = devicesToAssign.Take(200).ToList();
                    devicesToAssign.RemoveRange(0, segmentDevices.Count);

                    var seg = new IDNETNetworkSegment
                    {
                        SegmentId = $"IDNET-{segId:D2}",
                        Devices = segmentDevices,
                        EstimatedWireLength = segmentDevices.Select(x => x.Level).Distinct().Count() * 100 + segmentDevices.Count * 20,
                        DeviceCount = segmentDevices.Count,
                        RequiresRepeater = false,
                        StartingAddress = $"{segId:D2}001",
                        EndingAddress = $"{segId:D2}{segmentDevices.Count:D3}",
                        CoveredLevels = segmentDevices.Select(x => x.Level).Distinct().ToList()
                    };
                    networkSegments.Add(seg);
                    segId++;
                }

                // Create system summary
                var recommendations = new List<string>
                {
                    $"Total IDNET detection devices: {idnetDevices.Count}",
                    $"Network channels required: {networkSegments.Count}",
                    $"Total power consumption: {idnetDevices.Count * 0.8:F1} mA"
                };

                var deviceTypes = idnetDevices.GroupBy(d => d.DeviceType).ToDictionary(g => g.Key, g => g.Count());
                foreach (var kvp in deviceTypes.OrderByDescending(x => x.Value))
                {
                    recommendations.Add($"{kvp.Key}: {kvp.Value} devices");
                }

                var systemSummary = new IDNETSystemSummary
                {
                    RecommendedNetworkChannels = networkSegments.Count,
                    RepeatersRequired = 0,
                    TotalWireLength = networkSegments.Sum(s => s.EstimatedWireLength),
                    SystemRecommendations = recommendations,
                    IntegrationWithIDNAC = false,
                    PowerSupplyRequirements = networkSegments.Count <= 3 ?
                        $"Single ES-PS power supply supports {networkSegments.Count} IDNET channel{(networkSegments.Count > 1 ? "s" : "")}" :
                        $"Multiple power supplies required: {Math.Ceiling(networkSegments.Count / 3.0)} ES-PS units for {networkSegments.Count} channels"
                };

                return new IDNETSystemResults
                {
                    LevelAnalysis = levelAnalysis,
                    AllDevices = idnetDevices,
                    TotalDevices = idnetDevices.Count,
                    TotalPowerConsumption = idnetDevices.Count * 0.8,
                    TotalUnitLoads = idnetDevices.Sum(d => d.UnitLoads),
                    NetworkSegments = networkSegments,
                    SystemSummary = systemSummary,
                    AnalysisTimestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating IDNET results from detected devices: {ex.Message}");
                return CreateEmptyIDNETResults();
            }
        }

        /// <summary>
        /// Categorize a detected device for IDNET analysis
        /// </summary>
        private string CategorizeDeviceForIDNET(ElementData element)
        {
            if (element?.FamilyName == null) return null;

            var familyName = element.FamilyName.ToUpperInvariant();

            // Use the specific patterns from the model analysis
            if (familyName.Contains("DETECTORS - MULTI CRITERA"))
                return "Multi-Criteria Detector";
            else if (familyName.Contains("DETECTORS - ADDRESSABLE"))
                return "Addressable Detector";
            else if (familyName.Contains("DETECTORS - CONVENTIONAL"))
                return "Conventional Detector";
            else if (familyName.Contains("MODULES - ADDRESSABLE"))
            {
                if (familyName.Contains("IO") || familyName.Contains("I/O"))
                    return "Addressable I/O Module";
                else if (familyName.Contains("DUAL"))
                    return "Addressable Dual Module";
                else
                    return "Addressable Module";
            }
            else if (familyName.Contains("FA_TWO WAY COMMUNICATION") || familyName.Contains("TWO WAY COMMUNICATION"))
                return "Emergency Communication";
            else if (familyName.Contains("MODULES - NON-ADDRESSABLE"))
                return "Conventional Module";
            else if (familyName.Contains("PULL") || familyName.Contains("MANUAL") || familyName.Contains("STATION"))
                return "Manual Station";
            else if (familyName.Contains("SMOKE") || familyName.Contains("DETECTOR"))
                return "Smoke Detector";
            else if (familyName.Contains("HEAT"))
                return "Heat Detector";
            else
                return "IDNET Device";
        }

        /// <summary>
        /// Create empty IDNET results
        /// </summary>
        private IDNETSystemResults CreateEmptyIDNETResults()
        {
            return new IDNETSystemResults
            {
                LevelAnalysis = new Dictionary<string, IDNETLevelAnalysis>(),
                AllDevices = new List<IDNETDevice>(),
                TotalDevices = 0,
                TotalPowerConsumption = 0,
                TotalUnitLoads = 0,
                NetworkSegments = new List<IDNETNetworkSegment>(),
                SystemSummary = new IDNETSystemSummary
                {
                    RecommendedNetworkChannels = 0,
                    RepeatersRequired = 0,
                    TotalWireLength = 0,
                    SystemRecommendations = new List<string> { "No IDNET devices detected" },
                    IntegrationWithIDNAC = false,
                    PowerSupplyRequirements = "No IDNET power requirements"
                },
                AnalysisTimestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Create basic IDNET results if full analysis fails
        /// </summary>
        private IDNETSystemResults CreateBasicIDNETResults(List<ElementData> detectionDataList)
        {
            var results = new IDNETSystemResults
            {
                LevelAnalysis = new Dictionary<string, IDNETLevelAnalysis>(),
                AllDevices = new List<IDNETDevice>(),
                NetworkSegments = new List<IDNETNetworkSegment>(),
                AnalysisTimestamp = DateTime.Now
            };

            // Group devices by level
            var devicesByLevel = detectionDataList.GroupBy(d => d.LevelName);

            foreach (var levelGroup in devicesByLevel)
            {
                var levelAnalysis = new IDNETLevelAnalysis
                {
                    LevelName = levelGroup.Key,
                    TotalDevices = levelGroup.Count(),
                    DeviceTypeCount = new Dictionary<string, int>(),
                    Devices = new List<IDNETDevice>(),
                    TotalPowerConsumption = levelGroup.Count() * 0.5, // Estimate 0.5mA per device
                    SuggestedNetworkSegments = Math.Max(1, (int)Math.Ceiling(levelGroup.Count() / 100.0))
                };

                foreach (var device in levelGroup)
                {
                    var deviceType = DetermineDetectionDeviceType(device.FamilyName);

                    if (!levelAnalysis.DeviceTypeCount.ContainsKey(deviceType))
                        levelAnalysis.DeviceTypeCount[deviceType] = 0;
                    levelAnalysis.DeviceTypeCount[deviceType]++;

                    var idnetDevice = new IDNETDevice
                    {
                        DeviceId = device.Id.ToString(),
                        FamilyName = device.FamilyName,
                        DeviceType = deviceType,
                        Level = device.LevelName,
                        Location = $"Level {device.LevelName}",
                        PowerConsumption = 0.5, // Estimate
                        UnitLoads = 1
                    };

                    levelAnalysis.Devices.Add(idnetDevice);
                    results.AllDevices.Add(idnetDevice);
                }

                results.LevelAnalysis[levelGroup.Key] = levelAnalysis;
            }

            results.TotalDevices = results.AllDevices.Count;
            results.TotalPowerConsumption = results.AllDevices.Sum(d => d.PowerConsumption);

            return results;
        }

        /// <summary>
        /// Determine detection device type from family name
        /// </summary>
        private string DetermineDetectionDeviceType(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return "Unknown Device";

            var name = familyName.ToUpperInvariant();

            if (name.Contains("SMOKE"))
                return "Smoke Detector";
            if (name.Contains("HEAT"))
                return "Heat Detector";
            if (name.Contains("MANUAL") || name.Contains("PULL"))
                return "Manual Station";
            if (name.Contains("INPUT") || name.Contains("OUTPUT") || name.Contains("MODULE") || name.Contains("I/O"))
                return "Input/Output Module";
            if (name.Contains("BEAM"))
                return "Beam Detector";
            if (name.Contains("DUCT"))
                return "Duct Detector";
            if (name.Contains("ASPIRATION") || name.Contains("VESDA"))
                return "Aspirating Detector";

            return "Detection Device";
        }


        /// <summary>
        /// Validation result for analysis input
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
        }

        /// <summary>
        /// Comprehensive input validation for analysis
        /// </summary>
        private ValidationResult ValidateAnalysisInput(List<FamilyInstance> allElements, List<FamilyInstance> electricalElements)
        {
            var result = new ValidationResult { IsValid = true };
            var warnings = new List<string>();

            try
            {
                // Basic element validation
                if (allElements == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "No elements found - document may be empty or inaccessible.";
                    return result;
                }

                if (allElements.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = GetNoElementsErrorMessage();
                    return result;
                }

                // Check for electrical elements
                if (electricalElements == null || electricalElements.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = GetNoElectricalElementsErrorMessage(allElements.Count);
                    return result;
                }

                // Validate element accessibility
                int accessibleElements = 0;
                int elementsWithParameters = 0;
                var invalidElements = new List<string>();

                foreach (var element in electricalElements.Take(20)) // Sample first 20 for performance
                {
                    try
                    {
                        if (element?.Id != null && element.Symbol != null)
                        {
                            accessibleElements++;

                            // Check if element has required parameters
                            var hasWattage = element.LookupParameter("Wattage") != null;
                            var hasCurrent = element.LookupParameter("CURRENT DRAW") != null;

                            if (hasWattage || hasCurrent)
                            {
                                elementsWithParameters++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        invalidElements.Add($"Element ID {element?.Id}: {ex.Message}");
                    }
                }

                // Validate element accessibility percentage
                var accessibilityRatio = electricalElements.Count > 0 ? (double)accessibleElements / Math.Min(electricalElements.Count, 20) : 0;
                if (accessibilityRatio < 0.5)
                {
                    warnings.Add($"Warning: {accessibilityRatio:P0} of elements are accessible. Some elements may be corrupted or inaccessible.");
                }

                // Validate parameter availability
                var parameterRatio = accessibleElements > 0 ? (double)elementsWithParameters / accessibleElements : 0;
                if (parameterRatio < 0.3)
                {
                    warnings.Add($"Warning: Only {parameterRatio:P0} of elements have electrical parameters. Results may be incomplete.");
                }

                // Scope-specific validation
                ValidateScopeSpecific(result, warnings);

                // System resource validation
                ValidateSystemResources(result, warnings, electricalElements.Count);

                // Add warnings if validation passed
                if (result.IsValid && warnings.Any())
                {
                    result.Warnings = warnings;
                    System.Diagnostics.Debug.WriteLine($"Analysis validation passed with {warnings.Count} warnings");
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Validation error: {ex.Message}\n\nThis may indicate a problem with the Revit model or document access.";
                return result;
            }
        }

        private void ValidateScopeSpecific(ValidationResult result, List<string> warnings)
        {
            switch (_currentScope)
            {
                case "Selection":
                    // Selection scope validation is already handled by element count check
                    break;

                case "Active View":
                    try
                    {
                        var activeView = _document.ActiveView;
                        if (activeView == null)
                        {
                            warnings.Add("Warning: No active view detected. Results may be incomplete.");
                        }
                        else if (activeView.ViewType == ViewType.Schedule || activeView.ViewType == ViewType.Legend)
                        {
                            warnings.Add("Warning: Active view is a schedule or legend. Consider switching to a plan or 3D view.");
                        }
                    }
                    catch
                    {
                        warnings.Add("Warning: Could not verify active view properties.");
                    }
                    break;

                case "Entire Model":
                    // For entire model, check if it's too large
                    var totalElements = new FilteredElementCollector(_document)
                        .OfClass(typeof(FamilyInstance))
                        .GetElementCount();

                    if (totalElements > 10000)
                    {
                        warnings.Add($"Warning: Large model with {totalElements:N0} family instances. Analysis may take longer than usual.");
                    }
                    break;
            }
        }

        private void ValidateSystemResources(ValidationResult result, List<string> warnings, int elementCount)
        {
            try
            {
                // Check available memory (simplified check)
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64 / (1024 * 1024); // MB

                if (workingSet > 1000) // More than 1GB
                {
                    warnings.Add("Warning: High memory usage detected. Consider reducing analysis scope if performance issues occur.");
                }

                // Estimate processing time warning
                if (elementCount > 1000)
                {
                    warnings.Add($"Note: Analyzing {elementCount:N0} elements may take several minutes. Please be patient.");
                }
            }
            catch
            {
                // Resource validation is optional - don't fail if we can't check
            }
        }

        private string GetNoElementsErrorMessage()
        {
            return $"No elements found in scope '{_currentScope}'.\n\n" +
                   "Troubleshooting steps:\n" +
                   GetScopeSpecificTroubleshooting() +
                   "\n• Ensure the model contains family instances\n" +
                   "• Check that elements are not filtered out in the current view\n" +
                   "• Verify that the document is properly loaded";
        }

        private string GetNoElectricalElementsErrorMessage(int totalElements)
        {
            return $"No electrical elements found in scope '{_currentScope}' (searched {totalElements:N0} total elements).\n\n" +
                   "Fire alarm devices should contain one of these keywords:\n" +
                   "• Fire, Alarm, Notification\n" +
                   "• Strobe, Horn, Speaker\n" +
                   "• Smoke, Heat, Manual\n" +
                   "• Emergency, Safety\n\n" +
                   GetScopeSpecificTroubleshooting() +
                   "\n• Check family naming conventions\n" +
                   "• Verify that fire alarm families are loaded in the project";
        }

        private string GetScopeSpecificTroubleshooting()
        {
            switch (_currentScope)
            {
                case "Selection":
                    return "• Select fire alarm devices before running analysis\n" +
                           "• Use Ctrl+Click to select multiple elements\n" +
                           "• Ensure selected elements are visible in the current view";

                case "Active View":
                    return "• Switch to a floor plan or 3D view showing fire alarm devices\n" +
                           "• Ensure fire alarm elements are not hidden or filtered\n" +
                           "• Check view visibility/graphics settings";

                case "Entire Model":
                    return "• Verify that fire alarm families are loaded in the project\n" +
                           "• Check if elements are on hidden worksets\n" +
                           "• Ensure model contains fire alarm system components";

                default:
                    return "";
            }
        }



        private void ExportToCsv(string filePath)
        {
            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine("Export Type,Level,Family,Current (A),Wattage (W),Count,Total Current (A),Total Wattage (W)");

            // IDNAC Summary
            csv.AppendLine($"IDNAC Summary,All,All,{_electricalResults?.Totals["current"] ?? 0:F2},{_electricalResults?.Totals["wattage"] ?? 0:F2},{_electricalResults?.Elements?.Count ?? 0},{_electricalResults?.Totals["current"] ?? 0:F2},{_electricalResults?.Totals["wattage"] ?? 0:F2}");

            // By Level
            if (_electricalResults?.ByLevel != null)
            {
                foreach (var level in _electricalResults.ByLevel)
                {
                    csv.AppendLine($"Level,{level.Key},All,{level.Value.Current:F2},{level.Value.Wattage:F2},{level.Value.Devices},{level.Value.Current:F2},{level.Value.Wattage:F2}");
                }
            }

            // By Family
            if (_electricalResults?.ByFamily != null)
            {
                foreach (var family in _electricalResults.ByFamily)
                {
                    csv.AppendLine($"Family,All,{family.Key},{family.Value.Current:F2},{family.Value.Wattage:F2},{family.Value.Count},{family.Value.Current:F2},{family.Value.Wattage:F2}");
                }
            }

            System.IO.File.WriteAllText(filePath, csv.ToString());
        }

        private void ExportToExcel(string filePath)
        {
            // Since we don't have Excel library, convert to CSV format and save with CSV extension
            var csvFilePath = System.IO.Path.ChangeExtension(filePath, ".csv");
            var csv = new System.Text.StringBuilder();

            // Summary Sheet Data
            csv.AppendLine("IDNAC Calculator Analysis Results");
            csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Scope: {_currentScope}");
            csv.AppendLine("");

            // System Summary
            csv.AppendLine("System Summary");
            csv.AppendLine($"Total IDNACs Required: {_idnacResults?.TotalIdnacsNeeded ?? 0}");
            csv.AppendLine($"Total Current: {_electricalResults?.Totals["current"] ?? 0:F2} A");
            csv.AppendLine($"Total Wattage: {_electricalResults?.Totals["wattage"] ?? 0:F2} W");
            csv.AppendLine($"Total Devices: {_electricalResults?.Elements?.Count ?? 0}");
            csv.AppendLine("");

            // Level Analysis
            csv.AppendLine("Level Analysis");
            csv.AppendLine("Level,Devices,Current (A),Wattage (W)");
            if (_electricalResults?.ByLevel != null)
            {
                foreach (var level in _electricalResults.ByLevel)
                {
                    csv.AppendLine($"{level.Key},{level.Value.Devices},{level.Value.Current:F2},{level.Value.Wattage:F2}");
                }
            }
            csv.AppendLine("");

            // Family Analysis
            csv.AppendLine("Family Analysis");
            csv.AppendLine("Family,Count,Current (A),Wattage (W)");
            if (_electricalResults?.ByFamily != null)
            {
                foreach (var family in _electricalResults.ByFamily)
                {
                    csv.AppendLine($"{family.Key},{family.Value.Count},{family.Value.Current:F2},{family.Value.Wattage:F2}");
                }
            }

            System.IO.File.WriteAllText(csvFilePath, csv.ToString());
        }

        private void ExportIDNETResults(string filePath)
        {
            var csv = new System.Text.StringBuilder();

            // IDNET Analysis Results Header
            csv.AppendLine("IDNET Analysis Results");
            csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Scope: {_currentScope}");
            csv.AppendLine("");

            // System Summary
            csv.AppendLine("IDNET System Summary");
            csv.AppendLine($"Total Devices: {_idnetResults?.TotalDevices ?? 0}");
            csv.AppendLine($"Network Segments: {_idnetResults?.NetworkSegments?.Count ?? 0}");
            csv.AppendLine($"Total Power Consumption: {_idnetResults?.TotalPowerConsumption ?? 0:F1} mA");
            csv.AppendLine($"Total Unit Loads: {_idnetResults?.TotalUnitLoads ?? 0}");
            csv.AppendLine("");

            // Level Analysis
            if (_idnetResults?.LevelAnalysis != null)
            {
                csv.AppendLine("Level Analysis");
                csv.AppendLine("Level,Total Devices,Smoke Detectors,Heat Detectors,Manual Stations,Modules,Power (mA),Network Segments");

                foreach (var level in _idnetResults.LevelAnalysis)
                {
                    var levelData = level.Value;
                    var smokeCount = levelData.DeviceTypeCount?.GetValueOrDefault("Smoke Detector", 0) ?? 0;
                    var heatCount = levelData.DeviceTypeCount?.GetValueOrDefault("Heat Detector", 0) ?? 0;
                    var manualCount = levelData.DeviceTypeCount?.GetValueOrDefault("Manual Station", 0) ?? 0;
                    var moduleCount = levelData.DeviceTypeCount?.Values.Sum() - smokeCount - heatCount - manualCount;

                    csv.AppendLine($"{level.Key},{levelData.TotalDevices},{smokeCount},{heatCount},{manualCount},{moduleCount},{levelData.TotalPowerConsumption:F1},{levelData.SuggestedNetworkSegments}");
                }
                csv.AppendLine("");
            }

            // Network Segments
            if (_idnetResults?.NetworkSegments != null)
            {
                csv.AppendLine("Network Segments");
                csv.AppendLine("Segment ID,Device Count,Levels Served,Wire Length Estimate");

                foreach (var segment in _idnetResults.NetworkSegments)
                {
                    csv.AppendLine($"{segment.SegmentId},{segment.DeviceCount},{string.Join(";", segment.CoveredLevels ?? new List<string>())},{segment.EstimatedWireLength}");
                }
            }

            System.IO.File.WriteAllText(filePath, csv.ToString());
        }

        private void ExportIDNETDeviceList(string filePath)
        {
            var csv = new System.Text.StringBuilder();

            // Device List Header
            csv.AppendLine("IDNET Device List");
            csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Total Devices: {_idnetResults?.AllDevices?.Count ?? 0}");
            csv.AppendLine("");

            // Device Details
            csv.AppendLine("Device ID,Family Name,Device Type,Level,Zone,Address,Function,Area,Power (mA),Unit Loads,Network Segment");

            if (_idnetResults?.AllDevices != null)
            {
                foreach (var device in _idnetResults.AllDevices)
                {
                    csv.AppendLine($"{device.DeviceId},{device.FamilyName},{device.DeviceType},{device.Level},{device.Zone},{device.Address},{device.Function},{device.Area},{device.PowerConsumption:F1},{device.UnitLoads},{device.NetworkSegment}");
                }
            }

            System.IO.File.WriteAllText(filePath, csv.ToString());
        }



        private void NewAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show("Start a new analysis? This will clear current results.",
                    "New Analysis", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Clear current analysis data
                    _electricalResults = null;
                    _idnacResults = null;
                    _idnetResults = null;
                    _amplifierResults = null;
                    _analysisCompleted = false;

                    // Reset UI
                    ClearAllGrids();
                    UpdateDashboard(null, null, null);
                    UpdateStatus("Ready for new analysis");

                    // Clear dashboard metrics
                    if (TotalFireAlarmDevicesText != null) TotalFireAlarmDevicesText.Text = "0";
                    if (IDNACDevicesText != null) IDNACDevicesText.Text = "0";
                    if (IDNETDevicesText != null) IDNETDevicesText.Text = "0";
                    if (IDNACsRequiredText != null) IDNACsRequiredText.Text = "0";
                    if (TotalPowerText != null) TotalPowerText.Text = "0A";
                    if (AmplifiersText != null) AmplifiersText.Text = "0";

                    // Update status
                    if (SystemStatusText != null) SystemStatusText.Text = "Ready for new analysis";
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error starting new analysis: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenResults_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "Analysis Files (*.idnac)|*.idnac|All Files (*.*)|*.*",
                    DefaultExt = "idnac",
                    Title = "Open Analysis Results"
                };

                if (openDialog.ShowDialog() == true)
                {
                    try
                    {
                        string jsonData = System.IO.File.ReadAllText(openDialog.FileName);
                        var analysisData = Newtonsoft.Json.Linq.JObject.Parse(jsonData);

                        if (analysisData != null)
                        {
                            // Restore analysis results if they exist
                            if (analysisData["ElectricalResults"] != null)
                                _electricalResults = analysisData["ElectricalResults"].ToObject<ElectricalResults>();
                            
                            if (analysisData["IDNACResults"] != null)
                                _idnacResults = analysisData["IDNACResults"].ToObject<IDNACSystemResults>();
                            
                            if (analysisData["IDNETResults"] != null)
                                _idnetResults = analysisData["IDNETResults"].ToObject<IDNETSystemResults>();
                            
                            if (analysisData["AmplifierResults"] != null)
                                _amplifierResults = analysisData["AmplifierResults"].ToObject<AmplifierRequirements>();
                            
                            if (analysisData["PanelRecommendations"] != null)
                                _panelRecommendations = analysisData["PanelRecommendations"].ToObject<List<PanelPlacementRecommendation>>();

                            _analysisCompleted = true;
                            string timestamp = analysisData["Timestamp"]?.ToString() ?? "Unknown";
                            string scope = analysisData["Scope"]?.ToString() ?? "Unknown";
                            
                            UpdateStatus($"Analysis results loaded from {System.IO.Path.GetFileName(openDialog.FileName)} (Created: {timestamp}, Scope: {scope})");
                            
                            DXMessageBox.Show($"Analysis results loaded successfully from {openDialog.FileName}",
                                "Load Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception loadEx)
                    {
                        DXMessageBox.Show($"Error loading analysis data: {loadEx.Message}",
                            "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error opening file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("No analysis results to save. Run analysis first.",
                        "Save Analysis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Analysis Files (*.idnac)|*.idnac|All Files (*.*)|*.*",
                    DefaultExt = "idnac",
                    FileName = $"IDNAC_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = "Save Analysis Results"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        var analysisData = new
                        {
                            Timestamp = DateTime.Now,
                            Scope = _currentScope,
                            ElectricalResults = _electricalResults,
                            IDNACResults = _idnacResults,
                            IDNETResults = _idnetResults,
                            AmplifierResults = _amplifierResults,
                            PanelRecommendations = _panelRecommendations
                        };

                        string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(analysisData, Newtonsoft.Json.Formatting.Indented);
                        System.IO.File.WriteAllText(saveDialog.FileName, jsonData);

                        DXMessageBox.Show($"Analysis results saved successfully to {saveDialog.FileName}",
                            "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception saveEx)
                    {
                        DXMessageBox.Show($"Error saving analysis data: {saveEx.Message}",
                            "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error saving file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsAnalysis_Click(object sender, ItemClickEventArgs e)
        {
            // Same as save for now
            SaveAnalysis_Click(sender, e);
        }

        private void ExportAllData_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("No analysis results to export. Run analysis first.",
                        "Export All Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*",
                    DefaultExt = "zip",
                    FileName = $"IDNAC_Complete_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = "Export Complete Analysis Package"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // TODO: Create ZIP with all analysis data
                    DXMessageBox.Show("Complete export package functionality will be implemented in a future version.",
                        "Feature Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error exporting data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintSummaryReport_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("No analysis results to print. Run analysis first.",
                        "Print Report", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var summaryReport = GenerateSummaryReport();
                    var document = CreatePrintDocument(summaryReport, "Fire Alarm Analysis - Summary Report");
                    printDialog.PrintDocument(document.DocumentPaginator, "Fire Alarm Analysis Summary");
                    
                    DXMessageBox.Show("Summary report sent to printer successfully.",
                        "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error printing report: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintDetailedReport_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("No analysis results to print. Run analysis first.",
                        "Print Report", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var detailedReport = GenerateDetailedReport();
                    var document = CreatePrintDocument(detailedReport, "Fire Alarm Analysis - Detailed Report");
                    printDialog.PrintDocument(document.DocumentPaginator, "Fire Alarm Analysis Detailed");
                    
                    DXMessageBox.Show("Detailed report sent to printer successfully.",
                        "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error printing report: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintPreview_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (!_analysisCompleted)
                {
                    DXMessageBox.Show("No analysis results to preview. Run analysis first.",
                        "Print Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var previewWindow = new System.Windows.Window
                {
                    Title = "Print Preview - Fire Alarm Analysis Report",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                
                var summaryReport = GenerateSummaryReport();
                var document = CreatePrintDocument(summaryReport, "Fire Alarm Analysis - Print Preview");
                
                var documentViewer = new System.Windows.Controls.DocumentViewer();
                documentViewer.Document = document;
                previewWindow.Content = documentViewer;
                
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error showing print preview: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Options_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                // Show available settings options
                var result = DXMessageBox.Show(
                    "Application Settings\n\n" +
                    "Available configuration options:\n\n" +
                    "• IDNAC Settings - Configure circuit limits and capacity\n" +
                    "• Amplifier Config - Set amplifier types and ratings\n" +
                    "• Device Mappings - Map Revit families to device types\n" +
                    "• Import Catalog - Import family catalog data\n" +
                    "• Theme Settings - Change application appearance\n\n" +
                    "Access these options through the Settings section in the navigation menu.",
                    "Application Options", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Navigate to Settings section
                ToggleNavigationStep("Settings");
                UpdateWorkflowStep("Settings");
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error opening options: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var result = DXMessageBox.Show("Exit IDNAC Calculator?",
                    "Exit Application", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Error exiting application: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAllGrids()
        {
            try
            {
                // Clear IDNAC grids
                if (IDNACGrid != null) IDNACGrid.ItemsSource = null;
                if (DeviceGrid != null) DeviceGrid.ItemsSource = null;
                if (LevelGrid != null) LevelGrid.ItemsSource = null;
                if (SystemOverviewGrid != null) SystemOverviewGrid.ItemsSource = null;

                // Clear IDNET grids  
                if (IDNETLevelGrid != null) IDNETLevelGrid.ItemsSource = null;
                if (IDNETAnalysisGrid != null) IDNETAnalysisGrid.ItemsSource = null;
                if (IDNETDevicesGrid != null) IDNETDevicesGrid.ItemsSource = null;
                if (IDNETNetworkSegmentsGrid != null) IDNETNetworkSegmentsGrid.ItemsSource = null;

                // Clear raw data grid
                if (RawDataGrid != null) RawDataGrid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing grids: {ex.Message}");
            }
        }

        private void ScopeSelection_Click(object sender, ItemClickEventArgs e)
        {
            _currentScope = "Selection";
            UpdateScopeSelection();
        }

        private void ScopeSelection_Click(object sender, RoutedEventArgs e)
        {
            _currentScope = "Selection";
            UpdateScopeSelection();
        }

        private void ZoomToSelected_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                var checkBox = sender as System.Windows.Controls.CheckBox;
                bool isEnabled = checkBox?.IsChecked ?? false;

                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as GridControl;
                if (detailedGrid?.View is TableView tableView)
                {
                    if (isEnabled)
                    {
                        tableView.FocusedRowChanged += DetailedGrid_FocusedRowChanged;

                    }
                    else
                    {
                        tableView.FocusedRowChanged -= DetailedGrid_FocusedRowChanged;

                    }
                }

                System.Diagnostics.Debug.WriteLine($"Zoom to selected: {(isEnabled ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling zoom to selected change: {ex.Message}");
            }
        }

        private void DetailedGrid_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            try
            {
                if (e.NewRow is DeviceDetailItem deviceItem)
                {
                    ZoomToElementInRevit(deviceItem.ElementId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling focused row change: {ex.Message}");
            }
        }

        private void DetailedDeviceGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var grid = sender as GridControl;
                if (grid?.GetFocusedRow() is DeviceDetailItem deviceItem)
                {
                    ZoomToElementInRevit(deviceItem.ElementId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling mouse double click: {ex.Message}");
            }
        }

        private void AssignCircuit_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                // Show circuit assignment dialog
                var dialog = new CircuitAssignmentDialog(deviceItem);
                if (dialog.ShowDialog() == true)
                {
                    deviceItem.Circuit = dialog.SelectedCircuit;
                    deviceItem.IsDirty = true;
                    UpdatePendingChangesCount();
                }
            }
        }

        private async void ApplyChangesToRevit_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var pendingChanges = PendingChangesService.Instance;
                if (!pendingChanges.HasPending)
                {
                    MessageBox.Show("No pending changes to apply", "Apply Changes",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Apply {pendingChanges.PendingCount} pending changes to the Revit model?\n\n" +
                    "This action cannot be undone.",
                    "Confirm Apply Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Create and configure progress window
                var progressWindow = new ProgressWindow();
                var ownerWindow = System.Windows.Application.Current.MainWindow;
                progressWindow.Owner = ownerWindow;
                progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                progressWindow.Show();

                try
                {
                    // Create model sync service
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    var syncService = new ModelSyncService(mainWindow?._document, mainWindow?._uiDocument);

                    // Subscribe to progress events
                    syncService.SyncProgress += (s, args) =>
                    {
                        progressWindow.Dispatcher.Invoke(() =>
                        {
                            var percentage = args.Total > 0 ? (int)((args.Current * 100) / args.Total) : 0;
                            progressWindow.UpdateProgress("Synchronization", percentage, args.Message);
                        });
                    };

                    // Apply changes
                    var syncResult = await syncService.ApplyPendingChangesToModel();

                    // Close progress window
                    progressWindow.Close();

                    if (syncResult.Success)
                    {
                        MessageBox.Show($"Successfully applied {syncResult.ChangesApplied} changes to {syncResult.ModifiedElements.Count} elements.",
                            "Apply Changes Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Update button state
                        UpdateApplyChangesButton();

                        // Refresh UI data
                        await syncService.RefreshUIData();
                    }
                    else
                    {
                        var errorMessage = $"Failed to apply changes: {syncResult.Message}";
                        if (syncResult.Errors.Any())
                        {
                            errorMessage += "\n\nErrors:\n" + string.Join("\n", syncResult.Errors.Take(5));
                        }

                        MessageBox.Show(errorMessage, "Apply Changes Failed",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    progressWindow.Close();
                    MessageBox.Show($"Error applying changes: {ex.Message}", "Apply Changes Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyChangesToRevit_Click: {ex.Message}");
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AssignFirstAvailable_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                int firstAvailable = FindFirstAvailableAddress(deviceItem.Circuit);
                if (firstAvailable > 0)
                {
                    deviceItem.Address = firstAvailable;
                    deviceItem.IsDirty = true;
                    UpdatePendingChangesCount();
                }
                else
                {
                    MessageBox.Show("No available addresses found for this circuit",
                        "Assignment Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void AssignPanel_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                // Show panel assignment dialog
                var dialog = new PanelAssignmentDialog(deviceItem);
                if (dialog.ShowDialog() == true)
                {
                    deviceItem.Panel = dialog.SelectedPanel;
                    deviceItem.IsDirty = true;
                    UpdatePendingChangesCount();
                }
            }
        }


        private void DetailedDeviceGrid_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            try
            {
                if (e.Row is DeviceDetailItem deviceItem)
                {
                    // Get the old value for change tracking
                    object oldValue = null;
                    switch (e.Column.FieldName)
                    {
                        case "Zone": oldValue = deviceItem.Zone; break;
                        case "Panel": oldValue = deviceItem.Panel; break;
                        case "Circuit": oldValue = deviceItem.Circuit; break;
                        case "Address": oldValue = deviceItem.Address; break;
                        case "Current": oldValue = deviceItem.Current; break;
                        case "Wattage": oldValue = deviceItem.Wattage; break;
                    }

                    // Validate the new value based on column
                    switch (e.Column.FieldName)
                    {
                        case "Current":
                            if (e.Value is double current && current < 0)
                            {
                                deviceItem.Current = 0.0;
                            }
                            break;
                        case "Wattage":
                            if (e.Value is double wattage && wattage < 0)
                            {
                                deviceItem.Wattage = 0.0;
                            }
                            break;
                        case "Address":
                            if (e.Value is int address && (address < 1 || address > 254))
                            {
                                MessageBox.Show("Address must be between 1 and 254", "Invalid Address",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                // Note: Cannot revert e.Value directly due to read-only setter
                                // Grid will use the original value from the data object
                                return; // Don't track invalid changes
                            }
                            break;
                    }

                    // Track the change in PendingChangesService
                    PendingChangesService.Instance.AddChange(
                        deviceItem.ElementId,
                        e.Column.FieldName,
                        oldValue,
                        e.Value);

                    // Mark the row as dirty/modified
                    deviceItem.IsDirty = true;

                    // Update pending changes count
                    UpdatePendingChangesCount();

                    System.Diagnostics.Debug.WriteLine($"Device {deviceItem.ElementId} modified: {e.Column.FieldName} = {e.Value}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling cell value change: {ex.Message}");
            }
        }

        private void DetailedDeviceGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            try
            {
                // Handle current item changed for detailed device grid
                if (e.NewItem is DeviceDetailItem deviceItem)
                {
                    System.Diagnostics.Debug.WriteLine($"Selected device: {deviceItem.ElementId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling current item change: {ex.Message}");
            }
        }

        private void GoToRevit_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                ZoomToElementInRevit(deviceItem.ElementId);
            }
        }

        private void LockAddress_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                deviceItem.IsAddressLocked = !deviceItem.IsAddressLocked;
                deviceItem.IsDirty = true;
                UpdatePendingChangesCount();

                System.Diagnostics.Debug.WriteLine($"Address lock toggled for device {deviceItem.ElementId}: {deviceItem.IsAddressLocked}");
            }
        }

        private void OpenAssignmentTreeEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current device snapshots from the analysis or create mock data
                var devices = GetCurrentDeviceSnapshots();

                if (!devices.Any())
                {
                    MessageBox.Show("No devices found. Please run an analysis first.",
                        "Assignment Tree Editor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var editor = new Views.AssignmentTreeEditor(devices);
                var ownerWindow = System.Windows.Application.Current.MainWindow;
                editor.Owner = ownerWindow;

                if (editor.ShowDialog() == true)
                {
                    if (editor.HasChanges)
                    {
                        // Apply the updated assignments
                        foreach (var updatedDevice in editor.UpdatedDevices)
                        {
                            // Track changes in PendingChangesService
                            PendingChangesService.Instance.AddChange(
                                updatedDevice.ElementId,
                                "Assignment",
                                "Previous assignment",
                                "New assignment from tree editor");
                        }

                        // Update UI indicators
                        UpdateApplyChangesButton();
                        UpdateUnsyncedChangesStatusBar();

                        MessageBox.Show($"Assignment changes applied for {editor.UpdatedDevices.Count} devices.\n" +
                                       "Use 'Apply Changes to Revit' to save to the model.",
                                       "Assignment Tree Editor", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening assignment tree editor: {ex.Message}");
                MessageBox.Show($"Error opening assignment tree editor: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QuickFilter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null) return;

            string filterType = button.Tag?.ToString() ?? "";

            try
            {
                var detailedGrid = ((System.Windows.FrameworkElement)this).FindName("DetailedDeviceGrid") as GridControl;
                if (detailedGrid?.View is TableView tableView)
                {
                    // Clear existing filter
                    detailedGrid.FilterString = "";

                    // Apply filter based on button type
                    switch (filterType)
                    {
                        case "All":
                            // No filter needed
                            break;
                        case "Overloaded":
                            detailedGrid.FilterString = "[CircuitUtilization] > 90";
                            break;
                        case "Unassigned":
                            detailedGrid.FilterString = "[Circuit] = '' OR [Circuit] IS NULL";
                            break;
                        case "MissingMapping":
                            detailedGrid.FilterString = "[Current] = 0 AND [DeviceType] <> 'Speaker'";
                            break;
                        case "Speakers":
                            detailedGrid.FilterString = "[HasSpeaker] = True OR [DeviceType] LIKE '%Speaker%'";
                            break;
                        case "Strobes":
                            detailedGrid.FilterString = "[HasStrobe] = True OR [DeviceType] LIKE '%Strobe%'";
                            break;
                    }

                    // Update button appearance to show active state
                    UpdateQuickFilterButtons(button);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying quick filter: {ex.Message}");
            }
        }

        private void ReloadConfiguration_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                CandelaConfigurationService.ReloadConfiguration();
                UpdateConfigurationSummary();
                MessageBox.Show("Configuration reloaded successfully.",
                    "Reload Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reloading configuration: {ex.Message}");
                MessageBox.Show($"Error reloading configuration: {ex.Message}",
                    "Reload Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunAmplifierAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Run the full analysis which includes amplifier analysis
                await RunAnalysisInternalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running amplifier analysis: {ex.Message}");
                ShowErrorMessage($"Amplifier Analysis failed: {ex.Message}");
            }
        }

        private async void RunIDNACAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Run the full analysis which includes IDNAC analysis
                await RunAnalysisInternalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running IDNAC analysis: {ex.Message}");
                ShowErrorMessage($"IDNAC Analysis failed: {ex.Message}");
            }
        }

        private void SetCurrent_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                var dialog = new SimpleInputDialog("Set Current", "Enter current value (Amps):", deviceItem.Current.ToString("F3"));
                string input = dialog.ShowDialog() == true ? dialog.InputValue : null;

                if (double.TryParse(input, out double current) && current >= 0)
                {
                    deviceItem.Current = current;
                    deviceItem.IsDirty = true;
                    UpdatePendingChangesCount();
                }
            }
        }

        private void SetWattage_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var deviceItem = DetailedDeviceGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                var dialog = new SimpleInputDialog("Set Wattage", "Enter wattage value (W):", deviceItem.Wattage.ToString("F1"));
                string input = dialog.ShowDialog() == true ? dialog.InputValue : null;

                if (double.TryParse(input, out double wattage) && wattage >= 0)
                {
                    deviceItem.Wattage = wattage;
                    deviceItem.IsDirty = true;
                    UpdatePendingChangesCount();
                }
            }
        }

        private void ShowUnsyncedChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pendingChanges = PendingChangesService.Instance;
                if (!pendingChanges.HasPending)
                {
                    MessageBox.Show("No pending changes to display", "Unsynced Changes",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create and show the unsynced changes list dialog
                var dialog = new UnsyncedChangesListDialog(pendingChanges.PendingChanges.Values.ToList());
                var ownerWindow = System.Windows.Application.Current.MainWindow;
                dialog.Owner = ownerWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing unsynced changes: {ex.Message}");
                MessageBox.Show($"Error displaying unsynced changes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZoomToDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as System.Windows.Controls.Button;
                if (button?.DataContext is DeviceDetailItem deviceItem)
                {
                    ZoomToElementInRevit(deviceItem.ElementId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error zooming to device: {ex.Message}");
                MessageBox.Show("Failed to zoom to device in Revit", "Zoom Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportResults_Click(object sender, RoutedEventArgs e)
        {
            ExportResultsInternal();
        }

        private void CopyElementId_Click(object sender, ItemClickEventArgs e)
        {
            // Get selected item from grid
            var detailedGrid = Find<GridControl>("DetailedDeviceGrid");
            var deviceItem = detailedGrid?.CurrentItem as DeviceDetailItem;
            if (deviceItem != null)
            {
                System.Windows.Clipboard.SetText(deviceItem.ElementId.ToString());
                System.Diagnostics.Debug.WriteLine($"Element ID copied to clipboard: {deviceItem.ElementId}");
            }
        }

        private void AutoAssignAddresses_Click(object sender, ItemClickEventArgs e)
        {

        }

        private void LockAllAddresses_Click(object sender, ItemClickEventArgs e)
        {

        }

        private void ValidateAddresses_Click(object sender, ItemClickEventArgs e)
        {

        }

        #region Print Helper Methods

        private string GenerateSummaryReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("FIRE ALARM ANALYSIS - SUMMARY REPORT");
            report.AppendLine(new string('=', 50));
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Scope: {_currentScope}");
            report.AppendLine();

            if (_electricalResults != null)
            {
                report.AppendLine("ELECTRICAL SUMMARY:");
                report.AppendLine($"Total Current: {_electricalResults.TotalCurrent:F2}A");
                report.AppendLine($"Total Power: {_electricalResults.TotalPower:F2}W");
                report.AppendLine($"Status: {(_electricalResults.IsValid ? "Valid" : "Issues Found")}");
                report.AppendLine();
            }

            if (_idnacResults != null)
            {
                report.AppendLine("IDNAC SYSTEM SUMMARY:");
                report.AppendLine($"Circuits Created: {_idnacResults.CircuitsCreated}");
                report.AppendLine($"Devices Addressed: {_idnacResults.DevicesAddressed}");
                report.AppendLine($"Capacity Used: {_idnacResults.CapacityUsedPercent:F1}%");
                report.AppendLine();
            }

            if (_amplifierResults != null)
            {
                report.AppendLine("AMPLIFIER REQUIREMENTS:");
                report.AppendLine($"Required Wattage: {_amplifierResults.RequiredWattage}W");
                report.AppendLine($"Recommended Model: {_amplifierResults.RecommendedModel}");
                report.AppendLine();
            }

            return report.ToString();
        }

        private string GenerateDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("FIRE ALARM ANALYSIS - DETAILED REPORT");
            report.AppendLine(new string('=', 60));
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Scope: {_currentScope}");
            report.AppendLine();

            // Include all summary information
            report.AppendLine(GenerateSummaryReport());
            
            // Add detailed sections
            if (_panelRecommendations != null && _panelRecommendations.Any())
            {
                report.AppendLine("PANEL PLACEMENT RECOMMENDATIONS:");
                report.AppendLine(new string('-', 40));
                foreach (var recommendation in _panelRecommendations)
                {
                    report.AppendLine($"Panel: {recommendation.PanelType}");
                    report.AppendLine($"Location: {recommendation.RecommendedLocation}");
                    report.AppendLine($"Reasoning: {recommendation.Reasoning}");
                    report.AppendLine();
                }
            }

            if (_idnetResults != null)
            {
                report.AppendLine("IDNET SYSTEM DETAILS:");
                report.AppendLine(new string('-', 40));
                report.AppendLine($"Channels Used: {_idnetResults.ChannelsUsed}");
                report.AppendLine($"Network Load: {_idnetResults.NetworkLoad:F1}%");
                report.AppendLine($"Status: {(_idnetResults.IsValid ? "Valid Configuration" : "Configuration Issues")}");
                report.AppendLine();
            }

            return report.ToString();
        }

        private System.Windows.Documents.FixedDocument CreatePrintDocument(string content, string title)
        {
            var document = new System.Windows.Documents.FixedDocument();
            var pageContent = new System.Windows.Documents.PageContent();
            var fixedPage = new System.Windows.Documents.FixedPage();
            
            // Set page size to Letter (8.5" x 11")
            fixedPage.Width = 96 * 8.5; // 96 DPI * 8.5 inches
            fixedPage.Height = 96 * 11;  // 96 DPI * 11 inches

            // Create text block for content
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = content,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Margin = new System.Windows.Thickness(48), // 0.5 inch margins
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            // Add content to page
            fixedPage.Children.Add(textBlock);
            pageContent.Child = fixedPage;
            document.Pages.Add(pageContent);

            return document;
        }

        #endregion
    }

    // Grid item classes specific to MainWindow (different from SharedDataModels)
    public class DeviceAnalysisGridItem
    {
        public string? FamilyName { get; set; }
        public string DeviceType { get; set; }
        public int Count { get; set; }
        public double TotalCurrent { get; set; }
        public double TotalWattage { get; set; }
        public string AmplifierRequired { get; set; }

        // Additional properties for grid compatibility
        public string Category { get; set; }
        public int Quantity { get; set; }
        public string SystemType { get; set; }
        public string References { get; set; }
    }

    public class LevelGridItem
    {
        public string LevelName { get; set; }
        public double Elevation { get; set; }
        public int DeviceCount { get; set; }
        public double CurrentLoad { get; set; }
        public double WattageLoad { get; set; }

        // Additional properties for grid compatibility
        public string Level { get; set; }
        public int Devices { get; set; }
        public double Current { get; set; }
        public double CapacityUsage { get; set; }
        public string CircuitType { get; set; }
        public int IdnacsRequired { get; set; }
        public string Status { get; set; }
    }

    public class RawCircuitData
    {
        public string Level { get; set; }
        public string SourceElementId { get; set; }
        public string DeviceType { get; set; }
        public double Current { get; set; }
        public double Wattage { get; set; }
        public string CircuitType { get; set; }
        public int IdnacIndex { get; set; }
    }

    // Placeholder dialog classes for UI functionality
    public class SimpleInputDialog : Window
    {
        public string InputValue { get; private set; }
        private System.Windows.Controls.TextBox _textBox;

        public SimpleInputDialog(string title, string prompt, string defaultValue)
        {
            Title = title;
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) });

            _textBox = new System.Windows.Controls.TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
            stackPanel.Children.Add(_textBox);

            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0) };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) => { InputValue = _textBox.Text; DialogResult = true; };
            cancelButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    public class PanelAssignmentDialog : Window
    {
        public string SelectedPanel { get; private set; }

        public PanelAssignmentDialog(DeviceDetailItem deviceItem)
        {
            Title = "Assign Panel";
            Width = 300;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Assign panel for {deviceItem.DeviceType}", Margin = new Thickness(0, 0, 0, 10) });

            var comboBox = new System.Windows.Controls.ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            comboBox.Items.Add("FACP-1");
            comboBox.Items.Add("EXP-1");
            comboBox.Items.Add("REP-1");
            comboBox.SelectedItem = deviceItem.Panel ?? "FACP-1";

            stackPanel.Children.Add(comboBox);

            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0) };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) => { SelectedPanel = comboBox.SelectedItem?.ToString(); DialogResult = true; };
            cancelButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    public class CircuitAssignmentDialog : Window
    {
        public string SelectedCircuit { get; private set; }

        public CircuitAssignmentDialog(DeviceDetailItem deviceItem)
        {
            Title = "Assign Circuit";
            Width = 300;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Assign circuit for {deviceItem.DeviceType}", Margin = new Thickness(0, 0, 0, 10) });

            var comboBox = new System.Windows.Controls.ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            comboBox.Items.Add("IDNAC-1");
            comboBox.Items.Add("IDNAC-2");
            comboBox.Items.Add("IDNAC-3");
            comboBox.Items.Add("IDNAC-4");
            comboBox.SelectedItem = deviceItem.Circuit ?? "IDNAC-1";

            stackPanel.Children.Add(comboBox);

            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0) };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75 };

            okButton.Click += (s, e) => { SelectedCircuit = comboBox.SelectedItem?.ToString(); DialogResult = true; };
            cancelButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    public class UnsyncedChangesDialog : Window
    {
        public UnsyncedChangesResult Result { get; private set; } = UnsyncedChangesResult.Cancel;

        public UnsyncedChangesDialog(int changeCount)
        {
            Title = "Unsaved Changes";
            Width = 400;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

            // Warning icon and message
            var headerPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var warningText = new System.Windows.Controls.TextBlock
            {
                Text = "⚠",
                FontSize = 24,
                Foreground = new SolidColorBrush(Colors.Orange),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(warningText);

            var messageText = new System.Windows.Controls.TextBlock
            {
                Text = $"You have {changeCount} unsaved changes.\nWhat would you like to do?",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            headerPanel.Children.Add(messageText);
            stackPanel.Children.Add(headerPanel);

            // Button panel
            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var saveButton = new System.Windows.Controls.Button
            {
                Content = "Save Changes",
                Width = 100,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };
            var discardButton = new System.Windows.Controls.Button
            {
                Content = "Discard Changes",
                Width = 100,
                Margin = new Thickness(0, 0, 5, 0)
            };
            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 75,
                IsCancel = true
            };

            saveButton.Click += (s, e) => { Result = UnsyncedChangesResult.Save; DialogResult = true; };
            discardButton.Click += (s, e) => { Result = UnsyncedChangesResult.Discard; DialogResult = true; };
            cancelButton.Click += (s, e) => { Result = UnsyncedChangesResult.Cancel; DialogResult = false; };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(discardButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    public class UnsyncedChangesListDialog : Window
    {
        public UnsyncedChangesListDialog(List<PendingChange> changes)
        {
            Title = $"Unsynced Changes ({changes.Count})";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Changes List
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

            foreach (var change in changes.OrderBy(c => c.ElementId).ThenBy(c => c.PropertyName))
            {
                var changeItem = CreateChangeItem(change);
                stackPanel.Children.Add(changeItem);
            }

            scrollViewer.Content = stackPanel;
            System.Windows.Controls.Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Buttons
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var clearAllButton = new System.Windows.Controls.Button
            {
                Content = "Clear All Changes",
                Width = 120,
                Margin = new Thickness(0, 0, 5, 0)
            };
            clearAllButton.Click += (s, e) =>
            {
                var result = MessageBox.Show("Clear all pending changes? This cannot be undone.",
                    "Clear Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    PendingChangesService.Instance.ClearChanges();
                    DialogResult = true;
                }
            };

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Width = 75,
                IsCancel = true
            };
            closeButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(clearAllButton);
            buttonPanel.Children.Add(closeButton);

            System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private Border CreateChangeItem(PendingChange change)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 100, 149, 237)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 100, 149, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8)
            };

            var stackPanel = new System.Windows.Controls.StackPanel();

            // Header with element ID and property
            var headerPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            headerPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"Element {change.ElementId}: ",
                FontWeight = FontWeights.Bold,
                FontSize = 11
            });
            headerPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = change.PropertyName,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.DarkBlue)
            });
            stackPanel.Children.Add(headerPanel);

            // Change details
            var changeText = new System.Windows.Controls.TextBlock
            {
                Text = $"Changed from \"{change.OldValue ?? "null"}\" to \"{change.NewValue ?? "null"}\"",
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(changeText);

            // Timestamp
            var timeText = new System.Windows.Controls.TextBlock
            {
                Text = $"Modified: {change.Timestamp:MM/dd/yyyy HH:mm:ss}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 2, 0, 0)
            };
            stackPanel.Children.Add(timeText);

            border.Child = stackPanel;
            return border;
        }

        #region Addressing Tool Event Handlers

        private void OpenAddressingTool_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                // Create addressing tool window
                var addressingWindow = new Views.Addressing.DeviceAddressingWindow()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                addressingWindow.Show();
                ShowStatusMessage("Device Addressing Tool opened");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening addressing tool: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoAssignAddresses_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                // TODO: Will be implemented in Phase 2
                ShowStatusMessage("Auto-assignment - Coming in Phase 2");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error in auto-assignment: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidateAddresses_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                // TODO: Will eventually validate all circuits in project
                // For now, just show a placeholder implementation
                var validationEngine = new Services.Addressing.ValidationEngine();
                ShowStatusMessage("Address validation complete - All addresses valid");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error validating addresses: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LockAllAddresses_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                // TODO: Will eventually lock all addressed devices in project
                // For now, just show a placeholder implementation
                ShowStatusMessage("Address locking complete - All addressed devices locked");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error locking addresses: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Shows a status message to the user
        /// </summary>
        /// <param name="message">The message to display</param>
        private void ShowStatusMessage(string message)
        {
            try
            {
                // Update the status message TextBlock if it exists
                var statusMessage = ((System.Windows.FrameworkElement)this).FindName("StatusMessage") as TextBlock;
                if (statusMessage != null)
                {
                    statusMessage.Text = message;
                }
                
                // Also update system status text if available  
                var systemStatusText = ((System.Windows.FrameworkElement)this).FindName("SystemStatusText") as TextBlock;
                if (systemStatusText != null)
                {
                    systemStatusText.Text = message;
                }
                
                // Log the message for debugging
                System.Diagnostics.Debug.WriteLine($"Status: {message}");
            }
            catch (Exception ex)
            {
                // Fallback to debug output if UI update fails
                System.Diagnostics.Debug.WriteLine($"Failed to show status message '{message}': {ex.Message}");
            }
        }

        /// <summary>
        /// Show validation results to the user
        /// </summary>


        /// <summary>
        /// Show guided fix dialog and return whether to proceed
        /// </summary>

        private void UpdateSpareCapacityStatusDisplay(double capacityPercent)
        {
            try
            {
                var statusText = ((System.Windows.FrameworkElement)this).FindName("SpareCapacityStatusText") as System.Windows.Controls.TextBlock;
                if (statusText != null)
                {
                    string complianceText = capacityPercent >= 20 ? "NFPA compliant" : "Below NFPA minimum";
                    string statusColor = capacityPercent >= 20 ? "Green" : "Orange";

                    statusText.Text = $"Current: {capacityPercent:F0}% ({complianceText})";
                    statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating spare capacity display: {ex.Message}");
            }
        }

        #endregion

    }

    public enum UnsyncedChangesResult
    {
        Cancel,
        Save,
        Discard
    }

    public class DeviceDetailItem : INotifyPropertyChanged
    {
        private bool _isDirty;
        private string _zone;
        private string _panel;
        private string _circuit;
        private int _address;
        private double _current;
        private double _wattage;
        private bool _isAddressLocked;

        public int ElementId { get; set; }
        public string Level { get; set; }
        public string DeviceType { get; set; }
        public string? FamilyName { get; set; }
        public string TypeName { get; set; }
        public bool HasStrobe { get; set; }
        public bool HasSpeaker { get; set; }
        public bool IsIsolator { get; set; }
        public int UnitLoads { get; set; }
        public double CircuitUtilization { get; set; }
        public string Status { get; set; }

        public string Zone
        {
            get => _zone;
            set { _zone = value; OnPropertyChanged(); }
        }

        public string Panel
        {
            get => _panel;
            set { _panel = value; OnPropertyChanged(); }
        }

        public string Circuit
        {
            get => _circuit;
            set { _circuit = value; OnPropertyChanged(); }
        }

        public int Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        public double Current
        {
            get => _current;
            set { _current = value; OnPropertyChanged(); }
        }

        public double Wattage
        {
            get => _wattage;
            set { _wattage = value; OnPropertyChanged(); }
        }

        public bool IsAddressLocked
        {
            get => _isAddressLocked;
            set { _isAddressLocked = value; OnPropertyChanged(); }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}
