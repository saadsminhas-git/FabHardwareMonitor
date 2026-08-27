using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using FabHardwareMonitor.Services;

namespace FabHardwareMonitor;

public static class Elevation
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Starts this exe unelevated via the shell, even when the current process is elevated.
    /// Process.Start would inherit the high integrity level and Explorer would not host the widget.
    /// </summary>
    public static bool StartUnelevated(string arguments = "")
    {
        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            return false;
        }

        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null)
            {
                return false;
            }

            dynamic shell = Activator.CreateInstance(type)
                            ?? throw new InvalidOperationException("Shell.Application is unavailable.");
            var directory = Path.GetDirectoryName(exe) ?? "";
            shell.ShellExecute(exe, arguments, directory, "open", 1);
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Unelevate", ex);
            return false;
        }
    }

    public static bool StartSensorsHelper()
    {
        if (NamedIpcMutex.Exists(AppConstants.SensorsMutexName))
        {
            return true;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = AppConstants.SensorsArgument,
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
