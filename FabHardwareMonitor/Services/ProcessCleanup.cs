using System.Diagnostics;
using System.Management;
using FabHardwareMonitor.Interop;

namespace FabHardwareMonitor.Services;

/// <summary>
/// Start search can keep the non-shell launcher alive next to the --shell UI
/// process. Explorer will not paint the taskbar widget while both exist.
/// </summary>
internal static class ProcessCleanup
{
    public static void TerminateDetachers()
    {
        Terminate(detachersOnly: true, reason: "shell-start");
    }

    public static void TerminateDetachersAfterUi()
    {
        Terminate(detachersOnly: true, reason: "after-ui");
    }

    public static void ScheduleFollowUp()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(750);
            Terminate(detachersOnly: true, reason: "follow-up-1");
            await Task.Delay(2000);
            Terminate(detachersOnly: true, reason: "follow-up-2");
        });
    }

    private static void Terminate(bool detachersOnly, string reason)
    {
        var self = Environment.ProcessId;
        var killed = 0;
        try
        {
            foreach (var process in Process.GetProcessesByName("FabHardwareMonitor"))
            {
                if (process.Id == self)
                {
                    continue;
                }

                var command = TryReadCommandLine(process.Id);
                if (detachersOnly && ShouldKeepSibling(command))
                {
                    continue;
                }

                LaunchLog.Write(
                    $"{reason} kill pid={process.Id} ws={process.WorkingSet64 / 1024}K " +
                    $"cmd={command ?? "(null)"}");
                process.Kill(entireProcessTree: true);
                killed++;
            }

            LaunchLog.Write($"{reason} killed={killed}");
        }
        catch (Exception ex)
        {
            CrashLog.Write("ProcessCleanup", ex);
        }
    }

    private static bool ShouldKeepSibling(string? command)
    {
        if (!string.IsNullOrWhiteSpace(command))
        {
            return command.Contains(AppConstants.SensorsArgument, StringComparison.OrdinalIgnoreCase)
                   || command.Contains(AppConstants.ShellArgument, StringComparison.OrdinalIgnoreCase);
        }

        return NamedIpcMutex.Exists(AppConstants.MutexName)
               || NamedIpcMutex.Exists(AppConstants.SensorsMutexName);
    }

    private static string? TryReadCommandLine(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString();
            }
        }
        catch
        {
            // Fall through to Native fallback.
        }

        return Native.TryReadProcessCommandLine(pid);
    }
}
