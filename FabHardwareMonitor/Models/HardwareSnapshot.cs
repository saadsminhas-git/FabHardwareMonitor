namespace FabHardwareMonitor.Models;

public sealed class HardwareSnapshot
{
    public double UploadBytesPerSec { get; init; }
    public double DownloadBytesPerSec { get; init; }
    public double? CpuUsage { get; init; }
    public double? MemoryUsage { get; init; }
    public double? GpuUsage { get; init; }
    public double? VramUsage { get; init; }
    public double? CpuTemp { get; init; }
    public double? GpuTemp { get; init; }
    public string? CpuName { get; init; }
    public string? GpuName { get; init; }
    public string? CpuTempSensorName { get; init; }
    public string? NicName { get; init; }
    public string? NicId { get; init; }
    public string? GpuId { get; init; }
}

public sealed class NamedOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    public override string ToString() => Name;
}
