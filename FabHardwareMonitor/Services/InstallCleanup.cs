using System.IO;

namespace FabHardwareMonitor.Services;

/// <summary>
/// Removes per-user leftovers that Velopack/MSI do not delete: roaming
/// settings, crash logs, the logon task, and temp PawnIO helper files.
/// Does not delete the install directory (the installer removes that) and
/// does not uninstall the separate PawnIO driver.
/// </summary>
internal static class InstallCleanup
{
    private static readonly string[] SkipProfileNames =
    [
        "Public",
        "Default",
        "Default User",
        "All Users",
        "AllUsers"
    ];

    public static void Run()
    {
        new AutostartService().Unregister();
        TryDeleteTaskFile();

        foreach (var dir in RoamingAppDataFolders())
        {
            TryDeleteDirectory(dir);
        }

        TryDeleteTempJunk(Path.GetTempPath());
        TryDeleteTempJunk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
    }

    private static IEnumerable<string> RoamingAppDataFolders()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.SettingsFolderName);
        if (seen.Add(current))
        {
            yield return current;
        }

        string? usersRoot = null;
        try
        {
            usersRoot = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))?.FullName;
        }
        catch
        {
            // ignored
        }

        if (string.IsNullOrWhiteSpace(usersRoot) || !Directory.Exists(usersRoot))
        {
            yield break;
        }

        IEnumerable<string> profiles = [];
        try
        {
            profiles = Directory.GetDirectories(usersRoot);
        }
        catch
        {
            yield break;
        }

        foreach (var profile in profiles)
        {
            var name = Path.GetFileName(profile);
            if (SkipProfileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var roaming = Path.Combine(profile, "AppData", "Roaming", AppConstants.SettingsFolderName);
            if (seen.Add(roaming))
            {
                yield return roaming;
            }
        }
    }

    private static void TryDeleteTaskFile()
    {
        TryDeleteFile(Path.Combine(Environment.SystemDirectory, "Tasks", AppConstants.TaskName));
    }

    private static void TryDeleteTempJunk(string tempDir)
    {
        if (string.IsNullOrWhiteSpace(tempDir))
        {
            return;
        }

        TryDeleteFile(Path.Combine(tempDir, "FabHwMon-install-pawnio.flag"));
        TryDeleteFile(Path.Combine(tempDir, "FabHwMon-PawnIO.log"));
        TryDeleteFile(Path.Combine(tempDir, "PawnIO_setup.exe"));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Uninstall must still finish.
        }
    }

    private static void TryDeleteFile(string path)
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
