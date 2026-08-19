using System.Windows;
using System.Windows.Input;

namespace FabHardwareMonitor.Views;

/// <summary>
/// Base for the app's custom-chrome windows. Supplies the close command the
/// caption bar template binds to, plus Escape to dismiss.
/// </summary>
public abstract class FabWindow : Window
{
    protected FabWindow()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
        ShowInTaskbar = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
