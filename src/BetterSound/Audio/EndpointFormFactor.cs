namespace BetterSound.Audio;

/// <summary>
/// NAudio doesn't define this itself (there's no EndpointFormFactor.cs
/// anywhere in NAudio.CoreAudioApi) — it only wraps the raw property store,
/// so this is a direct copy of the native mmdeviceapi.h enum values that
/// PKEY_AudioEndpoint_FormFactor's PROPVARIANT actually contains.
/// </summary>
internal enum EndpointFormFactor
{
    RemoteNetworkDevice = 0,
    Speakers = 1,
    LineLevel = 2,
    Headphones = 3,
    Microphone = 4,
    Headset = 5,
    Handset = 6,
    UnknownDigitalPassthrough = 7,
    SPDIF = 8,
    DigitalAudioDisplayDevice = 9,
    UnknownFormFactor = 10,
}
