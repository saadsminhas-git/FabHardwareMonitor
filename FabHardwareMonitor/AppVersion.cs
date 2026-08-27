using System.Reflection;

namespace FabHardwareMonitor;

public static class AppVersion
{
    public static string Display
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            var raw = string.IsNullOrWhiteSpace(informational)
                ? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0"
                : informational;

            var plus = raw.IndexOf('+');
            if (plus >= 0)
            {
                raw = raw[..plus];
            }

            return raw;
        }
    }
}
