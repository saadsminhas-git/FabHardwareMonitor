using System.Diagnostics;
using FabHardwareMonitor.Interop;

namespace FabHardwareMonitor.Services;

internal static class ScheduledTasks
{
    internal const int ActionExec = 0;
    internal const int CreateOrUpdate = 6;
    internal const int LogonInteractiveToken = 3;
    internal const int TriggerLogon = 9;
    internal const int TriggerTime = 1;
    internal const int RunLevelLimited = 0;
    internal const int RunLevelHighest = 1;
    internal const int InstancesIgnoreNew = 2;
    internal const int InstancesParallel = 0;

    private const int FileNotFound = unchecked((int)0x80070002);
    private const int TaskNotFound = unchecked((int)0x8004130C);

    internal sealed class Spec
    {
        public required string Name { get; init; }
        public required string Exe { get; init; }
        public required string Arguments { get; init; }
        public required string Description { get; init; }
        public required int RunLevel { get; init; }
        public required int MultipleInstances { get; init; }
        public TimeSpan? LogonDelay { get; init; }
        public string? WorkingDirectory { get; init; }
    }

    public static bool TryRegister(Spec spec, string logSource)
    {
        try
        {
            if (!TryConnect(out var folder, out var service))
            {
                return false;
            }

            dynamic definition = service.NewTask(0);
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";

            definition.RegistrationInfo.Description = spec.Description;
            definition.Settings.Enabled = true;
            definition.Settings.AllowDemandStart = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.MultipleInstances = spec.MultipleInstances;
            definition.Settings.IdleSettings.StopOnIdleEnd = false;
            definition.Principal.Id = "Author";
            definition.Principal.UserId = user;
            definition.Principal.LogonType = LogonInteractiveToken;
            definition.Principal.RunLevel = spec.RunLevel;

            if (spec.LogonDelay is { } delay)
            {
                dynamic trigger = definition.Triggers.Create(TriggerLogon);
                trigger.UserId = user;
                trigger.Enabled = true;
                trigger.Delay = $"PT{(int)Math.Max(1, delay.TotalSeconds)}S";
            }
            else
            {
                // Demand-start tasks with no trigger can return SCHED_E_TASK_NOT_READY.
                dynamic trigger = definition.Triggers.Create(TriggerTime);
                trigger.Enabled = false;
                trigger.StartBoundary = "2099-01-01T00:00:00";
            }

            dynamic action = definition.Actions.Create(ActionExec);
            action.Path = spec.Exe;
            action.Arguments = spec.Arguments;
            if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
            {
                action.WorkingDirectory = spec.WorkingDirectory;
            }

            folder.RegisterTaskDefinition(
                spec.Name,
                definition,
                CreateOrUpdate,
                Type.Missing,
                Type.Missing,
                LogonInteractiveToken,
                Type.Missing);
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write(logSource, ex);
            return false;
        }
    }

    public static void Delete(string name, string logSource)
    {
        try
        {
            if (TryConnect(out var folder, out _))
            {
                folder.DeleteTask(name, 0);
                return;
            }
        }
        catch (Exception ex)
        {
            if (ex.HResult is not FileNotFound and not TaskNotFound)
            {
                CrashLog.Write(logSource, ex);
            }
        }

        RunSchtasks($"/Delete /F /TN \"{name}\"");
    }

    public static bool TryRun(string name)
    {
        var schtasks = System.IO.Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        return Native.StartProcessBreakaway(schtasks, $"/Run /TN \"{name}\"");
    }

    public static bool WaitUntil(Func<bool> ready, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (ready())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return ready();
    }

    private static bool TryConnect(out dynamic folder, out dynamic service)
    {
        folder = null!;
        service = null!;
        var type = Type.GetTypeFromProgID("Schedule.Service");
        if (type is null)
        {
            return false;
        }

        service = Activator.CreateInstance(type)
                  ?? throw new InvalidOperationException("Task Scheduler is unavailable.");
        service.Connect();
        folder = service.GetFolder("\\");
        return true;
    }

    private static Process? StartSchtasks(string arguments) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false
        });

    private static void RunSchtasks(string arguments)
    {
        try
        {
            using var process = StartSchtasks(arguments);
            process?.WaitForExit(8000);
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }
}
