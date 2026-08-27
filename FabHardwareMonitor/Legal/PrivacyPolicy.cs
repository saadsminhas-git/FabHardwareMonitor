using System.IO;
using System.Windows;

namespace FabHardwareMonitor.Legal;

public static class PrivacyPolicy
{
    private static readonly Uri ResourceUri = new("pack://application:,,,/Assets/legal/PRIVACY-AND-TERMS.md");

    private static string? _text;

    public static string Text => _text ??= Load();

    private static string Load()
    {
        var resource = Application.GetResourceStream(ResourceUri)
            ?? throw new InvalidOperationException("Privacy policy resource is missing.");
        using var reader = new StreamReader(resource.Stream);
        return reader.ReadToEnd().Trim();
    }
}
