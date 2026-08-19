using FabHardwareMonitor.Legal;

namespace FabHardwareMonitor.Views;

public partial class ConsentWindow : FabWindow
{
    public ConsentWindow()
    {
        InitializeComponent();
        PolicyText.Text = PrivacyPolicy.Text;
    }

    private void OnClose(object sender, System.Windows.RoutedEventArgs e) => Close();
}
