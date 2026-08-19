using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace FabHardwareMonitor.Services;

/// <summary>
/// About and Settings follow AppsUseLightTheme. The taskbar readout follows
/// SystemUsesLightTheme so values stay readable on a light or dark shell.
/// </summary>
public static class ThemeService
{
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private static readonly IntPtr HwndMessage = new(-3);

    private static HwndSource? _watch;

    public static bool TaskbarIsLight { get; private set; }

    public static event Action? Changed;

    public static void Start()
    {
        ApplyFromOs();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;

        var parameters = new HwndSourceParameters("FabThemeWatch")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HwndMessage
        };
        _watch = new HwndSource(parameters);
        _watch.AddHook(OnMessage);
    }

    public static void Stop()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        _watch?.RemoveHook(OnMessage);
        _watch?.Dispose();
        _watch = null;
    }

    public static void ApplyFromOs()
    {
        TaskbarIsLight = ReadThemeValue("SystemUsesLightTheme");
        Apply(ReadThemeValue("AppsUseLightTheme"));
        Changed?.Invoke();
    }

    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color))
        {
            return;
        }

        DispatchApply();
    }

    private static IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmThemeChanged)
        {
            DispatchApply();
        }
        else if (msg == WmSettingChange)
        {
            var name = Marshal.PtrToStringAuto(lParam);
            if (name is "ImmersiveColorSet")
            {
                DispatchApply();
            }
        }

        return IntPtr.Zero;
    }

    private static void DispatchApply()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            ApplyFromOs();
            return;
        }

        app.Dispatcher.BeginInvoke(ApplyFromOs);
    }

    private static bool ReadThemeValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue(name) is int and > 0 or long and > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Apply(bool light)
    {
        if (Application.Current is null)
        {
            return;
        }

        var uri = new Uri(
            light
                ? "pack://application:,,,/Themes/Palette.Light.xaml"
                : "pack://application:,,,/Themes/Palette.Dark.xaml",
            UriKind.Absolute);

        var palette = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Contains("InkBrush"))
            {
                merged[i] = palette;
                return;
            }
        }

        merged.Insert(0, palette);
    }
}
