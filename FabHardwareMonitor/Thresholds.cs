using System.Windows.Media;

namespace FabHardwareMonitor;

public static class Thresholds
{
    public static readonly Color White = Color.FromRgb(0xF4, 0xF1, 0xEA);
    public static readonly Color Amber = Color.FromRgb(0xE6, 0xB3, 0x3C);
    public static readonly Color Red = Color.FromRgb(0xE8, 0x4D, 0x4D);

    public static Color ParseOrDefault(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return White;
        }

        try
        {
            var parsed = ColorConverter.ConvertFromString(hex);
            return parsed is Color color ? color : White;
        }
        catch
        {
            return White;
        }
    }

    public static Color ForUsage(double? percent, Color fallback)
    {
        if (percent is null)
        {
            return fallback;
        }

        if (percent >= 95)
        {
            return Red;
        }

        if (percent >= 80)
        {
            return Amber;
        }

        return fallback;
    }

    public static Color ForTemp(double? celsius, Color fallback)
    {
        if (celsius is null)
        {
            return fallback;
        }

        if (celsius >= 90)
        {
            return Red;
        }

        if (celsius >= 80)
        {
            return Amber;
        }

        return fallback;
    }

    public static string FormatRate(double bytesPerSec)
    {
        var n = Math.Max(0, bytesPerSec);
        return n switch
        {
            < 1024 => $"{n:0.0} B/s",
            < 1024 * 1024 => $"{n / 1024:0.0} K/s",
            < 1024d * 1024 * 1024 => $"{n / (1024 * 1024):0.0} M/s",
            _ => $"{n / (1024d * 1024 * 1024):0.0} G/s"
        };
    }

    public static string FormatPercent(double? value) =>
        value is null ? "--" : $"{Math.Clamp(value.Value, 0, 100):0}%";

    public static string FormatTemp(double? value) =>
        value is null ? "--" : $"{Math.Round(value.Value):0}°C";
}
