using System.Windows.Media;

namespace FabHardwareMonitor;

public readonly record struct ReadoutPalette(Color Value, Color Label, Color Warn, Color Hot, Color Plate);

public static class Thresholds
{
    public static readonly Color White = Color.FromRgb(0xF4, 0xF1, 0xEA);
    public static readonly Color Ink = Color.FromRgb(0x1C, 0x19, 0x17);
    public static readonly Color Accent = Color.FromRgb(0xE6, 0x1C, 0x1C);

    /// <summary>Opaque Windows 11 dark taskbar fill (not a layered window).</summary>
    public static readonly Color DarkShell = Color.FromRgb(0x20, 0x20, 0x20);

    /// <summary>Opaque Windows 11 light taskbar fill.</summary>
    public static readonly Color LightShell = Color.FromRgb(0xF3, 0xF3, 0xF3);

    /// <summary>Readable on a dark Windows taskbar.</summary>
    public static readonly ReadoutPalette DarkTaskbar = new(
        Value: White,
        Label: Color.FromRgb(0xB8, 0xB3, 0xA8),
        Warn: Color.FromRgb(0xE6, 0xB3, 0x3C),
        Hot: Color.FromRgb(0xE8, 0x4D, 0x4D),
        Plate: Color.FromArgb(0xB8, 0x18, 0x18, 0x1C));

    /// <summary>Readable on a light Windows taskbar. Amber/red are darkened so they still clear 4.5:1.</summary>
    public static readonly ReadoutPalette LightTaskbar = new(
        Value: Ink,
        Label: Color.FromRgb(0x5C, 0x58, 0x52),
        Warn: Color.FromRgb(0x9A, 0x4F, 0x00),
        Hot: Color.FromRgb(0xC4, 0x16, 0x16),
        Plate: Color.FromArgb(0xC8, 0xF6, 0xF4, 0xF1));

    public static ReadoutPalette ForTaskbar(bool light) => light ? LightTaskbar : DarkTaskbar;

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

    public static Color ForUsage(double? percent, ReadoutPalette palette)
    {
        if (percent is null)
        {
            return palette.Value;
        }

        if (percent >= 95)
        {
            return palette.Hot;
        }

        if (percent >= 80)
        {
            return palette.Warn;
        }

        return palette.Value;
    }

    public static Color ForTemp(double? celsius, ReadoutPalette palette)
    {
        if (celsius is null)
        {
            return palette.Value;
        }

        if (celsius >= 90)
        {
            return palette.Hot;
        }

        if (celsius >= 80)
        {
            return palette.Warn;
        }

        return palette.Value;
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
        value is > 0 ? $"{Math.Round(value.Value):0}°C" : "--";
}
