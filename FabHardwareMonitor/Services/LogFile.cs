using System.Globalization;
using System.IO;
using System.Text;

namespace FabHardwareMonitor.Services;

internal static class LogFile
{
    private static readonly TimeSpan Keep = TimeSpan.FromDays(2);
    private static readonly HashSet<string> Trimmed = new(StringComparer.OrdinalIgnoreCase);
    private const long MaxBytes = 2 * 1024 * 1024;

    public static void Append(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            TrimOnce(path);
            File.AppendAllText(path, text);
        }
        catch
        {
            // Never throw from the logger.
        }
    }

    public static void TrimOnce(string path)
    {
        lock (Trimmed)
        {
            if (!Trimmed.Add(path))
            {
                return;
            }
        }

        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                File.Delete(path);
                return;
            }

            var lines = File.ReadAllLines(path);
            var cutoff = DateTime.Now - Keep;
            var keepFrom = lines.Length;
            for (var i = 0; i < lines.Length; i++)
            {
                if (TryStamp(lines[i], out var stamp) && stamp >= cutoff)
                {
                    keepFrom = i;
                    break;
                }
            }

            if (keepFrom == 0)
            {
                return;
            }

            if (keepFrom >= lines.Length)
            {
                File.WriteAllText(path, "");
                return;
            }

            var kept = new StringBuilder();
            for (var i = keepFrom; i < lines.Length; i++)
            {
                kept.AppendLine(lines[i]);
            }

            File.WriteAllText(path, kept.ToString());
        }
        catch
        {
            // Leave the file as-is if trim fails.
        }
    }

    private static bool TryStamp(string line, out DateTime stamp)
    {
        stamp = default;
        if (line.Length < 19)
        {
            return false;
        }

        return DateTime.TryParseExact(
            line.AsSpan(0, 19),
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out stamp);
    }
}
