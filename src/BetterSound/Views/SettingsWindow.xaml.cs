using System;
using System.Windows;
using BetterSound.Services;

namespace BetterSound.Views;

/// <summary>Windows counterpart to SettingsView.swift. "Show icon in Dock" has no Windows equivalent (no dock), so it's dropped; everything else lines up 1:1.</summary>
public partial class SettingsWindow : Window
{
    private readonly UpdateChecker _updates;
    private readonly Action _openAbout;

    public SettingsWindow(UpdateChecker updates, Action openAbout)
    {
        _updates = updates;
        _openAbout = openAbout;

        InitializeComponent();

        LaunchAtLoginToggle.IsChecked = LoginItemManager.IsEnabled;
        AutoCheckToggle.IsChecked = Settings.AutoCheckForUpdates;
        _updates.PropertyChanged += (_, _) => Dispatcher.Invoke(RefreshUpdateStatus);
        RefreshUpdateStatus();
    }

    private void LaunchAtLoginToggle_Click(object sender, RoutedEventArgs e) =>
        LoginItemManager.SetEnabled(LaunchAtLoginToggle.IsChecked == true);

    private void AutoCheckToggle_Click(object sender, RoutedEventArgs e)
    {
        var enabled = AutoCheckToggle.IsChecked == true;
        Settings.AutoCheckForUpdates = enabled;
        _updates.SyncPeriodicChecks(enabled);
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updates.IsChecking) return;
        await _updates.Check();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e) => _openAbout();

    private void RefreshUpdateStatus()
    {
        if (_updates.AvailableUpdate is { } update)
        {
            UpdateStatusText.Text = $"Update available: v{update.Version} — {update.HtmlUrl}";
        }
        else if (_updates.CheckFailed)
        {
            UpdateStatusText.Text = "Couldn't check for updates";
        }
        else if (_updates.LastCheckedAt is not null)
        {
            UpdateStatusText.Text = $"You're up to date (v{_updates.CurrentVersion})";
        }
        else
        {
            UpdateStatusText.Text = string.Empty;
        }
    }
}
