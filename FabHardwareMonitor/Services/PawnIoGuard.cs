using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace FabHardwareMonitor.Services;

public sealed class PawnIoGuard
{
    private const string TempFlagName = "FabHwMon-install-pawnio.flag";
    private const string InstallDirFlagName = "install-pawnio.flag";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";
    private const string DevicePath = @"\\?\GLOBALROOT\Device\PawnIO";
    private static readonly IntPtr InvalidHandle = new(-1);

    public bool IsInstalled()
    {
        try
        {
            if (LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled)
            {
                return true;
            }
        }
        catch
        {
            // Fall back to registry/files when the LHM property is unavailable.
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = hklm.OpenSubKey(UninstallKey);
                if (key is not null)
                {
                    return true;
                }
            }
            catch
            {
                // ignored
            }
        }

        return Directory.Exists(@"C:\Program Files\PawnIO")
               || File.Exists(@"C:\Windows\System32\drivers\PawnIO.sys");
    }

    public bool IsDeviceAccessible() => IsDeviceOpenable();

    public bool NeedsElevationForTemps() =>
        IsInstalled() && !IsDeviceAccessible() && !Elevation.IsAdministrator();

    public bool CanReadCpuTemps() => IsDeviceAccessible() || SensorIpc.HasRecentSample();

    public bool TryEnsureDriverRunning()
    {
        if (!IsInstalled())
        {
            return false;
        }

        if (IsDeviceOpenable())
        {
            return true;
        }

        TryStartService();
        return IsDeviceOpenable();
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
        var ok = IsInstalled() || process.ExitCode is 0 or 3010;
        if (ok)
        {
            TryEnsureDriverRunning();
        }

        return ok;
    }

    private static bool IsDeviceOpenable()
    {
        var handle = CreateFile(
            DevicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (handle == IntPtr.Zero || handle == InvalidHandle)
        {
            return false;
        }

        CloseHandle(handle);
        return true;
    }

    private static void TryStartService()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "start PawnIO",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(4000);
        }
        catch
        {
            // Starting the driver is best-effort; LHM still retries Open().
        }
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

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
