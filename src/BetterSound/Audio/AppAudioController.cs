using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace BetterSound.Audio;

/// <summary>
/// Windows counterpart to PerAppAudioController.swift. Where macOS has no
/// public "set this app's volume" API and needs a from-scratch Core Audio
/// Process Tap engine (ProcessTapEngine.swift) to fake one, Windows has
/// shipped real per-app session volume natively since Vista
/// (IAudioSessionManager2 / ISimpleAudioVolume, both documented public
/// WASAPI interfaces) — so this is a thin, boring wrapper by comparison.
///
/// Per-app *output device* routing (send one app to a different device than
/// everything else) is intentionally not implemented: Windows' own "App
/// volume and device preferences" panel does this through a second
/// undocumented internal mechanism beyond IPolicyConfig, and guessing at
/// that without a Windows machine to verify against felt like the wrong
/// tradeoff. See the README's Known limitations section.
/// </summary>
public sealed class AppAudioController : IDisposable
{
    private readonly Dictionary<string, AppAudioItem> _itemsByKey = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private MMDeviceEnumerator? _enumerator;

    public ObservableCollection<AppAudioItem> Items { get; } = new();

    public void Start(AudioEngine audio)
    {
        _enumerator = new MMDeviceEnumerator();
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();
        Refresh();
    }

    public void ToggleMute(AppAudioItem item)
    {
        var newValue = !item.IsMuted;
        foreach (var session in item.Sessions)
        {
            session.SimpleAudioVolume.Mute = newValue;
        }
        item.IsMuted = newValue;
        item.RaiseChanged(nameof(AppAudioItem.IsMuted));
        item.RaiseChanged(nameof(AppAudioItem.EffectiveVolume));
    }

    public void SetVolume(AppAudioItem item, double value)
    {
        var clamped = (float)Math.Clamp(value, 0.0, 1.0);
        foreach (var session in item.Sessions)
        {
            session.SimpleAudioVolume.Volume = clamped;
            if (clamped > 0 && session.SimpleAudioVolume.Mute)
            {
                session.SimpleAudioVolume.Mute = false;
            }
        }
        item.Volume = clamped;
        item.IsMuted = false;
        item.RaiseChanged(nameof(AppAudioItem.Volume));
        item.RaiseChanged(nameof(AppAudioItem.IsMuted));
        item.RaiseChanged(nameof(AppAudioItem.EffectiveVolume));
    }

    private void Refresh()
    {
        // Deliberately not disposed: the AudioSessionControl objects handed
        // out below get held onto in AppAudioItem.Sessions between ticks,
        // and releasing the parent MMDevice's COM pointer here risks
        // invalidating them along with it. A stray MMDevice wrapper every
        // 1.5s is a smaller cost than a slider that silently stops working.
        MMDevice defaultOutput;
        try
        {
            defaultOutput = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return;
        }

        var grouped = new Dictionary<string, List<AudioSessionControl>>();
        var sessions = defaultOutput.AudioSessionManager.Sessions;
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (session.IsSystemSoundsSession) continue;
            if (session.State == AudioSessionState.AudioSessionStateExpired) continue;

            var key = KeyFor(session);
            if (key is null) continue;

            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<AudioSessionControl>();
                grouped[key] = list;
            }
            list.Add(session);
        }

        var seenKeys = new HashSet<string>(grouped.Keys);

        foreach (var key in _itemsByKey.Keys.Except(seenKeys).ToList())
        {
            var stale = _itemsByKey[key];
            _itemsByKey.Remove(key);
            Items.Remove(stale);
        }

        foreach (var (key, sessionList) in grouped)
        {
            var first = sessionList[0];
            var volume = first.SimpleAudioVolume.Volume;
            var isMuted = first.SimpleAudioVolume.Mute;

            if (_itemsByKey.TryGetValue(key, out var existing))
            {
                existing.Sessions.Clear();
                existing.Sessions.AddRange(sessionList);
                var changed = false;
                if (Math.Abs(existing.Volume - volume) > 0.001)
                {
                    existing.Volume = volume;
                    existing.RaiseChanged(nameof(AppAudioItem.Volume));
                    changed = true;
                }
                if (existing.IsMuted != isMuted)
                {
                    existing.IsMuted = isMuted;
                    existing.RaiseChanged(nameof(AppAudioItem.IsMuted));
                    changed = true;
                }
                if (changed)
                {
                    existing.RaiseChanged(nameof(AppAudioItem.EffectiveVolume));
                }
                continue;
            }

            var (name, icon) = DescribeProcess(key);
            var item = new AppAudioItem(this, key, name, icon, sessionList, volume, isMuted);
            _itemsByKey[key] = item;
            Items.Add(item);
        }
    }

    private static string? KeyFor(AudioSessionControl session)
    {
        try
        {
            var pid = (int)session.GetProcessID;
            if (pid <= 0) return null;
            using var process = Process.GetProcessById(pid);
            var path = TryGetPath(process);
            return path ?? process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetPath(Process process)
    {
        try { return process.MainModule?.FileName; }
        catch { return null; }
    }

    private static (string Name, ImageSource? Icon) DescribeProcess(string key)
    {
        var name = Path.GetFileNameWithoutExtension(key);
        ImageSource? icon = null;

        try
        {
            if (File.Exists(key))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(key);
                if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                {
                    name = versionInfo.FileDescription!;
                }

                using var associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(key);
                if (associatedIcon is not null)
                {
                    icon = Imaging.CreateBitmapSourceFromHIcon(
                        associatedIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    icon.Freeze();
                }
            }
        }
        catch
        {
            // Some system/elevated processes refuse icon/version access; the
            // process name fallback above is good enough for those rows.
        }

        return (name, icon);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _enumerator?.Dispose();
    }
}
