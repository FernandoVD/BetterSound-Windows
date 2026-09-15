using System;
using NAudio.CoreAudioApi;

namespace BetterSound.Audio;

/// <summary>Mirrors AudioDevice.swift: a lightweight, immutable snapshot of one endpoint.</summary>
public sealed class AudioDevice
{
    public string Id { get; }
    public string Name { get; }
    public AudioDeviceKind Kind { get; }
    public bool SupportsVolume { get; }
    public bool CanBeDefault { get; }

    private AudioDevice(string id, string name, AudioDeviceKind kind, bool supportsVolume, bool canBeDefault)
    {
        Id = id;
        Name = name;
        Kind = kind;
        SupportsVolume = supportsVolume;
        CanBeDefault = canBeDefault;
    }

    public static AudioDevice From(MMDevice device)
    {
        var name = SafeName(device);
        return new AudioDevice(
            id: device.ID,
            name: name,
            kind: Classify(device, name),
            supportsVolume: TryHasVolume(device),
            canBeDefault: device.State == DeviceState.Active);
    }

    private static string SafeName(MMDevice device)
    {
        try { return device.FriendlyName; }
        catch { return "Unknown device"; }
    }

    private static bool TryHasVolume(MMDevice device)
    {
        try { _ = device.AudioEndpointVolume.MasterVolumeLevelScalar; return true; }
        catch { return false; }
    }

    /// <summary>
    /// Same intent as the Swift classify(): read the endpoint's official
    /// form factor (PKEY_AudioEndpoint_FormFactor, a documented Win32
    /// property — unlike the default-device switch, this one's on solid
    /// ground) and fall back to name heuristics for things Windows doesn't
    /// distinguish itself (e.g. it reports Bluetooth headsets and AirPods
    /// both as plain "Headphones").
    /// </summary>
    private static AudioDeviceKind Classify(MMDevice device, string name)
    {
        var lowerName = name.ToLowerInvariant();
        EndpointFormFactor formFactor = EndpointFormFactor.UnknownFormFactor;
        try
        {
            formFactor = device.Properties[PropertyKeys.PKEY_AudioEndpoint_FormFactor].Value is int raw
                ? (EndpointFormFactor)raw
                : EndpointFormFactor.UnknownFormFactor;
        }
        catch
        {
            // Some virtual/driver endpoints don't publish this key at all.
        }

        if (lowerName.Contains("bluetooth") || lowerName.Contains("airpods"))
        {
            return AudioDeviceKind.Bluetooth;
        }

        if (lowerName.Contains("usb"))
        {
            return AudioDeviceKind.Usb;
        }

        if (lowerName.Contains("hdmi") || lowerName.Contains("displayport") || lowerName.Contains("display port"))
        {
            return AudioDeviceKind.Hdmi;
        }

        return formFactor switch
        {
            EndpointFormFactor.Headphones or EndpointFormFactor.Headset => AudioDeviceKind.BuiltInHeadphones,
            EndpointFormFactor.Speakers => AudioDeviceKind.BuiltInSpeakers,
            _ => AudioDeviceKind.Other,
        };
    }
}
