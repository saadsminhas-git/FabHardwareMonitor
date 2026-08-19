using System.Windows;

namespace FabHardwareMonitor.Views;

public partial class AppMenuWindow : Window
{
    private bool _closing;
    private Action? _afterClose;

    public AppMenuWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            var next = _afterClose;
            _afterClose = null;
            next?.Invoke();
        };
    }

    public string AboutLabel
    {
        get => AboutButton.Content as string ?? "About";
        set => AboutButton.Content = value;
    }

    public void ShowAt(Point dipCursor)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -2000;
        Top = -2000;
        Show();
        UpdateLayout();

        var work = SystemParameters.WorkArea;
        var left = dipCursor.X;
        var top = dipCursor.Y - ActualHeight;
        if (top < work.Top)
        {
            top = dipCursor.Y;
        }

        if (left + ActualWidth > work.Right)
        {
            left = work.Right - ActualWidth;
        }

        if (left < work.Left)
        {
            left = work.Left;
        }

        Left = left;
        Top = top;
        Activate();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Dismiss();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Dismiss();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void OnSettings(object sender, RoutedEventArgs e) => Dismiss(SettingsChosen);

    private void OnAbout(object sender, RoutedEventArgs e) => Dismiss(AboutChosen);

    private void OnExit(object sender, RoutedEventArgs e) => Dismiss(ExitChosen);

    public event Action? SettingsChosen;
    public event Action? AboutChosen;
    public event Action? ExitChosen;

    private void Dismiss(Action? afterClose = null)
    {
        if (_closing)
        {
            if (afterClose is not null)
            {
                _afterClose += afterClose;
            }

            return;
        }

        _closing = true;
        _afterClose = afterClose;
        Close();
    }
}
