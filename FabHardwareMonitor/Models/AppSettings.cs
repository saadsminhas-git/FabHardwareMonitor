namespace FabHardwareMonitor.Models;

public sealed class AppSettings
{
    public int RefreshIntervalMs { get; set; } = 1000;
    public string? NicId { get; set; }
    public string? GpuId { get; set; }
    public string? CpuTempSensor { get; set; }
    public string TextColor { get; set; } = "#FFFFFFFF";
    public bool ShowVram { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public bool PawnIoSkipped { get; set; }

    public int ClampRefreshInterval()
    {
        if (RefreshIntervalMs < 500)
        {
            return 500;
        }

        if (RefreshIntervalMs > 5000)
        {
            return 5000;
        }

        return RefreshIntervalMs;
    }
}
