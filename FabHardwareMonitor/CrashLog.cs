using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace FabHardwareMonitor;

internal static class CrashLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.SettingsFolderName,
        "error.log");

    public static void Attach(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Write("Dispatcher", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Write("AppDomain", ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("Task", e.Exception);
            e.SetObserved();
        };
    }

    public static void Write(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var text = new StringBuilder()
                .AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}]")
                .AppendLine(exception.ToString())
                .AppendLine()
                .ToString();
            File.AppendAllText(Path, text);
        }
        catch
        {
            // Never throw from the logger.
        }
    }
}
