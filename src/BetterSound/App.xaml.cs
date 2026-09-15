using System;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using BetterSound.Audio;
using BetterSound.Services;
using BetterSound.Views;
using Application = System.Windows.Application;

namespace BetterSound;

/// <summary>
/// No main window: this behaves like a tray-only app the same way BetterSound
/// on macOS hides its Dock icon by default. The tray icon is a WinForms
/// NotifyIcon (WPF has no native one) driving a borderless WPF flyout window.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private NotifyIcon? _trayIcon;
    private MainFlyout? _flyout;
    private AboutWindow? _aboutWindow;
    private SettingsWindow? _settingsWindow;

    public AudioEngine Audio { get; } = new();
    public AppAudioController Apps { get; } = new();
    public UpdateChecker Updates { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "BetterSound.Windows.SingleInstance", out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            // Another instance already holds the mutex, so this process
            // never acquired ownership of it — ReleaseMutex() in OnExit
            // must be skipped, or it throws on a lock we don't hold.
            Shutdown();
            return;
        }

        Services.ThemeHelper.ApplySystemTheme();
        Services.ThemeHelper.ApplySystemAccentColor();

        Audio.Start();
        Apps.Start(Audio);
        Updates.SyncPeriodicChecks(Settings.AutoCheckForUpdates);

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "BetterSound",
            Visible = true,
        };
        _trayIcon.MouseUp += TrayIcon_MouseUp;

        var menu = new ContextMenuStrip();
        menu.Items.Add("BetterSound Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("About BetterSound", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit BetterSound", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;

        _flyout = new MainFlyout(Audio, Apps, Updates, ShowSettings);
    }

    private void TrayIcon_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
        _flyout?.ToggleNearCursor();
    }

    public void ShowSettings()
    {
        _flyout?.Hide();
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(Updates, ShowAbout);
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowAbout()
    {
        if (_aboutWindow is null || !_aboutWindow.IsLoaded)
        {
            _aboutWindow = new AboutWindow();
        }
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // _trayIcon is still null here if OnStartup bailed early because
        // another instance already owns the single-instance mutex.
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Audio.Dispose();
            Apps.Dispose();
        }
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        base.OnExit(e);
    }
}
