using Microsoft.Win32;
using System.IO;
using System.Net.Http;

namespace FabHardwareMonitor.Services;

public sealed class PawnIoGuard
{
    public bool IsInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            if (key is not null)
            {
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return Directory.Exists(@"C:\Program Files\PawnIO")
               || File.Exists(@"C:\Windows\System32\drivers\PawnIO.sys");
    }

    public async Task<bool> InstallAsync(CancellationToken cancellationToken = default)
    {
        var setupPath = Path.Combine(Path.GetTempPath(), "PawnIO_setup.exe");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        await using (var remote = await http.GetStreamAsync(AppConstants.PawnIoSetupUrl, cancellationToken))
        await using (var file = File.Create(setupPath))
        {
            await remote.CopyToAsync(file, cancellationToken);
        }

        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = setupPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        using var process = System.Diagnostics.Process.Start(start);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken);
        return IsInstalled();
    }
}
