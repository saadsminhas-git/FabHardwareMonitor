using FabHardwareMonitor.Interop;
using Microsoft.Win32.SafeHandles;

namespace FabHardwareMonitor.Services;

internal sealed class NamedIpcMutex : IDisposable
{
    private readonly SafeWaitHandle _handle;
    private bool _owned;

    private NamedIpcMutex(SafeWaitHandle handle, bool owned)
    {
        _handle = handle;
        _owned = owned;
    }

    public static NamedIpcMutex Create(string name, bool initiallyOwned, out bool createdNew)
    {
        var handle = Native.CreateMediumIntegrityMutex(name, initiallyOwned, out createdNew);
        return new NamedIpcMutex(handle, initiallyOwned && createdNew);
    }

    public static bool Exists(string name) => Native.TryOpenMutex(name);

    public void ReleaseIfOwned()
    {
        if (!_owned)
        {
            return;
        }

        Native.ReleaseMutex(_handle);
        _owned = false;
    }

    public void Dispose()
    {
        ReleaseIfOwned();
        _handle.Dispose();
    }
}
