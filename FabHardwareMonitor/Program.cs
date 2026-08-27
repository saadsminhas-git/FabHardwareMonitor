using System.IO;
using FabHardwareMonitor.Interop;
using FabHardwareMonitor.Services;
using Velopack;

namespace FabHardwareMonitor;

public static class Program
{
    public static bool VelopackInitialized { get; private set; }
    public static bool StartedFromLink { get; private set; }
    public static bool ShellRetry { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var shell = HasArg(args, AppConstants.ShellArgument);
        ShellRetry = HasArg(args, AppConstants.ShellRetryArgument);
        var flags = Native.GetStartupFlags();
        StartedFromLink = (flags & 0x800) != 0;
        var appResolver = Native.LaunchedFromAppResolver();
        LaunchLog.Write(
            $"main flags=0x{flags:X} title={Native.GetStartupTitle()} " +
            $"shell={shell} retry={ShellRetry} resolver={appResolver} link={StartedFromLink}");

        if (shell)
        {
            LaunchTask.TryReparentUnderExplorer();
            RunWidget();
            return;
        }

        // Search / Start .lnk must not host in-process. That process keeps
        // Explorer's ApplicationManager_DesktopShellWindow as foreground and
        // Deskband11 will not parent into the tray. --shell (autostart path) does.
        if (appResolver || StartedFromLink)
        {
            HandoffSearch();
            return;
        }

        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => InstallCleanup.Run())
            .Run();
        VelopackInitialized = true;

        if (HasArg(args, AppConstants.SensorsArgument))
        {
            SensorHost.Run();
            return;
        }

        if (Elevation.IsAdministrator())
        {
            ShortcutFix.Rewrite();
            SensorTask.Register();
            LaunchTask.EnsureRegistered();
            SensorHost.Run(() => Elevation.StartUnelevated(AppConstants.ShellArgument));
            return;
        }

        var parent = Native.ParentProcessName();
        if (Native.IsExplorerParent(parent))
        {
            if (NamedIpcMutex.Exists(AppConstants.MutexName))
            {
                LaunchLog.Write($"explorer already running parent={parent}");
                SignalRunningInstance();
                Environment.Exit(0);
            }

            LaunchLog.Write($"explorer in-process parent={parent}");
            RunWidget();
            return;
        }

        LaunchTask.TryStart();
        LaunchLog.Write("detach exit");
        Environment.Exit(0);
    }

    private static void HandoffSearch()
    {
        LaunchLog.Write(
            $"search handoff link={StartedFromLink} flags=0x{Native.GetStartupFlags():X} " +
            $"title={Native.GetStartupTitle()}");
        if (NamedIpcMutex.Exists(AppConstants.MutexName))
        {
            LaunchLog.Write("search ui already running");
            SignalRunningInstance();
            Environment.Exit(0);
            return;
        }

        WaitForHandoffSettle();

        LaunchTask.TryStartFromScheduler();
        LaunchLog.Write("search handoff exit");
        Environment.Exit(0);
    }

    private static void WaitForHandoffSettle()
    {
        var origin = DateTime.UtcNow;
        var min = TimeSpan.FromSeconds(5);
        var max = TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow - origin < max)
        {
            if (DateTime.UtcNow - origin >= min && !Native.IsTaskbarFocusPending())
            {
                break;
            }

            Thread.Sleep(100);
        }

        LaunchLog.Write($"search handoff settled {Native.DescribeForeground()} {Native.DescribeTray()}");
    }

    public static void SignalRunningInstance()
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(AppConstants.ReattachEventName);
            ev.Set();
            LaunchLog.Write("signaled reattach");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"reattach signal failed={ex.GetType().Name}");
        }
    }

    private static void RunWidget()
    {
        LaunchLog.Write(
            $"shell image={Path.GetFileName(Environment.ProcessPath)} path={Environment.ProcessPath} " +
            $"parent={Native.ParentProcessName()} " +
            $"flags=0x{Native.GetStartupFlags():X} shortcut={Native.LaunchedFromShortcut()} " +
            $"title={Native.GetStartupTitle()} aumid={Native.GetAppUserModelId()}");

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }

    private static bool HasArg(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}
