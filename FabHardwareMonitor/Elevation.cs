using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace FabHardwareMonitor;

public static class Elevation
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Velopack cannot start an exe marked requireAdministrator. Restart elevated after install instead.
    /// Returns true when a new elevated process was started and this process should exit.
    /// </summary>
    public static bool RelaunchElevated()
    {
        if (IsAdministrator())
        {
            return false;
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
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Win32Exception)
        {
            // UAC cancelled — keep running so net/CPU%/RAM still work; temps stay --.
            return false;
        }
    }
}
