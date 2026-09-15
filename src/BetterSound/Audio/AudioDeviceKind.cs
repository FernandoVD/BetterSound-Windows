namespace BetterSound.Audio;

/// <summary>
/// Mirrors AudioKind from the macOS app's AudioDevice.swift — same
/// categories, mapped to Segoe Fluent Icons glyphs instead of SF Symbols.
/// </summary>
public enum AudioDeviceKind
{
    BuiltInSpeakers,
    BuiltInHeadphones,
    Bluetooth,
    Usb,
    Hdmi,
    Other,
}

public static class AudioDeviceKindExtensions
{
    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets glyph, for the Output row.</summary>
    public static string Glyph(this AudioDeviceKind kind) => kind switch
    {
        AudioDeviceKind.BuiltInSpeakers => "",   // Speakers
        AudioDeviceKind.BuiltInHeadphones => "", // Headphone
        AudioDeviceKind.Bluetooth => "",         // Bluetooth
        AudioDeviceKind.Usb => "",                // USB
        AudioDeviceKind.Hdmi => "",               // TV / display
        _ => "",                                  // generic speaker/volume
    };

    /// <summary>Simplified glyph set for the Input row — matches the Swift app's inputSymbolName.</summary>
    public static string InputGlyph(this AudioDeviceKind kind) => ""; // Microphone
}
