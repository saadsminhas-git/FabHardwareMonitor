using FabHardwareMonitor.Models;
using LibreHardwareMonitor.Hardware;

namespace FabHardwareMonitor.Services;

public sealed class HardwareSampler : IDisposable
{
    private static readonly string[] CpuTempPriority =
    [
        "CPU Package",
        "CPU Core Average",
        "Core Average",
        "CPU Core Max",
        "Core Max",
        "Core (Tctl/Tdie)",
        "CPU CCD Average",
        "CCD Average"
    ];

    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = false,
        IsControllerEnabled = false,
        IsNetworkEnabled = false,
        IsStorageEnabled = false,
        IsPsuEnabled = false
    };

    private bool _opened;

    public void EnsureOpen()
    {
        if (_opened)
        {
            return;
        }

        _computer.Open();
        _opened = true;
    }

    public IReadOnlyList<NamedOption> ListGpus()
    {
        EnsureOpen();
        UpdateAll();
        var list = new List<NamedOption> { new() { Id = "", Name = "Auto" } };
        foreach (var gpu in Gpus())
        {
            list.Add(new NamedOption { Id = gpu.Identifier.ToString(), Name = gpu.Name });
        }

        return list;
    }

    public IReadOnlyList<NamedOption> ListCpuTempSensors()
    {
        EnsureOpen();
        UpdateAll();
        var list = new List<NamedOption> { new() { Id = "", Name = "Auto" } };
        foreach (var sensor in CpuTempSensors())
        {
            list.Add(new NamedOption
            {
                Id = sensor.Name,
                Name = sensor.Name
            });
        }

        return list;
    }

    public GpuSample Sample(string? gpuId, string? cpuTempSensor)
    {
        EnsureOpen();
        UpdateAll();

        var cpu = Cpus().FirstOrDefault();
        var gpu = SelectGpu(gpuId);
        var cpuTemp = ResolveCpuTemp(cpuTempSensor);

        return new GpuSample(
            CpuName: cpu?.Name,
            GpuName: gpu?.Name,
            GpuId: gpu?.Identifier.ToString(),
            CpuTemp: cpuTemp.Value,
            CpuTempSensorName: cpuTemp.Name,
            GpuTemp: FirstValue(gpu, SensorType.Temperature, "GPU Core", "GPU") ?? FirstValue(gpu, SensorType.Temperature),
            GpuLoad: FirstValue(gpu, SensorType.Load, "GPU Core", "D3D 3D", "GPU") ?? FirstValue(gpu, SensorType.Load),
            Vram: VramPercent(gpu));
    }

    public void Dispose()
    {
        if (_opened)
        {
            _computer.Close();
        }
    }

    private void UpdateAll()
    {
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
            }
        }
    }

    private IEnumerable<IHardware> Cpus() =>
        _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu);

    private IEnumerable<IHardware> Gpus() =>
        _computer.Hardware.Where(h =>
            h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel);

    private IHardware? SelectGpu(string? gpuId)
    {
        var gpus = Gpus().ToList();
        if (gpus.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(gpuId))
        {
            var match = gpus.FirstOrDefault(g => g.Identifier.ToString() == gpuId);
            if (match is not null)
            {
                return match;
            }
        }

        IHardware? best = null;
        double bestLoad = -1;
        foreach (var gpu in gpus)
        {
            var load = FirstValue(gpu, SensorType.Load, "GPU Core", "D3D 3D", "GPU") ?? 0;
            if (load >= bestLoad)
            {
                bestLoad = load;
                best = gpu;
            }
        }

        return best ?? gpus[0];
    }

    private (double? Value, string? Name) ResolveCpuTemp(string? overrideName)
    {
        var sensors = CpuTempSensors().ToList();
        if (sensors.Count == 0)
        {
            return (null, null);
        }

        if (!string.IsNullOrWhiteSpace(overrideName))
        {
            var named = sensors.FirstOrDefault(s => s.Name.Equals(overrideName, StringComparison.OrdinalIgnoreCase));
            if (named?.Value is not null)
            {
                return (named.Value, named.Name);
            }
        }

        foreach (var name in CpuTempPriority)
        {
            var match = sensors.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match?.Value is not null)
            {
                return (match.Value, match.Name);
            }
        }

        var ccd = sensors.FirstOrDefault(s =>
            s.Name.Contains("CCD", StringComparison.OrdinalIgnoreCase) && s.Value is not null);
        if (ccd is not null)
        {
            return (ccd.Value, ccd.Name);
        }

        var any = sensors.FirstOrDefault(s => s.Value is not null);
        return (any?.Value, any?.Name);
    }

    private IEnumerable<ISensor> CpuTempSensors()
    {
        foreach (var cpu in Cpus())
        {
            foreach (var sensor in AllSensors(cpu))
            {
                if (sensor.SensorType != SensorType.Temperature)
                {
                    continue;
                }

                if (sensor.Name.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return sensor;
            }
        }
    }

    private static double? VramPercent(IHardware? gpu)
    {
        if (gpu is null)
        {
            return null;
        }

        var used = FirstValue(gpu, SensorType.SmallData, "GPU Memory Used", "Memory Used")
                   ?? FirstValue(gpu, SensorType.Data, "GPU Memory Used", "Memory Used");
        var total = FirstValue(gpu, SensorType.SmallData, "GPU Memory Total", "Memory Total")
                    ?? FirstValue(gpu, SensorType.Data, "GPU Memory Total", "Memory Total");
        if (used is not null && total is > 0)
        {
            return Math.Clamp(used.Value * 100d / total.Value, 0, 100);
        }

        return FirstValue(gpu, SensorType.Load, "GPU Memory", "Memory");
    }

    private static double? FirstValue(IHardware? hardware, SensorType type, params string[] names)
    {
        if (hardware is null)
        {
            return null;
        }

        var sensors = AllSensors(hardware).Where(s => s.SensorType == type && s.Value is not null).ToList();
        foreach (var name in names)
        {
            var match = sensors.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                                                    || s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match?.Value is not null)
            {
                return match.Value;
            }
        }

        return names.Length == 0 ? sensors.FirstOrDefault()?.Value : null;
    }

    private static IEnumerable<ISensor> AllSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var sub in hardware.SubHardware)
        {
            foreach (var sensor in sub.Sensors)
            {
                yield return sensor;
            }
        }
    }

    public readonly record struct GpuSample(
        string? CpuName,
        string? GpuName,
        string? GpuId,
        double? CpuTemp,
        string? CpuTempSensorName,
        double? GpuTemp,
        double? GpuLoad,
        double? Vram);
}
