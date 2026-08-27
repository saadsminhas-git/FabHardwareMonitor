using System.IO;

namespace FabHardwareMonitor.Services;

/// <summary>
/// Starts the elevated temperature helper without a UAC prompt on later launches.
/// Registered once while setup is still elevated.
/// </summary>
internal static class SensorTask
{
    public static void Register()
    {
        var exe = ShortcutFix.InstalledExe();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe) || !Elevation.IsAdministrator())
        {
            return;
        }

        ScheduledTasks.TryRegister(new ScheduledTasks.Spec
        {
            Name = AppConstants.SensorsTaskName,
            Exe = exe,
            Arguments = AppConstants.SensorsArgument,
            Description = $"{AppConstants.ProductName} temperature helper.",
            RunLevel = ScheduledTasks.RunLevelHighest,
            MultipleInstances = ScheduledTasks.InstancesIgnoreNew,
            LogonDelay = TimeSpan.FromSeconds(10)
        }, "SensorTask");
    }

    public static void Unregister() => ScheduledTasks.Delete(AppConstants.SensorsTaskName, "SensorTask");

    public static bool TryStart()
    {
        if (SensorIpc.HelperIsRunning())
        {
            return true;
        }

        if (!ScheduledTasks.TryRun(AppConstants.SensorsTaskName))
        {
            return false;
        }

        return ScheduledTasks.WaitUntil(SensorIpc.HelperIsRunning, TimeSpan.FromSeconds(3));
    }
}
