using Velopack;

namespace FabHardwareMonitor;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => new Services.AutostartService().Unregister())
            .OnFirstRun(_ => new Services.AutostartService().Register())
            .Run();

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
