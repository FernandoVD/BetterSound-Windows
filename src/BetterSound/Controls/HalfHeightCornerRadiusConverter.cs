using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BetterSound.Controls;

/// <summary>Turns a slider's own height into a CornerRadius half that big, so the track is a true capsule regardless of which height style (20px master, 12px per-app) is applied.</summary>
public sealed class HalfHeightCornerRadiusConverter : IValueConverter
{
    public static readonly HalfHeightCornerRadiusConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var height = value is double d && d > 0 ? d : 20.0;
        var radius = height / 2.0;
        return new CornerRadius(radius);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
