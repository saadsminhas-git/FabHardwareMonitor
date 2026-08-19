using System.Windows;
using System.Windows.Media.Animation;
using FabHardwareMonitor.ViewModels;

namespace FabHardwareMonitor.Views;

public partial class SettingsWindow : FabWindow
{
    private int _highlightGeneration;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public async void HighlightPawnIo()
    {
        var generation = ++_highlightGeneration;
        PawnIoHighlight.BeginAnimation(OpacityProperty, null);
        PawnIoHighlight.Opacity = 1;
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (generation != _highlightGeneration || !IsLoaded)
        {
            return;
        }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PawnIoHighlight.BeginAnimation(OpacityProperty, fade);
    }

    private void OnSave(object sender, RoutedEventArgs e) => Close();

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
