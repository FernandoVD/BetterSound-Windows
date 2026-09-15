using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace BetterSound.Audio;

/// <summary>
/// Windows counterpart to AudioEngine.swift: mirrors Core Audio HAL state
/// into bindable properties, driven by NAudio's IMMNotificationClient +
/// per-device volume-change callbacks instead of polling.
/// </summary>
public sealed class AudioEngine : INotifyPropertyChanged, IDisposable, IMMNotificationClient
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _defaultOutput;
    private MMDevice? _defaultInput;

    public ObservableCollection<AudioDevice> Devices { get; } = new();
    public ObservableCollection<AudioDevice> InputDevices { get; } = new();

    public string? DefaultDeviceId => _defaultOutput?.ID;
    public string? DefaultInputDeviceId => _defaultInput?.ID;

    public bool SupportsMasterVolume { get; private set; }
    public bool IsMuted { get; private set; }
    public double MasterVolume { get; private set; }

    public bool SupportsInputVolume { get; private set; }
    public bool IsInputMuted { get; private set; }
    public double InputVolume { get; private set; }

    /// <summary>
    /// Bound to the master slider: reads as 0 while muted (matching the
    /// Swift UI's `isMuted ? 0 : masterVolume` trick) but writes always go
    /// through SetMasterVolume. A plain two-way binding to this — rather
    /// than a multi-value converter — is what makes that safe: WPF only
    /// invokes a binding's setter when the *target* (the Slider) changes,
    /// never when we raise PropertyChanged from this side, so toggling
    /// mute elsewhere can't loop back and zero out the real volume level.
    /// </summary>
    public double EffectiveMasterVolume
    {
        get => IsMuted ? 0.0 : MasterVolume;
        set => SetMasterVolume(value);
    }

    public double EffectiveInputVolume
    {
        get => IsInputMuted ? 0.0 : InputVolume;
        set => SetInputVolume(value);
    }

    public AudioDevice? CurrentOutputDevice => Devices.FirstOrDefault(d => d.Id == DefaultDeviceId);
    public AudioDevice? CurrentInputDevice => InputDevices.FirstOrDefault(d => d.Id == DefaultInputDeviceId);
    public string CurrentOutputGlyph => (CurrentOutputDevice?.Kind ?? AudioDeviceKind.Other).Glyph();
    public string CurrentOutputName => CurrentOutputDevice?.Name ?? "This device";
    public string CurrentInputName => CurrentInputDevice?.Name ?? "This device";

    public string MuteGlyph => IsMuted ? "" : "";
    public string InputMuteGlyph => IsInputMuted ? "" : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Start()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
        RefreshDeviceLists();
        BindDefaultOutput();
        BindDefaultInput();
    }

    // MARK: - Master (output) volume

    public void ToggleMute()
    {
        if (_defaultOutput is null) return;
        _defaultOutput.AudioEndpointVolume.Mute = !_defaultOutput.AudioEndpointVolume.Mute;
    }

    public void SetMasterVolume(double value)
    {
        if (_defaultOutput is null) return;
        _defaultOutput.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(value, 0.0, 1.0);
        if (value > 0 && _defaultOutput.AudioEndpointVolume.Mute)
        {
            _defaultOutput.AudioEndpointVolume.Mute = false;
        }
    }

    public void SelectDevice(AudioDevice device)
    {
        using var target = _enumerator.GetDevice(device.Id);
        DefaultDeviceInterop.SetAsDefault(target);
        // OnDefaultDeviceChanged will fire and rebind; no need to do it here too.
    }

    // MARK: - Input volume

    public void ToggleInputMute()
    {
        if (_defaultInput is null) return;
        _defaultInput.AudioEndpointVolume.Mute = !_defaultInput.AudioEndpointVolume.Mute;
    }

    public void SetInputVolume(double value)
    {
        if (_defaultInput is null) return;
        _defaultInput.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(value, 0.0, 1.0);
        if (value > 0 && _defaultInput.AudioEndpointVolume.Mute)
        {
            _defaultInput.AudioEndpointVolume.Mute = false;
        }
    }

    public void SelectInputDevice(AudioDevice device)
    {
        using var target = _enumerator.GetDevice(device.Id);
        DefaultDeviceInterop.SetAsDefault(target);
    }

    // MARK: - Internal wiring

    private void RefreshDeviceLists()
    {
        RunOnUi(() =>
        {
            Devices.Clear();
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                Devices.Add(AudioDevice.From(d));
            }

            InputDevices.Clear();
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                InputDevices.Add(AudioDevice.From(d));
            }

            RaiseAll(nameof(CurrentOutputDevice), nameof(CurrentOutputGlyph), nameof(CurrentOutputName),
                nameof(CurrentInputDevice), nameof(CurrentInputName));
        });
    }

    private void BindDefaultOutput()
    {
        if (_defaultOutput is not null)
        {
            _defaultOutput.AudioEndpointVolume.OnVolumeNotification -= OnOutputVolumeChanged;
        }

        try
        {
            _defaultOutput = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _defaultOutput.AudioEndpointVolume.OnVolumeNotification += OnOutputVolumeChanged;
            SupportsMasterVolume = true;
            IsMuted = _defaultOutput.AudioEndpointVolume.Mute;
            MasterVolume = _defaultOutput.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        catch
        {
            _defaultOutput = null;
            SupportsMasterVolume = false;
        }

        RunOnUi(() =>
        {
            RaiseAll(nameof(DefaultDeviceId), nameof(SupportsMasterVolume), nameof(IsMuted), nameof(MasterVolume), nameof(EffectiveMasterVolume), nameof(MuteGlyph),
                nameof(CurrentOutputDevice), nameof(CurrentOutputGlyph), nameof(CurrentOutputName));
        });
    }

    private void BindDefaultInput()
    {
        if (_defaultInput is not null)
        {
            _defaultInput.AudioEndpointVolume.OnVolumeNotification -= OnInputVolumeChanged;
        }

        try
        {
            _defaultInput = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            _defaultInput.AudioEndpointVolume.OnVolumeNotification += OnInputVolumeChanged;
            SupportsInputVolume = true;
            IsInputMuted = _defaultInput.AudioEndpointVolume.Mute;
            InputVolume = _defaultInput.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        catch
        {
            _defaultInput = null;
            SupportsInputVolume = false;
        }

        RunOnUi(() =>
        {
            RaiseAll(nameof(DefaultInputDeviceId), nameof(SupportsInputVolume), nameof(IsInputMuted), nameof(InputVolume), nameof(EffectiveInputVolume), nameof(InputMuteGlyph),
                nameof(CurrentInputDevice), nameof(CurrentInputName));
        });
    }

    private void OnOutputVolumeChanged(AudioVolumeNotificationData data)
    {
        RunOnUi(() =>
        {
            IsMuted = data.Muted;
            MasterVolume = data.MasterVolume;
            RaiseAll(nameof(IsMuted), nameof(MasterVolume), nameof(EffectiveMasterVolume), nameof(MuteGlyph));
        });
    }

    private void OnInputVolumeChanged(AudioVolumeNotificationData data)
    {
        RunOnUi(() =>
        {
            IsInputMuted = data.Muted;
            InputVolume = data.MasterVolume;
            RaiseAll(nameof(IsInputMuted), nameof(InputVolume), nameof(EffectiveInputVolume), nameof(InputMuteGlyph));
        });
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private void RaiseAll(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        if (_defaultOutput is not null) _defaultOutput.AudioEndpointVolume.OnVolumeNotification -= OnOutputVolumeChanged;
        if (_defaultInput is not null) _defaultInput.AudioEndpointVolume.OnVolumeNotification -= OnInputVolumeChanged;
        _enumerator.Dispose();
    }

    // MARK: - IMMNotificationClient

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) => RefreshDeviceLists();

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) => RefreshDeviceLists();

    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => RefreshDeviceLists();

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (role != Role.Multimedia) return;
        if (flow == DataFlow.Render) BindDefaultOutput();
        else if (flow == DataFlow.Capture) BindDefaultInput();
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }
}
