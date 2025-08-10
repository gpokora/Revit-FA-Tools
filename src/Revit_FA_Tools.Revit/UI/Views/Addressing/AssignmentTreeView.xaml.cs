using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Revit_FA_Tools.ViewModels.Tree;

namespace Revit_FA_Tools.Views
{
    /// <summary>
    /// Code-behind for AssignmentTreeView - handles drag-drop operations and UI events
    /// </summary>
    public partial class AssignmentTreeView : UserControl
    {
        private AssignmentTreeViewModel ViewModel => DataContext as AssignmentTreeViewModel;

        public AssignmentTreeView()
        {
            InitializeComponent();
            
            // Wire up TreeList events
            AssignmentTree.DragEnter += OnDragEnter;
            AssignmentTree.DragOver += OnDragOver;
            AssignmentTree.Drop += OnDrop;
            AssignmentTree.DragLeave += OnDragLeave;
            AssignmentTree.MouseMove += OnMouseMove;
            AssignmentTree.SelectedItemChanged += OnSelectionChanged;
            
            // Note: WPF TreeView doesn't support inline editing like DevExpress TreeListControl
            // Inline editing would need to be implemented with custom templates if needed
        }

        #region Drag-Drop Event Handlers

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            // Visual feedback for drag enter
            if (ValidateDragData(e))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            
            if (!ValidateDragData(e))
            {
                return;
            }

            // Get the target node under the mouse
            var targetNode = GetTreeNodeAtPoint(AssignmentTree, e.GetPosition(AssignmentTree));
            if (targetNode == null)
            {
                return;
            }
            var sourceData = e.Data.GetData("NodeData") as BaseNodeVM;

            if (IsValidDropTarget(sourceData, targetNode))
            {
                e.Effects = DragDropEffects.Move;
                
                // Visual feedback - find and select the TreeViewItem for the target node
                SelectTreeViewItem(AssignmentTree, targetNode);
            }

            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!ValidateDragData(e))
                {
                    return;
                }

                // Get drop target
                var targetNode = GetTreeNodeAtPoint(AssignmentTree, e.GetPosition(AssignmentTree));
                if (targetNode == null)
                {
                    return;
                }
                var sourceNode = e.Data.GetData("NodeData") as BaseNodeVM;

                if (!IsValidDropTarget(sourceNode, targetNode))
                {
                    return;
                }

                // Perform the drop operation
                bool success = PerformDropOperation(sourceNode, targetNode);
                
                if (success && ViewModel != null)
                {
                    // Refresh the tree after successful drop
                    ViewModel.RecomputeAndRefresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during drop operation: {ex.Message}", "Drop Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                e.Handled = true;
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            // Clear any visual feedback
            e.Handled = true;
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Initiate drag operation if left mouse button is pressed
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var draggedNode = GetTreeNodeAtPoint(AssignmentTree, e.GetPosition(AssignmentTree));
                if (draggedNode != null && IsDraggable(draggedNode))
                {
                    var data = new DataObject("NodeData", draggedNode);
                    DragDrop.DoDragDrop(AssignmentTree, data, DragDropEffects.Move);
                }
            }
        }

        #endregion

        #region Selection and Editing Event Handlers

        private void OnSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel == null) return;

            try
            {
                var selectedNode = e.NewValue as BaseNodeVM;
                
                // Update view model selection based on node type
                if (selectedNode is PanelNodeVM panel)
                {
                    ViewModel.SelectedPanel = panel;
                    ViewModel.SelectedBranch = null;
                    ViewModel.SelectedDevice = null;
                }
                else if (selectedNode is BranchNodeVM branch)
                {
                    ViewModel.SelectedBranch = branch;
                    ViewModel.SelectedDevice = null;
                    // Find parent panel
                    ViewModel.SelectedPanel = FindParentPanel(branch);
                }
                else if (selectedNode is DeviceNodeVM device)
                {
                    ViewModel.SelectedDevice = device;
                    // Find parent branch and panel
                    ViewModel.SelectedBranch = FindParentBranch(device);
                    ViewModel.SelectedPanel = FindParentPanel(ViewModel.SelectedBranch);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in selection changed: {ex.Message}");
            }
        }

        // Note: Inline editing methods removed as WPF TreeView doesn't support this directly
        // These would need to be implemented with custom data templates if inline editing is required

        #endregion

        #region Validation and Drop Logic

        private bool ValidateDragData(DragEventArgs e)
        {
            return e.Data.GetDataPresent("NodeData") && e.Data.GetData("NodeData") is BaseNodeVM;
        }

        private bool IsDraggable(object node)
        {
            // All node types can be dragged
            return node is BaseNodeVM;
        }

        private bool IsValidDropTarget(BaseNodeVM source, object target)
        {
            if (source == null || target == null)
                return false;

            // Prevent dropping on self
            if (source == target)
                return false;

            switch (source)
            {
                case DeviceNodeVM device:
                    // Devices can be dropped on branches or panels
                    return target is BranchNodeVM || target is PanelNodeVM;
                    
                case BranchNodeVM branch:
                    // Branches can be dropped on panels
                    return target is PanelNodeVM && target != FindParentPanel(branch);
                    
                case PanelNodeVM panel:
                    // Panels cannot be dropped (or could be dropped on system root if implemented)
                    return false;
                    
                default:
                    return false;
            }
        }

        private bool PerformDropOperation(BaseNodeVM source, object target)
        {
            if (ViewModel == null)
                return false;

            try
            {
                switch (source)
                {
                    case DeviceNodeVM device when target is BranchNodeVM targetBranch:
                        // Move device to different branch
                        return ViewModel.TryMoveDevice(device.Id, targetBranch.Id);
                        
                    case DeviceNodeVM device when target is PanelNodeVM targetPanel:
                        // Move device to panel (will need to create or select a branch)
                        var firstBranch = targetPanel.Branches.Count > 0 ? targetPanel.Branches[0] : null;
                        if (firstBranch != null)
                        {
                            return ViewModel.TryMoveDevice(device.Id, firstBranch.Id);
                        }
                        else
                        {
                            // Could create new branch automatically or show dialog
                            MessageBox.Show("Target panel has no branches. Add a branch first.", "Drop Target", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            return false;
                        }
                        
                    case BranchNodeVM branch when target is PanelNodeVM targetPanel:
                        // Move branch to different panel
                        return ViewModel.TryMoveBranch(branch.Id, targetPanel.Id);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error performing drop operation: {ex.Message}");
                return false;
            }
        }

        private BaseNodeVM GetTreeNodeAtPoint(TreeView treeView, Point point)
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
                return treeViewItem.DataContext as BaseNodeVM;
            }

            return null;
        }

        private void SelectTreeViewItem(TreeView treeView, object dataItem)
        {
            if (dataItem == null) return;

            // Find the TreeViewItem for the given data item
            var container = FindTreeViewItemContainer(treeView, dataItem);
            if (container != null)
            {
                container.IsSelected = true;
                container.Focus();
            }
        }

        private TreeViewItem FindTreeViewItemContainer(ItemsControl container, object item)
        {
            if (container == null || item == null) return null;

            // Check if any direct child matches
            foreach (var child in container.Items)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                if (childContainer?.DataContext == item)
                    return childContainer;
            }

            // Recursively search in child TreeViewItems
            foreach (var child in container.Items)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                if (childContainer != null)
                {
                    var found = FindTreeViewItemContainer(childContainer, item);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods

        private PanelNodeVM FindParentPanel(BaseNodeVM node)
        {
            if (ViewModel?.Panels == null)
                return null;

            foreach (var panel in ViewModel.Panels)
            {
                if (node is BranchNodeVM branch)
                {
                    if (panel.Branches.Contains(branch))
                        return panel;
                }
                else if (node is DeviceNodeVM device)
                {
                    foreach (var br in panel.Branches)
                    {
                        if (br.Devices.Contains(device))
                            return panel;
                    }
                }
            }
            
            return null;
        }

        private BranchNodeVM FindParentBranch(DeviceNodeVM device)
        {
            if (ViewModel?.Panels == null)
                return null;

            foreach (var panel in ViewModel.Panels)
            {
                foreach (var branch in panel.Branches)
                {
                    if (branch.Devices.Contains(device))
                        return branch;
                }
            }
            
            return null;
        }

        #endregion
    }
}