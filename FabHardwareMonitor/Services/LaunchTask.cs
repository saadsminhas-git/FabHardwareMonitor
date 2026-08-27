using System.Diagnostics;
using System.IO;
using FabHardwareMonitor.Interop;

namespace FabHardwareMonitor.Services;

/// <summary>
/// Starts the widget in a --shell process when the launcher is not an
/// Explorer child. Explorer-invoked activations host in-process instead.
/// </summary>
internal static class LaunchTask
{
    public static bool TryStart()
    {
        TryKillOrphanHosts();
        ShortcutFix.Rewrite();
        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }

        var parent = Native.ParentProcessName();
        LaunchLog.Write(
            $"exe={exe} parent={parent} shortcut={Native.LaunchedFromShortcut()} " +
            $"flags=0x{Native.GetStartupFlags():X} title={Native.GetStartupTitle()}");

        if (NamedIpcMutex.Exists(AppConstants.MutexName))
        {
            LaunchLog.Write("ui already running");
            Program.SignalRunningInstance();
            return true;
        }

        return TryStartFromScheduler() || Native.StartProcessFromExplorer(exe, AppConstants.ShellArgument);
    }

    /// <summary>
    /// Task Scheduler CreateProcess is independent of Start Search. Do not
    /// CreateProcess from the Search-activated process — that identity sticks.
    /// </summary>
    public static bool TryStartFromScheduler()
    {
        ShortcutFix.Rewrite();
        var registered = EnsureRegistered();
        var ran = registered && ScheduledTasks.TryRun(AppConstants.LaunchTaskName);
        LaunchLog.Write($"scheduler register={registered} run={ran}");
        return ran;
    }

    /// <summary>
    /// The scheduled task process is a child of svchost. Spawn the widget as
    /// an Explorer child so attach matches a folder double-click.
    /// </summary>
    public static void TryReparentUnderExplorer()
    {
        var parent = Native.ParentProcessName();
        if (Native.IsExplorerParent(parent))
        {
            LaunchLog.Write($"shell already explorer parent={parent}");
            return;
        }

        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }

        if (Native.StartProcessFromExplorer(exe, AppConstants.ShellArgument))
        {
            LaunchLog.Write($"shell reparent explorer from {parent}");
            Environment.Exit(0);
        }

        LaunchLog.Write($"shell reparent failed parent={parent}");
    }

    public static bool EnsureRegistered()
    {
        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }

        return ScheduledTasks.TryRegister(new ScheduledTasks.Spec
        {
            Name = AppConstants.LaunchTaskName,
            Exe = exe,
            Arguments = AppConstants.ShellArgument,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            Description = $"{AppConstants.ProductName} Start Menu launch.",
            RunLevel = ScheduledTasks.RunLevelLimited,
            MultipleInstances = ScheduledTasks.InstancesIgnoreNew
        }, "LaunchTask");
    }

    public static void Unregister() => ScheduledTasks.Delete(AppConstants.LaunchTaskName, "LaunchTask");

    private static void TryKillOrphanHosts()
    {
        foreach (var name in new[] { "FvWidgetHost", "FabHardwareMonitor.Host" })
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best-effort cleanup of the 1.1.17/18 host processes.
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
