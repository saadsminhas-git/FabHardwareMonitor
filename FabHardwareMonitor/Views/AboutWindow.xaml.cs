using System.Windows;
using FabHardwareMonitor.ViewModels;

namespace FabHardwareMonitor.Views;

public partial class AboutWindow : FabWindow
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnPrivacy(object sender, RoutedEventArgs e)
    {
        var window = new ConsentWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }
}
