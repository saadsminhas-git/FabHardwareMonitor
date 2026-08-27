using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FabHardwareMonitor.Interop;

internal static class Native
{
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_RBUTTONDBLCLK = 0x0206;
    public const int WM_NCRBUTTONDOWN = 0x00A4;
    public const int WM_NCRBUTTONUP = 0x00A5;
    public const int WM_NCRBUTTONDBLCLK = 0x00A6;
    public const int MA_NOACTIVATE = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    public const int GWL_STYLE = -16;
    private const int GwlExstyle = -20;
    private const int DwmwaCloak = 13;
    private const int DwmwaCloaked = 14;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_POPUP = 0x80000000;
    public const int SwShowNoActivate = 4;
    private const int SwHide = 0;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hwnd, int cmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const uint RdwInvalidate = 0x0001;
    private const uint RdwFrame = 0x0400;
    private const uint RdwUpdateNow = 0x0100;

    public static void NudgeHostedWidget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        Uncloak(hwnd);
        ShowWindow(hwnd, SwShowNoActivate);
        RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwFrame | RdwUpdateNow);
    }

    public static bool IsCloaked(IntPtr hwnd)
    {
        return DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    public static void Uncloak(IntPtr hwnd)
    {
        var value = 0;
        DwmSetWindowAttribute(hwnd, DwmwaCloak, ref value, sizeof(int));
    }

    public static string DescribeWindow(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var rect);
        var style = unchecked((uint)GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64());
        var ex = unchecked((uint)GetWindowLongPtr(hwnd, GwlExstyle).ToInt64());
        var parent = GetParent(hwnd);
        return
            $"parent={GetWindowClassName(parent)} rect={rect.Right - rect.Left}x{rect.Bottom - rect.Top}" +
            $" vis={IsWindowVisible(hwnd)} cloak={IsCloaked(hwnd)} style=0x{style:X} ex=0x{ex:X}";
    }

    public static bool IsStartUiForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        return IsSearchProcess(TryProcessName(pid)) || IsSearchUiWindow(hwnd);
    }

    public static bool IsTaskbarFocusPending()
    {
        if (IsStartSearchUiVisible())
        {
            return true;
        }

        var cls = GetWindowClassName(GetForegroundWindow());
        return string.Equals(cls, "ApplicationManager_DesktopShellWindow", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStartSearchUiVisible()
    {
        if (IsStartUiForeground())
        {
            return true;
        }

        var visible = false;
        EnumWindowsProc callback = (hwnd, _) =>
        {
            if (!IsSearchUiWindow(hwnd))
            {
                return true;
            }

            visible = true;
            return false;
        };
        EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return visible;
    }

    public static async Task WaitForStartUiDismissAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var clearSince = DateTime.UtcNow;
        var seenStart = IsStartSearchUiVisible();
        while (DateTime.UtcNow < deadline)
        {
            if (IsStartSearchUiVisible())
            {
                seenStart = true;
                clearSince = DateTime.UtcNow;
            }
            else if (!seenStart || DateTime.UtcNow - clearSince >= TimeSpan.FromMilliseconds(1500))
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Start Search closes as soon as the result is clicked, so foreground checks
    /// return too early. Wait out the taskbar restore before the first SetParent.
    /// </summary>
    public static async Task WaitForTaskbarIdleAsync(TimeSpan minimum, TimeSpan timeout)
    {
        var origin = DateTime.UtcNow;
        var deadline = origin + timeout;
        var lastTray = GetShellTrayRect();
        var trayStableSince = origin;
        var searchGoneSince = IsTaskbarFocusPending() ? DateTime.MaxValue : origin;
        while (DateTime.UtcNow < deadline)
        {
            var tray = GetShellTrayRect();
            if (!tray.Equals(lastTray))
            {
                lastTray = tray;
                trayStableSince = DateTime.UtcNow;
            }

            if (IsTaskbarFocusPending())
            {
                searchGoneSince = DateTime.MaxValue;
            }
            else if (searchGoneSince == DateTime.MaxValue)
            {
                searchGoneSince = DateTime.UtcNow;
            }

            var waitedMin = DateTime.UtcNow - origin >= minimum;
            var trayStable = DateTime.UtcNow - trayStableSince >= TimeSpan.FromMilliseconds(400);
            var searchGone = searchGoneSince != DateTime.MaxValue
                             && DateTime.UtcNow - searchGoneSince >= TimeSpan.FromMilliseconds(500);
            var waitedCap = DateTime.UtcNow - origin >= TimeSpan.FromSeconds(8);
            if (waitedMin && trayStable && (searchGone || waitedCap))
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    public static string DescribeForeground()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        return
            $"fg={GetWindowClassName(hwnd)} proc={TryProcessName(pid)} " +
            $"search={IsStartSearchUiVisible()} pending={IsTaskbarFocusPending()}";
    }

    public static string DescribeTray()
    {
        var rect = GetShellTrayRect();
        return $"tray={rect.Width}x{rect.Height}";
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var name = new System.Text.StringBuilder(256);
        return GetClassName(hwnd, name, name.Capacity) > 0 ? name.ToString() : "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private static bool IsSearchProcess(string name) =>
        name.Equals("SearchHost", StringComparison.OrdinalIgnoreCase)
        || name.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)
        || name.Equals("SearchApp", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase);

    private static bool IsSearchUiWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        GetWindowRect(hwnd, out var rect);
        if (rect.Right - rect.Left < 160 || rect.Bottom - rect.Top < 160)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (IsSearchProcess(TryProcessName(pid)))
        {
            return true;
        }

        var cls = GetWindowClassName(hwnd);
        return cls.Contains("StartMenu", StringComparison.OrdinalIgnoreCase)
               || cls.Contains("SearchUI", StringComparison.OrdinalIgnoreCase)
               || cls.Contains("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryProcessName(int pid)
    {
        if (pid <= 0)
        {
            return "";
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    private readonly struct TrayRect : IEquatable<TrayRect>
    {
        public TrayRect(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public bool Equals(TrayRect other) => Width == other.Width && Height == other.Height;
    }

    private static TrayRect GetShellTrayRect()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var rect))
        {
            return default;
        }

        return new TrayRect(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static bool IsWidgetPainted(IntPtr hwnd)
    {
        if (!IsHostedInTray(hwnd) || !IsWindowVisible(hwnd))
        {
            return false;
        }

        if (IsCloaked(hwnd))
        {
            return false;
        }

        GetWindowRect(hwnd, out var rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        return width >= 200 && height >= 24;
    }

    public static void ReleaseFromTray(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetParent(hwnd, IntPtr.Zero);
        var style = unchecked((uint)GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64());
        var restored = (style & ~WS_CHILD) | WS_POPUP;
        if (restored != style)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, unchecked((IntPtr)(nint)restored));
        }

        ShowWindow(hwnd, SwHide);
    }

    public static void DetachFromTray(IntPtr hwnd)
    {
        ReleaseFromTray(hwnd);
    }

    public static bool IsHostedInTray(IntPtr hwnd)
    {
        var parent = GetParent(hwnd);
        if (parent == IntPtr.Zero)
        {
            return false;
        }

        var name = new System.Text.StringBuilder(256);
        if (GetClassName(parent, name, name.Capacity) <= 0)
        {
            return false;
        }

        var className = name.ToString();
        return className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    public static void ApplyChildStyle(IntPtr hwnd)
    {
        var style = unchecked((uint)GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64());
        var hosted = (style & ~WS_POPUP) | WS_CHILD;
        if (hosted != style)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, unchecked((IntPtr)(nint)hosted));
        }
    }

    public static bool TryHostInTray(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        ApplyChildStyle(hwnd);
        if (IsHostedInTray(hwnd))
        {
            ShowWindow(hwnd, SwShowNoActivate);
            return true;
        }

        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
        {
            return false;
        }

        SetParent(hwnd, tray);
        ApplyChildStyle(hwnd);
        ShowWindow(hwnd, SwShowNoActivate);
        return IsHostedInTray(hwnd);
    }

    public static async Task WaitForShellTrayAsync(TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (FindWindow("Shell_TrayWnd", null) != IntPtr.Zero)
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    /// <summary>
    /// Named map readable by the Medium-IL widget even when the writer is elevated.
    /// Default CreateOrOpen from High IL is invisible to Explorer-hosted UI (UIPI).
    /// </summary>
    public static SafeMemoryMappedFileHandle CreateMediumIntegrityMap(string name, int size)
    {
        if (TryCreateMediumSecurity(out var descriptor))
        {
            try
            {
                var attributes = Attributes(descriptor);
                var handle = CreateFileMapping(new IntPtr(-1), ref attributes, PageReadWrite, 0, (uint)size, name);
                if (!handle.IsInvalid)
                {
                    return handle;
                }

                handle.Dispose();
            }
            finally
            {
                LocalFree(descriptor);
            }
        }

        return CreateFileMappingUnsecured(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, (uint)size, name);
    }

    public static IntPtr MapView(SafeMemoryMappedFileHandle handle, int size)
    {
        var view = MapViewOfFile(handle, FileMapRead | FileMapWrite, 0, 0, (UIntPtr)(uint)size);
        return view;
    }

    public static bool UnmapView(IntPtr view) => view != IntPtr.Zero && UnmapViewOfFile(view);

    public static SafeWaitHandle CreateMediumIntegrityMutex(string name, bool initiallyOwned, out bool createdNew)
    {
        SafeWaitHandle handle;
        if (TryCreateMediumSecurity(out var descriptor))
        {
            try
            {
                var attributes = Attributes(descriptor);
                handle = CreateMutex(ref attributes, initiallyOwned, name);
            }
            finally
            {
                LocalFree(descriptor);
            }
        }
        else
        {
            handle = CreateMutexUnsecured(IntPtr.Zero, initiallyOwned, name);
        }

        var error = Marshal.GetLastWin32Error();
        createdNew = !handle.IsInvalid && error != ErrorAlreadyExists;
        return handle;
    }

    public static bool TryOpenMutex(string name)
    {
        var handle = OpenMutex(MutexModifyState | Synchronize, false, name);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        handle.Dispose();
        return true;
    }

    public static bool ReleaseMutex(SafeWaitHandle handle) => ReleaseMutex(handle.DangerousGetHandle());

    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x0002;
    private const uint FileMapRead = 0x0004;
    private const uint ErrorAlreadyExists = 183;
    private const uint Synchronize = 0x00100000;
    private const uint MutexModifyState = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    private static SecurityAttributes Attributes(IntPtr descriptor) => new()
    {
        Length = Marshal.SizeOf<SecurityAttributes>(),
        SecurityDescriptor = descriptor,
        InheritHandle = 0
    };

    private static bool TryCreateMediumSecurity(out IntPtr descriptor) =>
        ConvertStringSecurityDescriptorToSecurityDescriptor(
            "D:(A;;GA;;;WD)S:(ML;;NW;;;ME)",
            1,
            out descriptor,
            IntPtr.Zero);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        IntPtr securityDescriptorSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeMemoryMappedFileHandle CreateFileMapping(
        IntPtr file,
        ref SecurityAttributes fileMappingAttributes,
        uint protect,
        uint maximumSizeHigh,
        uint maximumSizeLow,
        string name);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileMappingW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeMemoryMappedFileHandle CreateFileMappingUnsecured(
        IntPtr file,
        IntPtr fileMappingAttributes,
        uint protect,
        uint maximumSizeHigh,
        uint maximumSizeLow,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(
        SafeMemoryMappedFileHandle fileMappingObject,
        uint desiredAccess,
        uint fileOffsetHigh,
        uint fileOffsetLow,
        UIntPtr numberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr baseAddress);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateMutex(
        ref SecurityAttributes mutexAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool initialOwner,
        string name);

    [DllImport("kernel32.dll", EntryPoint = "CreateMutexW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateMutexUnsecured(
        IntPtr mutexAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool initialOwner,
        string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle OpenMutex(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseMutex(IntPtr mutex);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    private const uint ExtendedStartupinfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const uint ProcessCreateProcess = 0x0080;
    private const int ProcThreadAttributeParentProcess = 0x00020000;
    private const int StartfUseShowWindow = 0x00000001;
    private const int StartfTitleIsLinkName = 0x00000800;
    private const int StartfTitleIsAppId = 0x00001000;
    private const short SwShownormal = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Ptr;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStartupInfoW")]
    private static extern void GetStartupInfo(out StartupInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    public static bool LaunchedFromShortcut()
    {
        var info = new StartupInfo { Cb = Marshal.SizeOf<StartupInfo>() };
        GetStartupInfo(out info);
        return (info.Flags & (StartfTitleIsLinkName | StartfTitleIsAppId)) != 0;
    }

    /// <summary>
    /// Start search resolves the app by AppUserModel ID (TITLEISAPPID) instead of
    /// opening the Programs .lnk (TITLEISLINKNAME). All apps / desktop work via .lnk.
    /// </summary>
    public static bool LaunchedFromAppResolver()
    {
        var info = new StartupInfo { Cb = Marshal.SizeOf<StartupInfo>() };
        GetStartupInfo(out info);
        return (info.Flags & StartfTitleIsAppId) != 0 && (info.Flags & StartfTitleIsLinkName) == 0;
    }

    public static int GetStartupFlags()
    {
        var info = new StartupInfo { Cb = Marshal.SizeOf<StartupInfo>() };
        GetStartupInfo(out info);
        return info.Flags;
    }

    public static string GetStartupTitle()
    {
        var info = new StartupInfo { Cb = Marshal.SizeOf<StartupInfo>() };
        GetStartupInfo(out info);
        return info.Title == IntPtr.Zero ? "" : Marshal.PtrToStringUni(info.Title) ?? "";
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength);

    public static string? TryReadProcessCommandLine(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(1024);
        try
        {
            var status = NtQueryInformationProcess(
                handle,
                ProcessCommandLineInformation,
                buffer,
                1024,
                out _);
            if (status != 0)
            {
                return null;
            }

            var command = Marshal.PtrToStructure<UnicodeString>(buffer);
            if (command.Buffer == IntPtr.Zero || command.Length == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUni(command.Buffer, command.Length / 2);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            CloseHandle(handle);
        }
    }

    public static bool IsExplorerParent(string parent) =>
        parent.StartsWith("explorer.exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsSearchActivation(string parent) =>
        parent.Contains("SearchHost", StringComparison.OrdinalIgnoreCase)
        || parent.Contains("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)
        || parent.Contains("SearchApp", StringComparison.OrdinalIgnoreCase);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr appId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        IntPtr attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? applicationName,
        System.Text.StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    private const uint Th32csSnapProcess = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessSimple(
        string? applicationName,
        System.Text.StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    public static void SetAppUserModelId(string id)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(id);
        }
        catch
        {
            // Window creation still proceeds; LaunchTask is the real detach.
        }
    }

    public static string GetAppUserModelId()
    {
        if (GetCurrentProcessExplicitAppUserModelID(out var pointer) != 0 || pointer == IntPtr.Zero)
        {
            return "";
        }

        try
        {
            return Marshal.PtrToStringUni(pointer) ?? "";
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    public static string ParentProcessName()
    {
        var pid = GetCurrentProcessId();
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return "";
        }

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref entry))
            {
                return "";
            }

            uint parent = 0;
            do
            {
                if (entry.ProcessId == pid)
                {
                    parent = entry.ParentProcessId;
                    break;
                }
            }
            while (Process32Next(snapshot, ref entry));

            if (parent == 0)
            {
                return "";
            }

            entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
            if (!Process32First(snapshot, ref entry))
            {
                return parent.ToString();
            }

            do
            {
                if (entry.ProcessId == parent)
                {
                    return $"{entry.ExeFile} ({parent})";
                }
            }
            while (Process32Next(snapshot, ref entry));

            return parent.ToString();
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    /// <summary>
    /// Starts <paramref name="exe"/> outside the current job when the job allows
    /// breakaway. Used when Task Scheduler cannot run the widget.
    /// </summary>
    public static bool StartProcessBreakaway(string exe, string arguments) =>
        StartProcessClean(exe, arguments, breakaway: true);

    public static bool StartProcessClean(string exe, string arguments, bool breakaway = false)
    {
        var command = new System.Text.StringBuilder(
            string.IsNullOrWhiteSpace(arguments) ? $"\"{exe}\"" : $"\"{exe}\" {arguments}");
        var startup = new StartupInfo
        {
            Cb = Marshal.SizeOf<StartupInfo>(),
            Flags = StartfUseShowWindow,
            ShowWindow = SwShownormal
        };
        var flags = CreateUnicodeEnvironment;
        if (breakaway)
        {
            flags |= CreateBreakawayFromJob;
        }

        if (!CreateProcessSimple(
                exe,
                command,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                flags,
                IntPtr.Zero,
                System.IO.Path.GetDirectoryName(exe),
                ref startup,
                out var info))
        {
            return false;
        }

        if (info.Process != IntPtr.Zero)
        {
            CloseHandle(info.Process);
        }

        if (info.Thread != IntPtr.Zero)
        {
            CloseHandle(info.Thread);
        }

        return true;
    }

    /// <summary>
    /// Starts <paramref name="exe"/> as a child of Explorer so it does not inherit
    /// the Start Menu AppUserModelID or job. A normal CreateProcess would.
    /// </summary>
    public static bool StartProcessFromExplorer(string exe, string arguments)
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || GetWindowThreadProcessId(tray, out var explorerId) == 0 || explorerId <= 0)
        {
            return false;
        }

        var parent = OpenProcess(ProcessCreateProcess, false, explorerId);
        if (parent == IntPtr.Zero)
        {
            return false;
        }

        var attributeList = IntPtr.Zero;
        var parentValue = IntPtr.Zero;
        try
        {
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            if (size == 0)
            {
                return false;
            }

            attributeList = Marshal.AllocHGlobal((nint)size);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                Marshal.FreeHGlobal(attributeList);
                attributeList = IntPtr.Zero;
                return false;
            }

            parentValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(parentValue, parent);
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)ProcThreadAttributeParentProcess,
                    parentValue,
                    (nuint)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                return false;
            }

            var command = new System.Text.StringBuilder(
                string.IsNullOrWhiteSpace(arguments) ? $"\"{exe}\"" : $"\"{exe}\" {arguments}");
            var startup = new StartupInfoEx
            {
                StartupInfo =
                {
                    Cb = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = StartfUseShowWindow,
                    ShowWindow = SwShownormal
                },
                AttributeList = attributeList
            };
            var created = CreateProcess(
                exe,
                command,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ExtendedStartupinfoPresent | CreateUnicodeEnvironment | CreateBreakawayFromJob,
                IntPtr.Zero,
                System.IO.Path.GetDirectoryName(exe),
                ref startup,
                out var info);
            if (!created)
            {
                return false;
            }

            if (info.Process != IntPtr.Zero)
            {
                CloseHandle(info.Process);
            }

            if (info.Thread != IntPtr.Zero)
            {
                CloseHandle(info.Thread);
            }

            return true;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (parentValue != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(parentValue);
            }

            CloseHandle(parent);
        }
    }
}
