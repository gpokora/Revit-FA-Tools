using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Microsoft.Win32;

namespace Revit_FA_Tools.Views
{
    public partial class MappingEditor : ThemedWindow
    {
        private ObservableCollection<DeviceMappingItem> _mappingItems;
        private CandelaConfiguration _originalConfiguration;
        private bool _hasUnsavedChanges = false;

        public MappingEditor()
        {
            InitializeComponent();
            LoadMapping();
            UpdateUI();
        }

        public class DeviceMappingItem : INotifyPropertyChanged
        {
            private string _deviceKey;
            private string _description;
            private bool _isSpeaker;
            private bool _hasStrobe;
            private bool _isAudioDevice;
            private double _defaultCurrent;
            private int _defaultUnitLoads;
            private string _deviceFunction;
            private string _mountingType;

            public string DeviceKey
            {
                get => _deviceKey;
                set { _deviceKey = value; OnPropertyChanged(nameof(DeviceKey)); }
            }

            public string Description
            {
                get => _description;
                set { _description = value; OnPropertyChanged(nameof(Description)); }
            }

            public bool IsSpeaker
            {
                get => _isSpeaker;
                set { _isSpeaker = value; OnPropertyChanged(nameof(IsSpeaker)); }
            }

            public bool HasStrobe
            {
                get => _hasStrobe;
                set { _hasStrobe = value; OnPropertyChanged(nameof(HasStrobe)); }
            }

            public bool IsAudioDevice
            {
                get => _isAudioDevice;
                set { _isAudioDevice = value; OnPropertyChanged(nameof(IsAudioDevice)); }
            }

            public double DefaultCurrent
            {
                get => _defaultCurrent;
                set { _defaultCurrent = value; OnPropertyChanged(nameof(DefaultCurrent)); }
            }

            public int DefaultUnitLoads
            {
                get => _defaultUnitLoads;
                set { _defaultUnitLoads = value; OnPropertyChanged(nameof(DefaultUnitLoads)); }
            }

            public string DeviceFunction
            {
                get => _deviceFunction;
                set { _deviceFunction = value; OnPropertyChanged(nameof(DeviceFunction)); }
            }

            public string MountingType
            {
                get => _mountingType;
                set { _mountingType = value; OnPropertyChanged(nameof(MountingType)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                // Notify parent that changes were made
                if (System.Windows.Application.Current?.MainWindow is DXWindow mainWindow)
                {
                    // Find the mapping editor window and mark as changed
                    foreach (Window window in System.Windows.Application.Current.Windows)
                    {
                        if (window is MappingEditor editor)
                        {
                            editor.MarkAsChanged();
                            break;
                        }
                    }
                }
            }
        }

        private void LoadMapping()
        {
            try
            {
                statusText.Text = "Loading device mappings...";
                
                _originalConfiguration = CandelaConfigurationService.LoadConfiguration();
                _mappingItems = new ObservableCollection<DeviceMappingItem>();

                foreach (var deviceType in _originalConfiguration.DeviceTypes)
                {
                    var item = new DeviceMappingItem
                    {
                        DeviceKey = deviceType.Key,
                        Description = deviceType.Value.Description ?? "",
                        IsSpeaker = deviceType.Value.IsSpeaker,
                        HasStrobe = deviceType.Value.HasStrobe,
                        IsAudioDevice = deviceType.Value.IsAudioDevice,
                        DeviceFunction = deviceType.Value.DeviceFunction ?? "",
                        MountingType = deviceType.Value.MountingType ?? "",
                        DefaultCurrent = GetFirstCurrentValue(deviceType.Value.CandelaCurrentMap),
                        DefaultUnitLoads = GetFirstUnitLoadValue(deviceType.Value.UnitLoadMap)
                    };
                    _mappingItems.Add(item);
                }

                mappingGrid.ItemsSource = _mappingItems;
                statusText.Text = "Device mappings loaded successfully";
                UpdateDeviceCount();
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error loading mappings: {ex.Message}";
                DXMessageBox.Show($"Failed to load device mappings:\n{ex.Message}", 
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private double GetFirstCurrentValue(Dictionary<string, double> currentMap)
        {
            return currentMap?.Values.FirstOrDefault() ?? 0.020;
        }

        private int GetFirstUnitLoadValue(Dictionary<string, int> ulMap)
        {
            return ulMap?.Values.FirstOrDefault() ?? 1;
        }

        private void UpdateUI()
        {
            Title = _hasUnsavedChanges ? "Candela Current Mapping Editor *" : "Candela Current Mapping Editor";
            btnSave.IsEnabled = _hasUnsavedChanges;
            btnApply.IsEnabled = _hasUnsavedChanges;
        }

        private void UpdateDeviceCount()
        {
            deviceCountText.Text = $"{_mappingItems?.Count ?? 0} device types";
        }

        public void MarkAsChanged()
        {
            _hasUnsavedChanges = true;
            UpdateUI();
        }

        private void btnAddDevice_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var newItem = new DeviceMappingItem
                {
                    DeviceKey = $"NEW_DEVICE_{_mappingItems.Count + 1}",
                    Description = "New device type - please edit",
                    IsSpeaker = false,
                    HasStrobe = false,
                    IsAudioDevice = false,
                    DefaultCurrent = 0.020,
                    DefaultUnitLoads = 1,
                    DeviceFunction = "NOTIFICATION",
                    MountingType = "WALL"
                };

                _mappingItems.Add(newItem);
                UpdateDeviceCount();
                MarkAsChanged();

                // Select and focus the new row
                mappingView.FocusedRowHandle = _mappingItems.Count - 1;
                mappingView.ShowEditor();

                statusText.Text = "New device added - please edit the device key and description";
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error adding device: {ex.Message}";
            }
        }

        private void btnDeleteDevice_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var selectedItems = mappingView.GetSelectedRows().Cast<int>()
                    .Select(handle => mappingGrid.GetRow(handle) as DeviceMappingItem)
                    .Where(item => item != null)
                    .ToList();

                if (!selectedItems.Any())
                {
                    statusText.Text = "No devices selected for deletion";
                    return;
                }

                var result = DXMessageBox.Show(
                    $"Delete {selectedItems.Count} selected device mapping(s)?\n\n" +
                    "This action cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var item in selectedItems)
                    {
                        _mappingItems.Remove(item);
                    }

                    UpdateDeviceCount();
                    MarkAsChanged();
                    statusText.Text = $"Deleted {selectedItems.Count} device mappings";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error deleting devices: {ex.Message}";
            }
        }

        private void DuplicateRow_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var focusedRow = mappingGrid.GetFocusedRow() as DeviceMappingItem;
                if (focusedRow == null)
                {
                    statusText.Text = "No row selected for duplication";
                    return;
                }

                var duplicatedItem = new DeviceMappingItem
                {
                    DeviceKey = $"{focusedRow.DeviceKey}_COPY",
                    Description = $"{focusedRow.Description} (Copy)",
                    IsSpeaker = focusedRow.IsSpeaker,
                    HasStrobe = focusedRow.HasStrobe,
                    IsAudioDevice = focusedRow.IsAudioDevice,
                    DefaultCurrent = focusedRow.DefaultCurrent,
                    DefaultUnitLoads = focusedRow.DefaultUnitLoads,
                    DeviceFunction = focusedRow.DeviceFunction,
                    MountingType = focusedRow.MountingType
                };

                _mappingItems.Add(duplicatedItem);
                UpdateDeviceCount();
                MarkAsChanged();
                statusText.Text = "Row duplicated";
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error duplicating row: {ex.Message}";
            }
        }

        private void DeleteRow_Click(object sender, ItemClickEventArgs e)
        {
            btnDeleteDevice_Click(sender, null);
        }

        private void btnImportMapping_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Import Device Mappings from CSV"
                };

                if (openDialog.ShowDialog() == true)
                {
                    ImportFromCSV(openDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error importing CSV: {ex.Message}";
                DXMessageBox.Show($"Failed to import CSV:\n{ex.Message}", 
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportMapping_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Export Device Mappings to CSV",
                    FileName = "device_mappings.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportToCSV(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error exporting CSV: {ex.Message}";
                DXMessageBox.Show($"Failed to export CSV:\n{ex.Message}", 
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFromCSV(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                throw new InvalidOperationException("CSV file must have header and at least one data row");
            }

            var importedCount = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length >= 8)
                {
                    var item = new DeviceMappingItem
                    {
                        DeviceKey = fields[0].Trim('"'),
                        Description = fields[1].Trim('"'),
                        IsSpeaker = bool.Parse(fields[2]),
                        HasStrobe = bool.Parse(fields[3]),
                        IsAudioDevice = bool.Parse(fields[4]),
                        DefaultCurrent = double.Parse(fields[5]),
                        DefaultUnitLoads = int.Parse(fields[6]),
                        DeviceFunction = fields[7].Trim('"'),
                        MountingType = fields.Length > 8 ? fields[8].Trim('"') : ""
                    };
                    _mappingItems.Add(item);
                    importedCount++;
                }
            }

            UpdateDeviceCount();
            MarkAsChanged();
            statusText.Text = $"Imported {importedCount} device mappings from CSV";
        }

        private void ExportToCSV(string filePath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("DeviceKey,Description,IsSpeaker,HasStrobe,IsAudioDevice,DefaultCurrent,DefaultUnitLoads,DeviceFunction,MountingType");

            foreach (var item in _mappingItems)
            {
                csv.AppendLine($"\"{item.DeviceKey}\",\"{item.Description}\",{item.IsSpeaker},{item.HasStrobe}," +
                             $"{item.IsAudioDevice},{item.DefaultCurrent},{item.DefaultUnitLoads}," +
                             $"\"{item.DeviceFunction}\",\"{item.MountingType}\"");
            }

            File.WriteAllText(filePath, csv.ToString());
            statusText.Text = $"Exported {_mappingItems.Count} device mappings to CSV";
        }

        private void btnReload_Click(object sender, ItemClickEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = DXMessageBox.Show(
                    "You have unsaved changes. Reload will discard all changes.\n\nContinue?",
                    "Confirm Reload", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            LoadMapping();
            _hasUnsavedChanges = false;
            UpdateUI();
        }

        private void btnSave_Click(object sender, ItemClickEventArgs e)
        {
            SaveChanges();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            SaveChanges();
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = DXMessageBox.Show(
                    "You have unsaved changes. Close without saving?",
                    "Confirm Close", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            DialogResult = false;
        }

        private void SaveChanges()
        {
            try
            {
                statusText.Text = "Saving changes...";

                // Create new configuration with updated mappings
                var updatedConfig = new CandelaConfiguration();
                updatedConfig.Info = _originalConfiguration.Info;
                updatedConfig.FallbackHierarchy = _originalConfiguration.FallbackHierarchy;
                updatedConfig.RecognitionPatterns = _originalConfiguration.RecognitionPatterns;

                // Convert mapping items back to device types
                foreach (var item in _mappingItems)
                {
                    var deviceConfig = new DeviceTypeConfig
                    {
                        Description = item.Description,
                        IsSpeaker = item.IsSpeaker,
                        HasStrobe = item.HasStrobe,
                        IsAudioDevice = item.IsAudioDevice,
                        DeviceFunction = item.DeviceFunction,
                        MountingType = item.MountingType,
                        CandelaCurrentMap = new Dictionary<string, double>
                        {
                            ["Default"] = item.DefaultCurrent
                        },
                        UnitLoadMap = new Dictionary<string, int>
                        {
                            ["Default"] = item.DefaultUnitLoads
                        }
                    };

                    updatedConfig.DeviceTypes[item.DeviceKey] = deviceConfig;
                }

                // Save configuration
                if (CandelaConfigurationService.SaveConfiguration(updatedConfig))
                {
                    CandelaConfigurationService.ReloadConfiguration(); // Clear cache
                    _hasUnsavedChanges = false;
                    UpdateUI();
                    statusText.Text = $"Saved {_mappingItems.Count} device mappings successfully";
                }
                else
                {
                    statusText.Text = "Failed to save configuration file";
                    DXMessageBox.Show("Failed to save configuration file. Check file permissions.", 
                        "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error saving changes: {ex.Message}";
                DXMessageBox.Show($"Failed to save changes:\n{ex.Message}", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = DXMessageBox.Show(
                    "You have unsaved changes. Save before closing?",
                    "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveChanges();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
    }
}