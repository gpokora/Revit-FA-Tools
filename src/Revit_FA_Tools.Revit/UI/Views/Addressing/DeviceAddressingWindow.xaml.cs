using System;
using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using Revit_FA_Tools.ViewModels.Addressing;
using Revit_FA_Tools.Models.Addressing;

namespace Revit_FA_Tools.Views.Addressing
{
    /// <summary>
    /// Interaction logic for DeviceAddressingWindow.xaml
    /// </summary>
    public partial class DeviceAddressingWindow : ThemedWindow
    {
        private AddressingViewModel _viewModel;
        private SmartDeviceNode _draggedDevice;
        
        public DeviceAddressingWindow()
        {
            InitializeComponent();
            InitializeViewModel();
        }

        public DeviceAddressingWindow(AddressingViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void InitializeViewModel()
        {
            if (_viewModel == null)
            {
                _viewModel = new AddressingViewModel();
                DataContext = _viewModel;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            base.OnClosed(e);
        }

        #region Drag & Drop Event Handlers

        private void OnTreeMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is TreeListControl treeList)
            {
                try
                {
                    var hitInfo = treeList.View.CalcHitInfo(e.GetPosition(treeList));
                    if (hitInfo.InRowCell && hitInfo.RowHandle >= 0)
                    {
                        var device = treeList.GetRow(hitInfo.RowHandle) as SmartDeviceNode;
                        if (device != null && !device.IsAddressLocked)
                        {
                            _draggedDevice = device;
                            DragDrop.DoDragDrop(treeList, device, DragDropEffects.Move);
                        }
                    }
                }
                catch
                {
                    // Handle DevExpress API compatibility issues
                }
            }
        }

        private void OnTreeDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            
            if (e.Data.GetDataPresent(typeof(SmartDeviceNode)) && sender is TreeListControl treeList)
            {
                try
                {
                    var hitInfo = treeList.View.CalcHitInfo(e.GetPosition(treeList));
                    var dropTarget = GetDropTarget(hitInfo, treeList);
                    
                    if (dropTarget != null)
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                }
                catch
                {
                    // Handle DevExpress API compatibility issues
                }
            }
            
            e.Handled = true;
        }

        // CRITICAL: This method implements the core addressing behavior
        private void OnTreeDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SmartDeviceNode)) && sender is TreeListControl treeList)
            {
                try
                {
                    var draggedDevice = (SmartDeviceNode)e.Data.GetData(typeof(SmartDeviceNode));
                    var hitInfo = treeList.View.CalcHitInfo(e.GetPosition(treeList));
                    var dropTarget = GetDropTarget(hitInfo, treeList);
                    var dropPosition = GetDropPosition(hitInfo, treeList);
                
                if (dropTarget is AddressingCircuit targetCircuit)
                {
                    // SCENARIO 1: Add device to circuit from pool
                    HandleAddDeviceToCircuit(draggedDevice, targetCircuit, dropPosition);
                }
                else if (dropTarget is SmartDeviceNode targetDevice && 
                        draggedDevice.ParentCircuit == targetDevice.ParentCircuit)
                {
                    // SCENARIO 2: CRITICAL - Physical reordering within circuit
                    HandlePhysicalReordering(draggedDevice, targetDevice, dropPosition);
                }
                else if (IsDropOutsideCircuit(hitInfo))
                {
                    // SCENARIO 3: Remove device from circuit
                    HandleRemoveFromCircuit(draggedDevice);
                }
                }
                catch
                {
                    // Handle DevExpress API compatibility issues
                }
            }
            
            e.Handled = true;
        }

        // CRITICAL IMPLEMENTATION: Physical reordering preserves addresses
        private void HandlePhysicalReordering(SmartDeviceNode draggedDevice, SmartDeviceNode targetDevice, int newPosition)
        {
            var circuit = draggedDevice.ParentCircuit;
            var oldPosition = draggedDevice.PhysicalPosition;
            var preservedAddress = draggedDevice.AssignedAddress; // CRITICAL: Preserve address
            
            // ONLY change physical position
            circuit.ReorderDevicePhysically(draggedDevice, newPosition);
            
            // VERIFY: Address must remain unchanged
            if (draggedDevice.AssignedAddress != preservedAddress)
            {
                throw new InvalidOperationException("CRITICAL ERROR: Address changed during physical reordering!");
            }
            
            _viewModel.StatusMessage = $"Device '{draggedDevice.DeviceName}' moved from position {oldPosition} to {newPosition} - Address {preservedAddress} UNCHANGED";
        }

        // CRITICAL IMPLEMENTATION: Device removal returns address to pool
        private void HandleRemoveFromCircuit(SmartDeviceNode device)
        {
            var oldCircuit = device.ParentCircuit?.Name ?? "Unknown";
            var oldAddress = device.AssignedAddress;
            
            // CRITICAL: Remove from circuit automatically returns address to pool
            device.ParentCircuit?.RemoveDevice(device);
            
            // Add back to available devices
            if (!_viewModel.AvailableDevices.Contains(device))
            {
                _viewModel.AvailableDevices.Add(device);
            }
            
            // Update status with clear feedback
            _viewModel.StatusMessage = oldAddress.HasValue 
                ? $"Device removed from {oldCircuit} - Address {oldAddress} RETURNED TO POOL"
                : $"Device removed from {oldCircuit}";
        }

        // Add device to circuit (starts unaddressed)
        private void HandleAddDeviceToCircuit(SmartDeviceNode device, AddressingCircuit circuit, int position)
        {
            // Remove from available devices if it's there
            if (_viewModel.AvailableDevices.Contains(device))
            {
                _viewModel.AvailableDevices.Remove(device);
            }
            
            // Remove from previous circuit if assigned
            if (device.ParentCircuit != null && device.ParentCircuit != circuit)
            {
                device.ParentCircuit.RemoveDevice(device);
            }
            
            // Add to new circuit - device starts UNADDRESSED
            circuit.AddDevice(device, position);
            
            _viewModel.StatusMessage = $"Device '{device.DeviceName}' added to {circuit.Name} at position {position} - NO ADDRESS assigned";
        }

        // Helper methods
        private object GetDropTarget(TreeListViewHitInfo hitInfo, TreeListControl treeList)
        {
            try
            {
                if (hitInfo.InRowCell && hitInfo.RowHandle >= 0)
                {
                    return treeList.GetRow(hitInfo.RowHandle);
                }
            }
            catch
            {
                // Handle DevExpress API compatibility issues
            }
            return null;
        }

        private int GetDropPosition(TreeListViewHitInfo hitInfo, TreeListControl treeList)
        {
            try
            {
                if (hitInfo.InRowCell && hitInfo.RowHandle >= 0)
                {
                    var targetItem = treeList.GetRow(hitInfo.RowHandle);
                    if (targetItem is SmartDeviceNode device)
                    {
                        return device.PhysicalPosition;
                    }
                    else if (targetItem is AddressingCircuit circuit)
                    {
                        return circuit.Devices.Count + 1;
                    }
                }
            }
            catch
            {
                // Handle DevExpress API compatibility issues
            }
            return 1;
        }

        private bool IsDropOutsideCircuit(TreeListViewHitInfo hitInfo)
        {
            // TODO: Implement logic to detect drop outside circuit bounds
            return false;
        }

        // Address editing event handler
        private void OnAddressChanged(object sender, TreeListCellValueChangedEventArgs e)
        {
            if (e.Column.FieldName == "AssignedAddress" && e.Row is SmartDeviceNode device)
            {
                var newAddress = e.Value as int?;
                if (newAddress.HasValue)
                {
                    if (!_viewModel.TryAssignAddress(device, newAddress.Value))
                    {
                        // Revert if assignment failed
                        e.Handled = true;
                        // TODO: Show validation error
                    }
                }
            }
        }

        #endregion
    }
}