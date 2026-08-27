using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FabHardwareMonitor.Interop;
using Microsoft.Win32.SafeHandles;

namespace FabHardwareMonitor.Services;

internal static class SensorIpc
{
    private const int MapSize = 8192;
    private static readonly object Gate = new();
    private static SafeMemoryMappedFileHandle? _handle;
    private static IntPtr _view;

    public static void EnsureOpen()
    {
        lock (Gate)
        {
            if (_view != IntPtr.Zero)
            {
                return;
            }

            _handle = Native.CreateMediumIntegrityMap(AppConstants.SensorMapName, MapSize);
            if (_handle.IsInvalid)
            {
                return;
            }

            _view = Native.MapView(_handle, MapSize);
        }
    }

    public static void Write(HardwareSampler.GpuSample sample)
    {
        EnsureOpen();
        var payload = new Payload
        {
            UtcTicks = DateTime.UtcNow.Ticks,
            CpuName = sample.CpuName,
            GpuName = sample.GpuName,
            GpuId = sample.GpuId,
            CpuLoad = sample.CpuLoad,
            CpuTemp = sample.CpuTemp,
            CpuTempSensorName = sample.CpuTempSensorName,
            GpuTemp = sample.GpuTemp,
            GpuLoad = sample.GpuLoad,
            Vram = sample.Vram
        };
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length + 4 > MapSize)
        {
            return;
        }

        lock (Gate)
        {
            if (_view == IntPtr.Zero)
            {
                return;
            }

            Marshal.WriteInt32(_view, bytes.Length);
            Marshal.Copy(bytes, 0, _view + 4, bytes.Length);
        }
    }

    public static HardwareSampler.GpuSample? TryRead(TimeSpan maxAge)
    {
        try
        {
            EnsureOpen();
            byte[] bytes;
            lock (Gate)
            {
                if (_view == IntPtr.Zero)
                {
                    return null;
                }

                var length = Marshal.ReadInt32(_view);
                if (length is <= 0 or > MapSize - 4)
                {
                    return null;
                }

                bytes = new byte[length];
                Marshal.Copy(_view + 4, bytes, 0, length);
            }

            var payload = JsonSerializer.Deserialize<Payload>(bytes);
            if (payload is null)
            {
                return null;
            }

            var age = DateTime.UtcNow - new DateTime(payload.UtcTicks, DateTimeKind.Utc);
            if (age < TimeSpan.Zero || age > maxAge)
            {
                return null;
            }

            return new HardwareSampler.GpuSample(
                payload.CpuName,
                payload.GpuName,
                payload.GpuId,
                payload.CpuLoad,
                payload.CpuTemp,
                payload.CpuTempSensorName,
                payload.GpuTemp,
                payload.GpuLoad,
                payload.Vram);
        }
        catch
        {
            return null;
        }
    }

    public static bool HasRecentSample() => TryRead(TimeSpan.FromSeconds(5)) is not null;

    public static bool HelperIsRunning() => NamedIpcMutex.Exists(AppConstants.SensorsMutexName);

    private sealed class Payload
    {
        public long UtcTicks { get; set; }
        public string? CpuName { get; set; }
        public string? GpuName { get; set; }
        public string? GpuId { get; set; }
        public double? CpuLoad { get; set; }
        public double? CpuTemp { get; set; }
        public string? CpuTempSensorName { get; set; }
        public double? GpuTemp { get; set; }
        public double? GpuLoad { get; set; }
        public double? Vram { get; set; }
    }
}
