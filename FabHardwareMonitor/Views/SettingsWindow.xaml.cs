using System.Windows;
using FabHardwareMonitor.ViewModels;

namespace FabHardwareMonitor.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => Close();
}
