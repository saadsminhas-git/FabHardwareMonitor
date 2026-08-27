using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Deskband11Lib.Core;
using Deskband11Lib.Wpf;
using FabHardwareMonitor.Interop;
using FabHardwareMonitor.Services;

namespace FabHardwareMonitor.Views;

public partial class TaskbarWidget : Window
{
    private HwndSource? _source;

    public TaskbarContentHost TaskbarContentHost { get; }

    public TaskbarWidget()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
        ShowActivated = false;
        Visibility = Visibility.Hidden;
        TaskbarContentHost = new TaskbarContentHost(this, (FrameworkElement)Content, new TaskbarContentHostOptions
        {
            PreferredWidth = 336,
            PreferredHeight = 48,
            Placement = TaskbarContentPlacement.BeforeNotificationArea,
            AnimateLayoutChanges = false
        });
    }

    public async Task<bool> AttachAndShowAsync()
    {
        await Native.WaitForShellTrayAsync(TimeSpan.FromSeconds(20));
        var fromShell = Environment.GetCommandLineArgs().Any(a =>
            string.Equals(a, AppConstants.ShellArgument, StringComparison.OrdinalIgnoreCase));
        if (fromShell)
        {
            await Native.WaitForTaskbarIdleAsync(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
            LaunchLog.Write($"taskbar idle {Native.DescribeForeground()} {Native.DescribeTray()}");
        }
        else
        {
            await Native.WaitForStartUiDismissAsync(TimeSpan.FromSeconds(8));
            LaunchLog.Write(Native.IsStartSearchUiVisible() ? "start ui still open" : "start ui dismissed");
        }

        return await TryAttachPassAsync(fromShell ? "shell-1" : "standard-1");
    }

    public async Task RetryAttachAsync()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && Native.IsHostedInTray(hwnd))
        {
            LaunchLog.Write($"reattach skipped already hosted {Native.DescribeWindow(hwnd)}");
            Native.NudgeHostedWidget(hwnd);
            TaskbarContentHost.RefreshLayout();
            return;
        }

        await Native.WaitForTaskbarIdleAsync(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        await TryAttachPassAsync("retry");
    }

    public void AbandonAttach()
    {
        Native.ReleaseFromTray(new WindowInteropHelper(this).Handle);
        try
        {
            Close();
        }
        catch
        {
            // Explorer may already have destroyed the HWND.
        }
    }

    private async Task<bool> TryAttachPassAsync(string label)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        await TaskbarContentHost.AttachWhenLayoutReadyAsync();

        if (!Native.IsHostedInTray(hwnd))
        {
            Native.TryHostInTray(hwnd);
        }

        if (!Native.IsHostedInTray(hwnd))
        {
            LaunchLog.Write($"widget not hosted {label} {Native.DescribeWindow(hwnd)}");
            Native.ReleaseFromTray(hwnd);
            return false;
        }

        LaunchLog.Write($"widget hosted {label}");
        await FinalizeHostedAsync(hwnd);
        LaunchLog.Write($"widget after-show {Native.DescribeWindow(hwnd)}");
        return !Native.IsCloaked(hwnd) && Native.IsHostedInTray(hwnd);
    }

    private async Task FinalizeHostedAsync(IntPtr hwnd)
    {
        Visibility = Visibility.Visible;
        Show();
        Native.NudgeHostedWidget(hwnd);
        TaskbarContentHost.RefreshLayout();

        for (var pass = 1; pass <= 4; pass++)
        {
            await Task.Delay(200);
            TaskbarContentHost.RefreshLayout();
            Native.NudgeHostedWidget(hwnd);
        }
    }

    private void OnPawnIoWarningClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (Application.Current as App)?.ShowSettings(highlightPawnIo: true);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = PresentationSource.FromVisual(this) as HwndSource;
        if (_source?.CompositionTarget is { } target)
        {
            target.RenderMode = RenderMode.SoftwareOnly;
        }

        _source?.AddHook(OnMessage);
    }

    /// <summary>
    /// The widget lives inside Shell_TrayWnd, so any right-click message left
    /// unhandled reaches the taskbar and opens its menu on top of ours. Claim
    /// the whole right-click sequence here rather than routing through WPF.
    /// </summary>
    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Native.WM_MOUSEACTIVATE:
                handled = true;
                return new IntPtr(Native.MA_NOACTIVATE);
            case Native.WM_RBUTTONDOWN:
            case Native.WM_NCRBUTTONDOWN:
            case Native.WM_RBUTTONDBLCLK:
            case Native.WM_NCRBUTTONDBLCLK:
            case Native.WM_CONTEXTMENU:
                handled = true;
                break;
            case Native.WM_RBUTTONUP:
            case Native.WM_NCRBUTTONUP:
                handled = true;
                Dispatcher.BeginInvoke(() => (Application.Current as App)?.ShowAppMenu());
                break;
        }

        return IntPtr.Zero;
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
    {
        e.Handled = true;
        Dispatcher.BeginInvoke(() => (Application.Current as App)?.ShowAppMenu());
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(OnMessage);
        _source = null;
        TaskbarContentHost.Dispose();
        base.OnClosed(e);
    }
}
