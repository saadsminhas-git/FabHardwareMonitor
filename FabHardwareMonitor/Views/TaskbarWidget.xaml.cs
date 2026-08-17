using System.Windows;
using System.Windows.Input;
using Deskband11Lib.Core;
using Deskband11Lib.Wpf;

namespace FabHardwareMonitor.Views;

public partial class TaskbarWidget : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public TaskbarWidget()
    {
        InitializeComponent();
        TaskbarContentHost = new TaskbarContentHost(this, (FrameworkElement)Content, new TaskbarContentHostOptions
        {
            PreferredWidth = 320,
            PreferredHeight = 48,
            Placement = TaskbarContentPlacement.BeforeNotificationArea,
            AnimateLayoutChanges = false
        });
    }

    public Task PrepareTaskbarContentAsync() => TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowAppMenu();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        TaskbarContentHost.Dispose();
        base.OnClosed(e);
    }
}
