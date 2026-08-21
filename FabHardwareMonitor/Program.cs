using Velopack;

namespace FabHardwareMonitor;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Services.InstallCleanup.Run())
            .Run();

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
