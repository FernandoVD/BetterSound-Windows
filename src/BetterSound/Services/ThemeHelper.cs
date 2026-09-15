using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace BetterSound.Services;

/// <summary>Reads the Windows 8+ "AppsUseLightTheme" personalization value once at startup and swaps in the dark palette when needed. Doesn't watch for live theme changes — a reasonable v1 corner given there's no Windows box here to eyeball either state.</summary>
public static class ThemeHelper
{
    public static void ApplySystemTheme()
    {
        if (!IsDarkModeEnabled()) return;

        var resources = System.Windows.Application.Current.Resources;
        var dark = new ResourceDictionary { Source = new Uri("Styles/DarkColors.xaml", UriKind.Relative) };

        // The light Colors.xaml dictionary was added first in App.xaml;
        // replace it in place so every DynamicResource/StaticResource
        // lookup made after this point resolves to the dark brushes.
        var merged = resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source is { } source && source.OriginalString.EndsWith("Colors.xaml"))
            {
                merged[i] = dark;
                return;
            }
        }

        merged.Insert(0, dark);
    }

    /// <summary>
    /// Replaces the app's own hardcoded blue AccentBrush with the user's
    /// actual Windows accent color, read from the same registry value
    /// Settings > Personalization > Colors writes to. Falls back to
    /// whatever Colors.xaml/DarkColors.xaml already defines if the key is
    /// missing or malformed — this is a well-known but undocumented value,
    /// so treat a failure here as "stays the hardcoded color," not a crash.
    /// </summary>
    public static void ApplySystemAccentColor()
    {
        if (TryGetAccentColor(out var color))
        {
            System.Windows.Application.Current.Resources["AccentBrush"] = new SolidColorBrush(color);
        }
    }

    private static bool TryGetAccentColor(out Color color)
    {
        color = default;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is not int raw)
            {
                return false;
            }

            // Stored as 0xAABBGGRR rather than the usual 0xAARRGGBB — a
            // long-documented quirk of this specific value, not a typo.
            var value = unchecked((uint)raw);
            var a = (byte)((value >> 24) & 0xFF);
            var b = (byte)((value >> 16) & 0xFF);
            var g = (byte)((value >> 8) & 0xFF);
            var r = (byte)(value & 0xFF);

            if (a == 0)
            {
                return false;
            }

            color = Color.FromArgb(a, r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDarkModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
