using FabHardwareMonitor.Models;

namespace FabHardwareMonitor.Services;

public sealed class HardwarePipeline : IDisposable
{
    private readonly NetworkSampler _network = new();
    private readonly SystemSampler _system = new();
    private readonly HardwareSampler _hardware = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private AppSettings _settings;

    public event Action<HardwareSnapshot>? Updated;

    public HardwarePipeline(AppSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<NamedOption> ListNics() => _network.ListAdapters();

    public IReadOnlyList<NamedOption> ListGpus()
    {
        try
        {
            return _hardware.ListGpus();
        }
        catch
        {
            return [new NamedOption { Id = "", Name = "Auto" }];
        }
    }

    public IReadOnlyList<NamedOption> ListCpuTempSensors()
    {
        try
        {
            return _hardware.ListCpuTempSensors();
        }
        catch
        {
            return [new NamedOption { Id = "", Name = "Auto" }];
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Restart();
    }

    public void Start()
    {
        Restart();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignored
        }

        _system.Dispose();
        _hardware.Dispose();
    }

    private void Restart()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => LoopAsync(token), token);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            _hardware.EnsureOpen();
        }
        catch
        {
            // Temps stay unavailable; CPU/RAM/net still sample.
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ClampRefreshInterval()));
        while (!token.IsCancellationRequested)
        {
            try
            {
                var (up, down, nicName, nicId) = _network.Sample(_settings.NicId);
                var (cpu, mem) = _system.Sample();
                HardwareSampler.GpuSample temps = default;
                try
                {
                    temps = _hardware.Sample(_settings.GpuId, _settings.CpuTempSensor);
                }
                catch
                {
                    temps = default;
                }

                Updated?.Invoke(new HardwareSnapshot
                {
                    UploadBytesPerSec = up,
                    DownloadBytesPerSec = down,
                    CpuUsage = cpu,
                    MemoryUsage = mem,
                    GpuUsage = temps.GpuLoad,
                    VramUsage = temps.Vram,
                    CpuTemp = temps.CpuTemp,
                    GpuTemp = temps.GpuTemp,
                    CpuName = temps.CpuName,
                    GpuName = temps.GpuName,
                    CpuTempSensorName = temps.CpuTempSensorName,
                    NicName = nicName,
                    NicId = nicId,
                    GpuId = temps.GpuId
                });
            }
            catch
            {
                // Keep the loop alive through transient counter/driver errors.
            }

            try
            {
                await timer.WaitForNextTickAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
