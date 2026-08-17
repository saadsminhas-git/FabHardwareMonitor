using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FabHardwareMonitor.Services;

public sealed class SystemSampler : IDisposable
{
    private PerformanceCounter? _utility;
    private PerformanceCounter? _time;
    private bool _primed;

    public SystemSampler()
    {
        _utility = TryCreate("Processor Information", "% Processor Utility", "_Total")
                   ?? TryCreate("Processor", "% Processor Utility", "_Total");
        _time = TryCreate("Processor", "% Processor Time", "_Total");
    }

    public (double? Cpu, double? Memory) Sample()
    {
        double? cpu = null;
        try
        {
            var counter = _utility ?? _time;
            if (counter is not null)
            {
                var value = counter.NextValue();
                if (!_primed)
                {
                    _primed = true;
                    value = counter.NextValue();
                }

                cpu = Math.Clamp(value, 0, 100);
            }
        }
        catch
        {
            cpu = null;
        }

        return (cpu, ReadMemoryPercent());
    }

    public void Dispose()
    {
        _utility?.Dispose();
        _time?.Dispose();
    }

    private static PerformanceCounter? TryCreate(string category, string counter, string instance)
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(category))
            {
                return null;
            }

            var created = new PerformanceCounter(category, counter, instance, readOnly: true);
            _ = created.NextValue();
            return created;
        }
        catch
        {
            return null;
        }
    }

    private static double? ReadMemoryPercent()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return null;
        }

        var used = status.TotalPhys - status.AvailPhys;
        return Math.Clamp(used * 100d / status.TotalPhys, 0, 100);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
}
