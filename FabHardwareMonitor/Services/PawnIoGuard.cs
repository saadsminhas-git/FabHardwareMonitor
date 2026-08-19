using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace FabHardwareMonitor.Services;

public sealed class PawnIoGuard
{
    private const string TempFlagName = "FabHwMon-install-pawnio.flag";
    private const string InstallDirFlagName = "install-pawnio.flag";

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

    public bool InstallerRequested() => FlagPaths().Any(File.Exists);

    public void ClearInstallerRequest()
    {
        foreach (var path in FlagPaths())
        {
            TryDelete(path);
        }
    }

    public Task<bool> InstallAsync(CancellationToken cancellationToken = default)
        => InstallAsync(silent: false, cancellationToken);

    public async Task<bool> InstallAsync(bool silent, CancellationToken cancellationToken = default)
    {
        var setupPath = BundledSetupPath();
        if (setupPath is null)
        {
            setupPath = Path.Combine(Path.GetTempPath(), "PawnIO_setup.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            await using (var remote = await http.GetStreamAsync(AppConstants.PawnIoSetupUrl, cancellationToken))
            await using (var file = File.Create(setupPath))
            {
                await remote.CopyToAsync(file, cancellationToken);
            }
        }

        var elevated = Elevation.IsAdministrator();
        var start = new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = silent ? "-install -silent" : "",
            UseShellExecute = !elevated,
            Verb = elevated ? "" : "runas",
            CreateNoWindow = silent
        };

        using var process = Process.Start(start);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken);
        return IsInstalled() || process.ExitCode is 0 or 3010;
    }

    private static string? BundledSetupPath()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "PawnIO_setup.exe");
        return File.Exists(bundled) ? bundled : null;
    }

    private static IEnumerable<string> FlagPaths()
    {
        yield return Path.Combine(Path.GetTempPath(), TempFlagName);
        yield return Path.Combine(AppContext.BaseDirectory, InstallDirFlagName);
        var parent = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        if (parent is not null)
        {
            yield return Path.Combine(parent.FullName, InstallDirFlagName);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
