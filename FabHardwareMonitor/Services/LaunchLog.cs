using System.IO;
using System.Text;

namespace FabHardwareMonitor.Services;

internal static class LaunchLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.SettingsFolderName,
        "launch.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var text = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append(' ')
                .AppendLine(message)
                .ToString();
            File.AppendAllText(Path, text);
        }
        catch
        {
            // Never throw from the logger.
        }
    }
}
