using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Deskband11Lib.Core;
using Deskband11Lib.Wpf;
using FabHardwareMonitor.Interop;

namespace FabHardwareMonitor.Views;

public partial class TaskbarWidget : Window
{
    private HwndSource? _source;
    private int _lastPaintWidth;
    private int _lastPaintHeight;
    private bool _presentQueued;

    public TaskbarContentHost TaskbarContentHost { get; }

    public TaskbarWidget()
    {
        InitializeComponent();
        TaskbarContentHost = new TaskbarContentHost(this, (FrameworkElement)Content, new TaskbarContentHostOptions
        {
            PreferredWidth = 336,
            PreferredHeight = 48,
            Placement = TaskbarContentPlacement.BeforeNotificationArea,
            AnimateLayoutChanges = false
        });
    }

    public Task PrepareTaskbarContentAsync() => TaskbarContentHost.AttachWhenLayoutReadyAsync();

    public async Task PresentAsync()
    {
        Show();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        Present();
        for (var i = 0; i < 40 && !HasClientArea(); i++)
        {
            await Task.Delay(50);
        }

        Present();
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
            case Native.WM_SIZE:
            case Native.WM_WINDOWPOSCHANGED:
                QueuePresent();
                break;
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
                Dispatcher.BeginInvoke(() => (Application.Current as App)?.ShowAppMenu(), DispatcherPriority.ApplicationIdle);
                break;
        }

        return IntPtr.Zero;
    }

    private void QueuePresent()
    {
        if (_presentQueued)
        {
            return;
        }

        _presentQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _presentQueued = false;
            Present();
        }, DispatcherPriority.Render);
    }

    private bool HasClientArea()
    {
        var hwnd = _source?.Handle ?? IntPtr.Zero;
        return hwnd != IntPtr.Zero
            && Native.GetClientRect(hwnd, out var rect)
            && rect.Width > 1
            && rect.Height > 1;
    }

    private void Present()
    {
        var hwnd = _source?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero || !Native.GetClientRect(hwnd, out var rect) || rect.Width <= 1 || rect.Height <= 1)
        {
            return;
        }

        EnsureLayered(hwnd);
        var sizeChanged = rect.Width != _lastPaintWidth || rect.Height != _lastPaintHeight;
        _lastPaintWidth = rect.Width;
        _lastPaintHeight = rect.Height;
        InvalidateVisual();
        if (Content is UIElement content)
        {
            content.InvalidateVisual();
            if (sizeChanged)
            {
                content.InvalidateMeasure();
                content.InvalidateArrange();
            }
        }

        UpdateLayout();
        Native.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, Native.RDW_INVALIDATE | Native.RDW_UPDATENOW | Native.RDW_ALLCHILDREN);
    }

    private static void EnsureLayered(IntPtr hwnd)
    {
        var ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        if ((ex & Native.WS_EX_LAYERED) != 0)
        {
            return;
        }

        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex | Native.WS_EX_LAYERED));
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
    {
        e.Handled = true;
        Dispatcher.BeginInvoke(() => (Application.Current as App)?.ShowAppMenu(), DispatcherPriority.ApplicationIdle);
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(OnMessage);
        _source = null;
        TaskbarContentHost.Dispose();
        base.OnClosed(e);
    }
}
