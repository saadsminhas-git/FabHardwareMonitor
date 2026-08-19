using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Deskband11Lib.Core;
using Deskband11Lib.Wpf;
using FabHardwareMonitor.Interop;

namespace FabHardwareMonitor.Views;

public partial class TaskbarWidget : Window
{
    private HwndSource? _source;

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

    private void OnPawnIoWarningClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (Application.Current as App)?.ShowSettings(highlightPawnIo: true);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = PresentationSource.FromVisual(this) as HwndSource;
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
