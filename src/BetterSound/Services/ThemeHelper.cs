using System;
using System.Windows;
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
