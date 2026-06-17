using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace SharpTimer.App.Views.Timing;

public sealed partial class TimerView : UserControl
{
    public TimerView()
    {
        InitializeComponent();
    }

    public event EventHandler? BluetoothButtonClicked;
    public event EventHandler? BluetoothFlyoutOpened;
    public event EventHandler? BluetoothFlyoutClosed;
    public event EventHandler? BluetoothRetryScanRequested;
    public event ItemClickEventHandler? BluetoothDeviceClicked;
    public event EventHandler? ResetCubeStateRequested;
    public event EventHandler? ResetCubeOrientationRequested;
    public event EventHandler? DisconnectCubeRequested;

    public void GoToImmersionState(string stateName, bool useTransitions)
    {
        VisualStateManager.GoToState(this, stateName, useTransitions);
    }

    private void BluetoothButton_Click(object sender, RoutedEventArgs e)
    {
        BluetoothButtonClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BluetoothFlyout_Opened(object sender, object e)
    {
        BluetoothFlyoutOpened?.Invoke(this, EventArgs.Empty);
    }

    private void BluetoothFlyout_Closed(object sender, object e)
    {
        BluetoothFlyoutClosed?.Invoke(this, EventArgs.Empty);
    }

    private void BluetoothRetryScanButton_Click(object sender, RoutedEventArgs e)
    {
        BluetoothRetryScanRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BluetoothDevicesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        BluetoothDeviceClicked?.Invoke(sender, e);
    }

    private void ResetCubeStateButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCubeStateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetCubeOrientationButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCubeOrientationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DisconnectCubeButton_Click(object sender, RoutedEventArgs e)
    {
        DisconnectCubeRequested?.Invoke(this, EventArgs.Empty);
    }
}
