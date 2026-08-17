using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using FabHardwareMonitor.ViewModels;

namespace FabHardwareMonitor.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLogoClick(object sender, MouseButtonEventArgs e) =>
        Open(AppConstants.WebsiteUrl);

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Open(e.Uri.ToString());
        e.Handled = true;
    }

    private static void Open(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
