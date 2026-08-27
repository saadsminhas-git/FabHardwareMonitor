using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace FabHardwareMonitor;

/// <summary>
/// WPF has no letter-spacing, so short uppercase labels get thin spaces woven
/// between their characters to get the tracked, editorial look.
/// </summary>
public sealed class TrackingConverter : IValueConverter
{
    private const char ThinSpace = '\u2009';

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length * 2);
        foreach (var character in text.ToUpperInvariant())
        {
            builder.Append(character);
            builder.Append(ThinSpace);
        }

        return builder.ToString(0, builder.Length - 1);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
