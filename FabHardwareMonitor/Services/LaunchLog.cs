namespace FabHardwareMonitor.Services;

internal static class LaunchLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.SettingsFolderName,
        "launch.log");

    public static void Write(string message)
    {
        LogFile.Append(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
    }
}
