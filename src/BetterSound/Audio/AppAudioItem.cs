using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using NAudio.CoreAudioApi;

namespace BetterSound.Audio;

/// <summary>
/// One row in the Applications section. Unlike the macOS build (which
/// groups by running app because CoreAudio's process tap is one-per-app),
/// this groups by executable path: an app that spawns several audio-capable
/// processes (Chrome's per-tab renderers are the common case) shows as a
/// single row, and moving its slider applies to every session that belongs
/// to it, same net effect as macOS's one-row-per-app.
/// </summary>
public sealed class AppAudioItem : INotifyPropertyChanged
{
    private readonly AppAudioController _owner;

    public string Key { get; }
    public string Name { get; }
    public ImageSource? Icon { get; }
    internal List<AudioSessionControl> Sessions { get; }

    public double Volume { get; internal set; }
    public bool IsMuted { get; internal set; }

    /// <summary>Same read-0-while-muted / write-through-the-controller pattern as AudioEngine.EffectiveMasterVolume — see that property's doc comment for why a plain two-way binding is safe here.</summary>
    public double EffectiveVolume
    {
        get => IsMuted ? 0.0 : Volume;
        set => _owner.SetVolume(this, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppAudioItem(AppAudioController owner, string key, string name, ImageSource? icon, List<AudioSessionControl> sessions, double volume, bool isMuted)
    {
        _owner = owner;
        Key = key;
        Name = name;
        Icon = icon;
        Sessions = sessions;
        Volume = volume;
        IsMuted = isMuted;
    }

    public void ToggleMute() => _owner.ToggleMute(this);

    internal void RaiseChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
