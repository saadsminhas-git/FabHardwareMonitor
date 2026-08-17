using Velopack;

namespace FabHardwareMonitor;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => new Services.AutostartService().Unregister())
            .Run();

        if (Elevation.RelaunchElevated())
        {
            return;
        }

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
