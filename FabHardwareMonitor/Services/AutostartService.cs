using System.IO;
using Microsoft.Win32;

namespace FabHardwareMonitor.Services;

public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

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
        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            return;
        }

        if (TryRegisterTask(exe))
        {
            ClearRunKey();
            return;
        }

        // Task Scheduler can be blocked; HKCU Run still starts at sign-in.
        SetRunKey(exe);
    }

    public void Unregister()
    {
        ScheduledTasks.Delete(AppConstants.TaskName, "Autostart");
        ClearRunKey();
    }

    private static bool TryRegisterTask(string exe) =>
        ScheduledTasks.TryRegister(new ScheduledTasks.Spec
        {
            Name = AppConstants.TaskName,
            Exe = exe,
            Arguments = AppConstants.ShellArgument,
            Description = $"{AppConstants.ProductName} starts at sign-in.",
            RunLevel = ScheduledTasks.RunLevelLimited,
            MultipleInstances = ScheduledTasks.InstancesIgnoreNew,
            LogonDelay = TimeSpan.FromSeconds(15)
        }, "Autostart");

    private static void SetRunKey(string exe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(AppConstants.TaskName, $"\"{exe}\" {AppConstants.ShellArgument}", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Autostart", ex);
        }
    }

    private static void ClearRunKey()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppConstants.TaskName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Autostart", ex);
        }
    }
}
