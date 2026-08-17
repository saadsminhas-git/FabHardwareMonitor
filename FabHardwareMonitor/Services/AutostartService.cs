using System.Diagnostics;
using System.IO;

namespace FabHardwareMonitor.Services;

public sealed class AutostartService
{
    public void Apply(bool enabled)
    {
        if (enabled)
        {
            Register();
        }
        else
        {
            Unregister();
        }
    }

    public void Register()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            return;
        }

        RunSchtasks(
            $"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{AppConstants.TaskName}\" /TR \"\\\"{exe}\\\"\" /DELAY 0000:15");
    }

    public void Unregister()
    {
        RunSchtasks($"/Delete /F /TN \"{AppConstants.TaskName}\"");
    }

    private static void RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(8000);
        }
        catch
        {
            // Autostart is best-effort; the widget still runs this session.
        }
    }
}
