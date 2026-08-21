using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FabHardwareMonitor.Services;

public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskRunLevelLimited = 0;
    private const int TaskInstancesIgnoreNew = 2;

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
        var exe = Environment.ProcessPath;
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
        TryDeleteTask();
        ClearRunKey();
    }

    private static bool TryRegisterTask(string exe)
    {
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type is null)
            {
                return false;
            }

            dynamic service = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Task Scheduler is unavailable.");
            service.Connect();
            dynamic folder = service.GetFolder("\\");
            dynamic definition = service.NewTask(0);
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";

            definition.RegistrationInfo.Description = $"{AppConstants.ProductName} starts at sign-in.";
            definition.Settings.Enabled = true;
            definition.Settings.AllowDemandStart = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.MultipleInstances = TaskInstancesIgnoreNew;
            definition.Settings.IdleSettings.StopOnIdleEnd = false;
            definition.Principal.Id = "Author";
            definition.Principal.UserId = user;
            definition.Principal.LogonType = TaskLogonInteractiveToken;
            definition.Principal.RunLevel = TaskRunLevelLimited;

            dynamic trigger = definition.Triggers.Create(TaskTriggerLogon);
            trigger.UserId = user;
            trigger.Enabled = true;
            trigger.Delay = "PT15S";

            dynamic action = definition.Actions.Create(TaskActionExec);
            action.Path = exe;

            folder.RegisterTaskDefinition(
                AppConstants.TaskName,
                definition,
                TaskCreateOrUpdate,
                Type.Missing,
                Type.Missing,
                TaskLogonInteractiveToken,
                Type.Missing);
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Autostart", ex);
            return false;
        }
    }

    private static void TryDeleteTask()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type is not null)
            {
                dynamic service = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("Task Scheduler is unavailable.");
                service.Connect();
                dynamic folder = service.GetFolder("\\");
                folder.DeleteTask(AppConstants.TaskName, 0);
                return;
            }
        }
        catch (Exception ex)
        {
            var missing = ex.HResult == unchecked((int)0x80070002)
                          || ex.HResult == unchecked((int)0x8004130C);
            if (!missing)
            {
                CrashLog.Write("Autostart", ex);
            }
        }

        RunSchtasks($"/Delete /F /TN \"{AppConstants.TaskName}\"");
    }

    private static void SetRunKey(string exe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(AppConstants.TaskName, $"\"{exe}\"", RegistryValueKind.String);
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

    private static void RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(8000);
        }
        catch
        {
            // Autostart cleanup is best-effort.
        }
    }
}
