using Microsoft.Win32;

namespace BetterSound.Services;

/// <summary>Windows counterpart to the Swift app's @AppStorage-backed settings — a couple of HKCU values instead of UserDefaults.</summary>
public static class Settings
{
    private const string KeyPath = @"Software\BetterSound";

    public static bool AutoCheckForUpdates
    {
        get => ReadBool("autoCheckForUpdates", false);
        set => WriteBool("autoCheckForUpdates", value);
    }

    private static bool ReadBool(string name, bool fallback)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var raw = key?.GetValue(name);
        return raw is int i ? i != 0 : fallback;
    }

    private static void WriteBool(string name, bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
    }
}
