using System.Runtime.InteropServices;

namespace FabHardwareMonitor.Services;

public sealed class SystemSampler : IDisposable
{
    private long _idle;
    private long _kernel;
    private long _user;
    private bool _primed;

    public (double? Cpu, double? Memory) Sample() => (ReadCpuPercent(), ReadMemoryPercent());

    public void Dispose()
    {
    }

    /// <summary>
    /// Same method Traffic Monitor uses by default. Performance counters are
    /// often missing at logon, which made CPU show as --.
    /// </summary>
    private double? ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        if (!_primed)
        {
            _idle = idle;
            _kernel = kernel;
            _user = user;
            _primed = true;
            return 0;
        }

        var idleDelta = idle - _idle;
        var kernelDelta = kernel - _kernel;
        var userDelta = user - _user;
        _idle = idle;
        _kernel = kernel;
        _user = user;

        var total = kernelDelta + userDelta;
        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(100d * (total - idleDelta) / total, 0, 100);
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
}
