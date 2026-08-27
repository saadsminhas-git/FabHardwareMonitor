using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FabHardwareMonitor.Services;

/// <summary>
/// Keep a single Start Menu shortcut to the real exe (no Velopack AUMID).
/// Remove stub shortcuts that otherwise appear as a second app in Start.
/// </summary>
internal static class ShortcutFix
{
    public static string? InstalledExe()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return path;
        }

        if (string.Equals(Path.GetFileName(dir), "current", StringComparison.OrdinalIgnoreCase))
        {
            var main = Path.Combine(dir, "FabHardwareMonitor.exe");
            return File.Exists(main) ? main : path;
        }

        var nested = Path.Combine(dir, "current", "FabHardwareMonitor.exe");
        return File.Exists(nested) ? nested : path;
    }

    public static void Rewrite()
    {
        var exe = InstalledExe();
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }

        var name = AppConstants.ProductName + ".lnk";
        TryWrite(Path.Combine(UserPrograms(), name), exe);
        TryDeleteStub(Path.Combine(CommonPrograms(), name));
        TryDeleteStub(Path.Combine(CommonDesktop(), name));
        TryDeleteStub(Path.Combine(UserDesktop(), name));
        TryDelete(Path.Combine(UserPrograms(), "FV Widget Host.lnk"));
        TryDelete(Path.Combine(UserPrograms(), "FvWidgetHost.lnk"));
        TryDeleteAutoNamedShortcuts(exe);
        TryClearVelopackAppId();
        TryRemoveBrokenUninstallEntries();
        LaunchLog.Write($"shortcut exists={File.Exists(Path.Combine(UserPrograms(), name))} exe={exe}");
    }

    public static void RemoveOurs()
    {
        TryDelete(Path.Combine(UserPrograms(), AppConstants.ProductName + ".lnk"));
        TryDelete(Path.Combine(UserDesktop(), AppConstants.ProductName + ".lnk"));
    }

    private static string UserPrograms() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    private static string UserDesktop() =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private static string CommonPrograms() =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

    private static string CommonDesktop() =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    private static void TryWrite(string linkPath, string exe)
    {
        try
        {
            var directory = Path.GetDirectoryName(linkPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            TryDelete(linkPath);
            var shortcut = CreateShortcut(linkPath);
            if (shortcut is null)
            {
                return;
            }

            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
            shortcut.Arguments = "";
            shortcut.Description = AppConstants.ProductName;
            shortcut.IconLocation = exe + ",0";
            shortcut.Save();
            NotifyShortcutChanged(linkPath);
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShortcutFix", ex);
        }
    }

    private static void TryDeleteStub(string linkPath)
    {
        try
        {
            if (!File.Exists(linkPath))
            {
                return;
            }

            var shortcut = CreateShortcut(linkPath);
            if (shortcut is null)
            {
                TryDelete(linkPath);
                return;
            }

            var target = (string)shortcut.TargetPath;
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            var file = Path.GetFileName(target);
            if (string.Equals(file, AppConstants.HostExeFileName, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(linkPath);
                return;
            }

            var inCurrent = string.Equals(Path.GetFileName(Path.GetDirectoryName(target)), "current", StringComparison.OrdinalIgnoreCase);
            var isStub = string.Equals(file, AppConstants.ProductName + ".exe", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(file, "Update.exe", StringComparison.OrdinalIgnoreCase);
            if (isStub && !inCurrent)
            {
                TryDelete(linkPath);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShortcutFix", ex);
        }
    }

    private static dynamic? CreateShortcut(string linkPath)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null)
        {
            return null;
        }

        dynamic shell = Activator.CreateInstance(type)
                        ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        return shell.CreateShortcut(linkPath);
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
        catch (Exception ex)
        {
            CrashLog.Write("ShortcutFix", ex);
        }
    }

    private static void TryClearVelopackAppId()
    {
        foreach (var id in new[]
                 {
                     @"Software\Classes\AppUserModelId\velopack.FabHardwareMonitor",
                     @"Software\Classes\AppUserModelId\FabricVisuals.FabHardwareMonitor.Relaunch",
                     $@"Software\Classes\AppUserModelId\{AppConstants.AppUserModelId}"
                 })
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(id, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                CrashLog.Write("ShortcutFix", ex);
            }
        }
    }

    public static void RemoveBrokenUninstallEntries() => TryRemoveBrokenUninstallEntries();

    private static void TryRemoveBrokenUninstallEntries()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FabHardwareMonitor");
        foreach (var path in new[]
                 {
                     @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                     @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                 })
        {
            try
            {
                using var uninstall = Registry.CurrentUser.OpenSubKey(path, writable: true);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var name in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = uninstall.OpenSubKey(name);
                        if (sub is null)
                        {
                            continue;
                        }

                        var location = sub.GetValue("InstallLocation") as string ?? "";
                        var command = sub.GetValue("UninstallString") as string ?? "";
                        var display = sub.GetValue("DisplayName") as string ?? "";
                        var orphan = ContainsIgnoreCase(location, local) || ContainsIgnoreCase(command, local);
                        var missingUpdate = ContainsIgnoreCase(command, "Update.exe")
                                            && ContainsIgnoreCase(display, AppConstants.ProductName)
                                            && !File.Exists(FirstQuotedPath(command));
                        if (!orphan && !missingUpdate)
                        {
                            continue;
                        }

                        uninstall.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
                        LaunchLog.Write($"removed uninstall entry={name}");
                    }
                    catch (Exception ex)
                    {
                        CrashLog.Write("ShortcutFix", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write("ShortcutFix", ex);
            }
        }
    }

    private static bool ContainsIgnoreCase(string value, string part) =>
        !string.IsNullOrWhiteSpace(part) && value.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static string FirstQuotedPath(string command)
    {
        var start = command.IndexOf('"');
        var end = command.IndexOf('"', start + 1);
        if (start >= 0 && end > start)
        {
            return command.Substring(start + 1, end - start - 1);
        }

        var token = command.Trim().Split(' ')[0];
        return token.Trim('"');
    }

    private static void TryDeleteAutoNamedShortcuts(string exe)
    {
        try
        {
            var programs = UserPrograms();
            if (!Directory.Exists(programs))
            {
                return;
            }

            foreach (var linkPath in Directory.EnumerateFiles(programs, "*.lnk"))
            {
                var file = Path.GetFileName(linkPath);
                if (!file.Contains("Shortcut", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var shortcut = CreateShortcut(linkPath);
                if (shortcut is null)
                {
                    continue;
                }

                var target = (string)shortcut.TargetPath;
                if (string.Equals(target, exe, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(linkPath);
                }
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShortcutFix", ex);
        }
    }

    private const uint ShcneUpdateitem = 0x00002000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string? item1, string? item2);

    private static void NotifyShortcutChanged(string path) =>
        SHChangeNotify(ShcneUpdateitem, ShcnfPathW | ShcnfFlush, path, null);
}
