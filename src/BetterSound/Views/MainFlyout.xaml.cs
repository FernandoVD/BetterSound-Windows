using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using BetterSound.Audio;

namespace BetterSound.Views;

/// <summary>Windows counterpart to MenuBarContentView.swift: a borderless popup anchored near the tray icon instead of a menu-bar-extra popover.</summary>
public partial class MainFlyout : Window
{
    private readonly Action _openSettings;

    public AudioEngine Audio { get; }
    public AppAudioController Apps { get; }
    public Services.UpdateChecker Updates { get; }

    public MainFlyout(AudioEngine audio, AppAudioController apps, Services.UpdateChecker updates, Action openSettings)
    {
        Audio = audio;
        Apps = apps;
        Updates = updates;
        _openSettings = openSettings;

        InitializeComponent();
        DataContext = this;
    }

    public void ToggleNearCursor()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        var cursor = System.Windows.Forms.Cursor.Position;
        var workArea = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;

        // Anchor above-right of the click point, like Windows' own Quick
        // Settings flyout anchors above the tray icon near the taskbar.
        Show();
        UpdateLayout();
        Left = Math.Min(cursor.X, workArea.Right - ActualWidth - 8);
        Top = workArea.Bottom - ActualHeight - 8;
        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void MasterMuteButton_Click(object sender, RoutedEventArgs e) => Audio.ToggleMute();

    private void InputMuteButton_Click(object sender, RoutedEventArgs e) => Audio.ToggleInputMute();

    private void AppIcon_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AppAudioItem item)
        {
            item.ToggleMute();
        }
    }

    private void OutputDeviceButton_Click(object sender, RoutedEventArgs e) =>
        ShowDeviceMenu((System.Windows.Controls.Button)sender, Audio.Devices, Audio.DefaultDeviceId, Audio.SelectDevice);

    private void InputDeviceButton_Click(object sender, RoutedEventArgs e) =>
        ShowDeviceMenu((System.Windows.Controls.Button)sender, Audio.InputDevices, Audio.DefaultInputDeviceId, Audio.SelectInputDevice);

    private static void ShowDeviceMenu(
        System.Windows.Controls.Button anchor,
        System.Collections.Generic.IEnumerable<AudioDevice> devices,
        string? currentId,
        Action<AudioDevice> select)
    {
        var menu = new ContextMenu { PlacementTarget = anchor };

        // Devices that can't be a system default (some virtual drivers) are
        // left out entirely, same as the macOS picker — see AudioDevice.CanBeDefault.
        foreach (var device in devices)
        {
            if (!device.CanBeDefault) continue;

            var item = new MenuItem { Header = device.Name, IsCheckable = true, IsChecked = device.Id == currentId };
            item.Click += (_, _) => select(device);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => _openSettings();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
