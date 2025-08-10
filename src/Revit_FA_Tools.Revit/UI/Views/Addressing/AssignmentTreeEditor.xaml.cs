using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Revit_FA_Tools.Models;
using Revit_FA_Tools.Services;

namespace Revit_FA_Tools.Views
{
    public partial class AssignmentTreeEditor : ThemedWindow
    {
        private ObservableCollection<AssignmentTreeNode> _treeData;
        private AssignmentTreeNode _selectedNode;
        private bool _isDragDropEnabled = true;
        private Point _dragStartPoint;
        private bool _isDragging = false;

        public List<DeviceSnapshot> UpdatedDevices { get; private set; } = new List<DeviceSnapshot>();
        public bool HasChanges { get; private set; } = false;

        public AssignmentTreeEditor(List<DeviceSnapshot> devices)
        {
            InitializeComponent();
            LoadAssignmentTree(devices);
            UpdateStatusBar();
        }

        private void LoadAssignmentTree(List<DeviceSnapshot> devices)
        {
            _treeData = new ObservableCollection<AssignmentTreeNode>();

            // Group devices by their current assignments
            var unassignedDevices = devices.Where(d => string.IsNullOrEmpty(d.Zone)).ToList();
            var assignedDevices = devices.Where(d => !string.IsNullOrEmpty(d.Zone)).ToList();

            // Create panel nodes
            var panelGroups = assignedDevices.GroupBy(d => GetPanelForDevice(d)).ToList();

            foreach (var panelGroup in panelGroups)
            {
                var panelNode = new AssignmentTreeNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = panelGroup.Key ?? "Unknown Panel",
                    Type = "Panel",
                    NodeType = AssignmentNodeType.Panel,
                    StatusText = $"({panelGroup.Count()} devices)"
                };

                // Create circuit nodes under each panel
                var circuitGroups = panelGroup.GroupBy(d => GetCircuitForDevice(d)).ToList();

                foreach (var circuitGroup in circuitGroups)
                {
                    var circuitNode = new AssignmentTreeNode
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = circuitGroup.Key ?? "Unknown Circuit",
                        Type = "Circuit",
                        NodeType = AssignmentNodeType.Circuit,
                        Parent = panelNode,
                        Current = circuitGroup.Sum(d => d.Amps),
                        UnitLoads = circuitGroup.Sum(d => d.UnitLoads),
                        UtilizationText = $"{circuitGroup.Sum(d => d.Amps):F2}A / {circuitGroup.Sum(d => d.UnitLoads)}UL"
                    };

                    // Add device nodes under each circuit
                    foreach (var device in circuitGroup)
                    {
                        var deviceNode = new AssignmentTreeNode
                        {
                            Id = device.ElementId.ToString(),
                            Name = $"{device.FamilyName} - {device.TypeName}",
                            Type = "Device",
                            NodeType = AssignmentNodeType.Device,
                            Parent = circuitNode,
                            Current = device.Amps,
                            UnitLoads = device.UnitLoads,
                            Details = $"{device.LevelName} | {device.Amps:F3}A | {device.UnitLoads}UL",
                            DeviceSnapshot = device,
                            IconSource = GetDeviceIcon(device)
                        };

                        circuitNode.Children.Add(deviceNode);
                    }

                    circuitNode.Status = GetCircuitStatus(circuitNode);
                    panelNode.Children.Add(circuitNode);
                }

                panelNode.Current = panelNode.Children.Sum(c => c.Current);
                panelNode.UnitLoads = panelNode.Children.Sum(c => c.UnitLoads);
                panelNode.Status = GetPanelStatus(panelNode);
                _treeData.Add(panelNode);
            }

            // Add unassigned devices node if any exist
            if (unassignedDevices.Any())
            {
                var unassignedNode = new AssignmentTreeNode
                {
                    Id = "unassigned",
                    Name = "Unassigned Devices",
                    Type = "Container",
                    NodeType = AssignmentNodeType.Container,
                    StatusText = $"({unassignedDevices.Count} devices)",
                    Status = "Warning"
                };

                foreach (var device in unassignedDevices)
                {
                    var deviceNode = new AssignmentTreeNode
                    {
                        Id = device.ElementId.ToString(),
                        Name = $"{device.FamilyName} - {device.TypeName}",
                        Type = "Device",
                        NodeType = AssignmentNodeType.Device,
                        Parent = unassignedNode,
                        Current = device.Amps,
                        UnitLoads = device.UnitLoads,
                        Details = $"{device.LevelName} | {device.Amps:F3}A | {device.UnitLoads}UL",
                        DeviceSnapshot = device,
                        Status = "Unassigned",
                        IconSource = GetDeviceIcon(device)
                    };

                    unassignedNode.Children.Add(deviceNode);
                }

                _treeData.Add(unassignedNode);
            }

            AssignmentTree.ItemsSource = _treeData;
        }

        private string GetPanelForDevice(DeviceSnapshot device)
        {
            // This would typically come from device parameters
            // For now, use a simple heuristic based on level
            return device.Zone?.Contains("FACP") == true ? "FACP-1" :
                   device.Zone?.Contains("EXP") == true ? "EXP-1" :
                   "FACP-1";
        }

        private string GetCircuitForDevice(DeviceSnapshot device)
        {
            // This would typically come from device parameters
            // For now, generate based on device type and location
            if (device.HasSpeaker) return "IDNAC-1";
            if (device.HasStrobe) return "IDNAC-2";
            return "IDNAC-3";
        }

        private string GetDeviceIcon(DeviceSnapshot device)
        {
            if (device.HasSpeaker && device.HasStrobe) return "{dx:DXImage SvgImages/Icon Builder/Actions_SpeakerStrobe.svg}";
            if (device.HasSpeaker) return "{dx:DXImage SvgImages/Icon Builder/Actions_Speaker.svg}";
            if (device.HasStrobe) return "{dx:DXImage SvgImages/Icon Builder/Actions_Strobe.svg}";
            if (device.IsIsolator) return "{dx:DXImage SvgImages/Icon Builder/Actions_Isolator.svg}";
            return "{dx:DXImage SvgImages/Icon Builder/Actions_Device.svg}";
        }

        private string GetCircuitStatus(AssignmentTreeNode circuitNode)
        {
            double currentUtilization = circuitNode.Current / 3.0 * 100;
            double ulUtilization = circuitNode.UnitLoads / 139.0 * 100;

            if (currentUtilization > 90 || ulUtilization > 90) return "Overloaded";
            if (currentUtilization > 75 || ulUtilization > 75) return "Warning";
            return "OK";
        }

        private string GetPanelStatus(AssignmentTreeNode panelNode)
        {
            var overloadedCircuits = panelNode.Children.Count(c => c.Status == "Overloaded");
            if (overloadedCircuits > 0) return $"{overloadedCircuits} overloaded circuits";

            var warningCircuits = panelNode.Children.Count(c => c.Status == "Warning");
            if (warningCircuits > 0) return $"{warningCircuits} circuits at capacity";

            return "OK";
        }

        private void UpdateStatusBar()
        {
            var allDevices = GetAllDeviceNodes(_treeData);
            var unassignedDevices = allDevices.Where(d => d.Status == "Unassigned").ToList();

            TotalDevicesText.Text = allDevices.Count.ToString();
            UnassignedDevicesText.Text = unassignedDevices.Count.ToString();

            if (unassignedDevices.Any())
            {
                StatusText.Text = $"{unassignedDevices.Count} devices need assignment";
            }
            else
            {
                StatusText.Text = "All devices assigned";
            }
        }

        private List<AssignmentTreeNode> GetAllDeviceNodes(ObservableCollection<AssignmentTreeNode> nodes)
        {
            var devices = new List<AssignmentTreeNode>();

            foreach (var node in nodes)
            {
                if (node.NodeType == AssignmentNodeType.Device)
                {
                    devices.Add(node);
                }

                devices.AddRange(GetAllDeviceNodes(node.Children));
            }

            return devices;
        }

        #region Event Handlers

        private void ExpandAll_Click(object sender, ItemClickEventArgs e)
        {
            ExpandAllTreeViewItems(AssignmentTree);
        }

        private void CollapseAll_Click(object sender, ItemClickEventArgs e)
        {
            CollapseAllTreeViewItems(AssignmentTree);
        }

        private void ExpandAllTreeViewItems(TreeView treeView)
        {
            if (treeView.Items.Count == 0) return;

            foreach (var item in treeView.Items)
            {
                if (treeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true;
                    ExpandTreeViewItem(treeViewItem);
                }
            }
        }

        private void ExpandTreeViewItem(TreeViewItem item)
        {
            item.IsExpanded = true;
            foreach (var childItem in item.Items)
            {
                if (item.ItemContainerGenerator.ContainerFromItem(childItem) is TreeViewItem childTreeViewItem)
                {
                    ExpandTreeViewItem(childTreeViewItem);
                }
            }
        }

        private void CollapseAllTreeViewItems(TreeView treeView)
        {
            if (treeView.Items.Count == 0) return;

            foreach (var item in treeView.Items)
            {
                if (treeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    CollapseTreeViewItem(treeViewItem);
                }
            }
        }

        private void CollapseTreeViewItem(TreeViewItem item)
        {
            item.IsExpanded = false;
            foreach (var childItem in item.Items)
            {
                if (item.ItemContainerGenerator.ContainerFromItem(childItem) is TreeViewItem childTreeViewItem)
                {
                    CollapseTreeViewItem(childTreeViewItem);
                }
            }
        }

        private AssignmentTreeNode GetTreeNodeAtPoint(TreeView treeView, Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(treeView, point);
            if (hitTestResult?.VisualHit == null) return null;

            // Walk up the visual tree to find a TreeViewItem
            var element = hitTestResult.VisualHit as DependencyObject;
            while (element != null && !(element is TreeViewItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is TreeViewItem treeViewItem)
            {
                return treeViewItem.DataContext as AssignmentTreeNode;
            }

            return null;
        }

        private void AutoBalance_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                StatusText.Text = "Auto-balancing assignments...";

                // Get all device nodes
                var allDevices = GetAllDeviceNodes(_treeData);
                var deviceSnapshots = allDevices.Where(d => d.DeviceSnapshot != null)
                                                .Select(d => d.DeviceSnapshot)
                                                .ToList();

                // Use the circuit balancer
                var balancer = new CircuitBalancer();
                var capacity = new CircuitBalancer.CircuitCapacity();
                var options = new CircuitBalancer.BalancingOptions { UseOptimizedBalancing = true };

                var result = balancer.BalanceDevices(deviceSnapshots, capacity, options);

                if (result.UnallocatedDevices == 0)
                {
                    // Rebuild tree with new assignments
                    LoadAssignmentTree(deviceSnapshots);
                    HasChanges = true;
                    StatusText.Text = $"Auto-balance completed: {result.Circuits.Count} circuits optimized";
                }
                else
                {
                    var warningMsg = result.Warnings.Any() ? string.Join(", ", result.Warnings) : "Unknown balancing issue";
                    StatusText.Text = $"Auto-balance incomplete: {result.UnallocatedDevices} unallocated devices. {warningMsg}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-balance error: {ex.Message}";
            }
        }

        private void ValidateAssignments_Click(object sender, ItemClickEventArgs e)
        {
            var issues = new List<string>();

            foreach (var panel in _treeData.Where(n => n.NodeType == AssignmentNodeType.Panel))
            {
                foreach (var circuit in panel.Children.Where(c => c.NodeType == AssignmentNodeType.Circuit))
                {
                    // Check capacity limits
                    if (circuit.Current > 3.0)
                        issues.Add($"{circuit.Name}: Current exceeds 3.0A limit ({circuit.Current:F2}A)");

                    if (circuit.UnitLoads > 139)
                        issues.Add($"{circuit.Name}: Unit loads exceed 139 UL limit ({circuit.UnitLoads} UL)");

                    // Check address conflicts
                    var deviceAddresses = circuit.Children.Where(d => d.DeviceSnapshot != null)
                                                         .GroupBy(d => d.DeviceSnapshot.ElementId) // Using ElementId as address proxy
                                                         .Where(g => g.Count() > 1);

                    foreach (var addressGroup in deviceAddresses)
                    {
                        issues.Add($"{circuit.Name}: Address conflict at address {addressGroup.Key}");
                    }
                }
            }

            if (issues.Any())
            {
                var message = $"Found {issues.Count} validation issues:\n\n" +
                             string.Join("\n", issues.Take(10));
                if (issues.Count > 10)
                    message += $"\n... and {issues.Count - 10} more issues";

                MessageBox.Show(message, "Validation Issues", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("All assignments are valid!", "Validation Complete",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddPanel_Click(object sender, ItemClickEventArgs e)
        {
            var newPanel = new AssignmentTreeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"New Panel {_treeData.Count(n => n.NodeType == AssignmentNodeType.Panel) + 1}",
                Type = "Panel",
                NodeType = AssignmentNodeType.Panel,
                StatusText = "(0 devices)",
                Status = "OK"
            };

            _treeData.Add(newPanel);
            HasChanges = true;
            StatusText.Text = "New panel added";
        }

        private void AddCircuit_Click(object sender, ItemClickEventArgs e)
        {
            if (_selectedNode?.NodeType == AssignmentNodeType.Panel)
            {
                var newCircuit = new AssignmentTreeNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"IDNAC-{_selectedNode.Children.Count + 1}",
                    Type = "Circuit",
                    NodeType = AssignmentNodeType.Circuit,
                    Parent = _selectedNode,
                    Status = "OK"
                };

                _selectedNode.Children.Add(newCircuit);
                HasChanges = true;
                StatusText.Text = "New circuit added to " + _selectedNode.Name;
            }
            else
            {
                StatusText.Text = "Select a panel to add a circuit";
            }
        }

        private void EnableDragDrop_CheckedChanged(object sender, ItemClickEventArgs e)
        {
            _isDragDropEnabled = EnableDragDropCheck.IsChecked ?? false;
            StatusText.Text = $"Drag & drop {(_isDragDropEnabled ? "enabled" : "disabled")}";
        }

        private void UpdatePropertiesPanel()
        {
            // Hide all property panels
            NoSelectionText.Visibility = Visibility.Visible;
            PanelProperties.Visibility = Visibility.Collapsed;
            CircuitProperties.Visibility = Visibility.Collapsed;
            DeviceProperties.Visibility = Visibility.Collapsed;

            if (_selectedNode == null) return;

            NoSelectionText.Visibility = Visibility.Collapsed;

            switch (_selectedNode.NodeType)
            {
                case AssignmentNodeType.Panel:
                    PanelProperties.Visibility = Visibility.Visible;
                    PanelNameBox.Text = _selectedNode.Name;
                    PanelTypeCombo.Text = _selectedNode.Type;
                    // PanelLocationBox would be populated from actual panel data
                    break;

                case AssignmentNodeType.Circuit:
                    CircuitProperties.Visibility = Visibility.Visible;
                    CircuitNameBox.Text = _selectedNode.Name;
                    CircuitTypeCombo.Text = _selectedNode.Type;

                    // Update capacity bars
                    double currentUtilization = _selectedNode.Current / 3.0 * 100;
                    double ulUtilization = _selectedNode.UnitLoads / 139.0 * 100;

                    CurrentUsageBar.Value = Math.Min(currentUtilization, 100);
                    CurrentUsageText.Text = $"Current: {_selectedNode.Current:F2}A / 3.0A ({currentUtilization:F1}%)";

                    ULUsageBar.Value = Math.Min(ulUtilization, 100);
                    ULUsageText.Text = $"Unit Loads: {_selectedNode.UnitLoads} / 139 UL ({ulUtilization:F1}%)";
                    break;

                case AssignmentNodeType.Device:
                    DeviceProperties.Visibility = Visibility.Visible;
                    DeviceNameText.Text = _selectedNode.Name;
                    if (_selectedNode.DeviceSnapshot != null)
                    {
                        DeviceFamilyText.Text = _selectedNode.DeviceSnapshot.FamilyName;
                        DeviceTypeText.Text = _selectedNode.DeviceSnapshot.TypeName;
                        DeviceLevelBox.Text = _selectedNode.DeviceSnapshot.LevelName;
                        DeviceZoneBox.Text = _selectedNode.DeviceSnapshot.Zone ?? "";
                        DeviceAddressSpinner.Value = _selectedNode.DeviceSnapshot.ElementId; // Using ElementId as proxy
                    }
                    break;
            }
        }

        #region Drag and Drop

        private void AssignmentTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragDropEnabled || e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            Point currentPosition = e.GetPosition(AssignmentTree);

            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var node = GetTreeNodeAtPoint(AssignmentTree, currentPosition);
                if (node?.NodeType == AssignmentNodeType.Device)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(AssignmentTree, node, DragDropEffects.Move);
                    _isDragging = false;
                }
            }
        }

        private void AssignmentTree_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (!_isDragDropEnabled) return;

            var position = e.GetPosition(AssignmentTree);
            var targetNode = GetTreeNodeAtPoint(AssignmentTree, position);
            var draggedNode = e.Data.GetData(typeof(AssignmentTreeNode)) as AssignmentTreeNode;

            if (targetNode != null && draggedNode != null &&
                    targetNode.NodeType == AssignmentNodeType.Circuit &&
                    draggedNode.NodeType == AssignmentNodeType.Device)
            {
                e.Effects = DragDropEffects.Move;
            }
        }
    

        private void AssignmentTree_DragDrop(object sender, DragEventArgs e)
        {
            if (!_isDragDropEnabled) return;

            var position = e.GetPosition(AssignmentTree);
            var targetNode = GetTreeNodeAtPoint(AssignmentTree, position);
            var draggedNode = e.Data.GetData(typeof(AssignmentTreeNode)) as AssignmentTreeNode;

            if (targetNode?.NodeType == AssignmentNodeType.Circuit &&
                draggedNode?.NodeType == AssignmentNodeType.Device)
            {
                // Remove from old parent
                if (draggedNode.Parent != null)
                {
                    draggedNode.Parent.Children.Remove(draggedNode);
                    UpdateNodeTotals(draggedNode.Parent);
                }

                // Add to new parent
                draggedNode.Parent = targetNode;
                targetNode.Children.Add(draggedNode);
                UpdateNodeTotals(targetNode);

                HasChanges = true;
                StatusText.Text = $"Moved {draggedNode.Name} to {targetNode.Name}";
                UpdateStatusBar();
            }
        }

        private void UpdateNodeTotals(AssignmentTreeNode node)
        {
            if (node.NodeType == AssignmentNodeType.Circuit)
            {
                node.Current = node.Children.Sum(c => c.Current);
                node.UnitLoads = node.Children.Sum(c => c.UnitLoads);
                node.UtilizationText = $"{node.Current:F2}A / {node.UnitLoads}UL";
                node.Status = GetCircuitStatus(node);

                // Update parent panel totals
                if (node.Parent != null)
                {
                    UpdateNodeTotals(node.Parent);
                }
            }
            else if (node.NodeType == AssignmentNodeType.Panel)
            {
                node.Current = node.Children.Sum(c => c.Current);
                node.UnitLoads = node.Children.Sum(c => c.UnitLoads);
                node.Status = GetPanelStatus(node);
            }
        }

        #endregion

        private void UpdatePanel_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode?.NodeType == AssignmentNodeType.Panel)
            {
                _selectedNode.Name = PanelNameBox.Text;
                _selectedNode.Type = PanelTypeCombo.Text;
                HasChanges = true;
                StatusText.Text = "Panel updated";
            }
        }

        private void UpdateCircuit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode?.NodeType == AssignmentNodeType.Circuit)
            {
                _selectedNode.Name = CircuitNameBox.Text;
                _selectedNode.Type = CircuitTypeCombo.Text;
                HasChanges = true;
                StatusText.Text = "Circuit updated";
            }
        }

        private void UpdateDevice_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode?.NodeType == AssignmentNodeType.Device && _selectedNode.DeviceSnapshot != null)
            {
                // Update device snapshot with new values
                var newSnapshot = _selectedNode.DeviceSnapshot with
                {
                    LevelName = DeviceLevelBox.Text,
                    Zone = DeviceZoneBox.Text
                };

                _selectedNode.DeviceSnapshot = newSnapshot;
                HasChanges = true;
                StatusText.Text = "Device updated";
            }
        }

        private void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Collect all updated device snapshots
                var allDevices = GetAllDeviceNodes(_treeData);
                UpdatedDevices = allDevices.Where(d => d.DeviceSnapshot != null)
                                          .Select(d => d.DeviceSnapshot)
                                          .ToList();

                DialogResult = true;
                StatusText.Text = $"Applied changes for {UpdatedDevices.Count} devices";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (HasChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Close without saving?",
                                           "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;
            }

            DialogResult = false;
        }

        #endregion

        private void AssignmentTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedNode = e.NewValue as AssignmentTreeNode;
            UpdatePropertiesPanel();
        }
    }
}

    #region Data Models

    public class AssignmentTreeNode : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public AssignmentNodeType NodeType { get; set; }
        public AssignmentTreeNode Parent { get; set; }
        public ObservableCollection<AssignmentTreeNode> Children { get; set; } = new ObservableCollection<AssignmentTreeNode>();
        
        public double Current { get; set; }
        public int UnitLoads { get; set; }
        public string Status { get; set; }
        public string StatusText { get; set; }
        public string UtilizationText { get; set; }
        public string Details { get; set; }
        public string IconSource { get; set; }
        public string ToolTip => $"{Name} - {Type} - {Status}";
        
        public DeviceSnapshot DeviceSnapshot { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum AssignmentNodeType
    {
        Panel,
        Circuit,
        Device,
        Container
    }

    #endregion
