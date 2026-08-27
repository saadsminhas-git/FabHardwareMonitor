namespace FabHardwareMonitor.Services;

internal static class SensorHost
{
    public static void Run(Action? ready = null)
    {
        using var mutex = NamedIpcMutex.Create(AppConstants.SensorsMutexName, true, out var created);
        if (!created)
        {
            return;
        }

        ready?.Invoke();
        ScheduledTasks.WaitUntil(
            () => NamedIpcMutex.Exists(AppConstants.MutexName),
            TimeSpan.FromSeconds(8));
        SensorIpc.EnsureOpen();

        using var sampler = new HardwareSampler();
        var pawnIo = new PawnIoGuard();
        try
        {
            pawnIo.TryEnsureDriverRunning();
            sampler.EnsureOpen();
        }
        catch (Exception ex)
        {
            CrashLog.Write("SensorHostOpen", ex);
        }

        var misses = 0;
        while (misses < 20)
        {
            try
            {
                SensorIpc.Write(sampler.Sample(null, null));
            }
            catch (Exception ex)
            {
                CrashLog.Write("SensorHostSample", ex);
            }

            Thread.Sleep(1000);
            if (NamedIpcMutex.Exists(AppConstants.MutexName))
            {
                misses = 0;
            }
            else
            {
                misses++;
            }
        }
    }

    public static void EnsureHelper()
    {
        if (Elevation.IsAdministrator() || SensorIpc.HelperIsRunning())
        {
            return;
        }

        if (!new PawnIoGuard().NeedsElevationForTemps())
        {
            return;
        }

        if (SensorTask.TryStart())
        {
            return;
        }

        Elevation.StartSensorsHelper();
    }
}
