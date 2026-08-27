using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FabHardwareMonitor.Models;
using FabHardwareMonitor.Services;
using Brush = System.Windows.Media.Brush;

namespace FabHardwareMonitor.ViewModels;

public sealed partial class TaskbarViewModel : ObservableObject
{
    [ObservableProperty] private string _uploadText = "0.0 K/s";
    [ObservableProperty] private string _downloadText = "0.0 K/s";
    [ObservableProperty] private string _cpuText = "--";
    [ObservableProperty] private string _memText = "--";
    [ObservableProperty] private string _gpuText = "--";
    [ObservableProperty] private string _vramText = "--";
    [ObservableProperty] private string _cpuTempText = "--";
    [ObservableProperty] private string _gpuTempText = "--";
    [ObservableProperty] private Brush _labelBrush = Solid(Thresholds.DarkTaskbar.Label);
    [ObservableProperty] private Brush _uploadBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _downloadBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _cpuBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _memBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _gpuBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _vramBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _cpuTempBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _gpuTempBrush = Solid(Thresholds.White);
    [ObservableProperty] private Brush _plateBrush = Solid(Thresholds.DarkTaskbar.Plate);
    [ObservableProperty] private Brush _shellBrush = Solid(Thresholds.DarkShell);
    [ObservableProperty] private bool _showPawnIoWarning;
    public Brush RailBrush { get; } = Solid(Thresholds.Accent);

    private Color _preferredDarkValue = Thresholds.White;
    private HardwareSnapshot? _last;

    public void ApplySettings(AppSettings settings)
    {
        _preferredDarkValue = Thresholds.ParseOrDefault(settings.TextColor);
        Paint();
    }

    public void ApplyShellTheme() => Paint();

    public void Apply(HardwareSnapshot snapshot)
    {
        _last = snapshot;
        UploadText = Thresholds.FormatRate(snapshot.UploadBytesPerSec);
        DownloadText = Thresholds.FormatRate(snapshot.DownloadBytesPerSec);
        CpuText = Thresholds.FormatPercent(snapshot.CpuUsage);
        MemText = Thresholds.FormatPercent(snapshot.MemoryUsage);
        GpuText = Thresholds.FormatPercent(snapshot.GpuUsage);
        VramText = Thresholds.FormatPercent(snapshot.VramUsage);
        CpuTempText = Thresholds.FormatTemp(snapshot.CpuTemp);
        GpuTempText = Thresholds.FormatTemp(snapshot.GpuTemp);
        Paint();
    }

    private void Paint()
    {
        var lightBar = ThemeService.TaskbarIsLight;
        var palette = Thresholds.ForTaskbar(lightBar);
        if (!lightBar)
        {
            palette = palette with { Value = _preferredDarkValue };
        }

        LabelBrush = Solid(palette.Label);
        PlateBrush = Solid(palette.Plate);
        ShellBrush = Solid(lightBar ? Thresholds.LightShell : Thresholds.DarkShell);
        var value = Solid(palette.Value);
        UploadBrush = value;
        DownloadBrush = value;

        var snap = _last;
        if (snap is null)
        {
            CpuBrush = value;
            MemBrush = value;
            GpuBrush = value;
            VramBrush = value;
            CpuTempBrush = value;
            GpuTempBrush = value;
            return;
        }

        CpuBrush = Solid(Thresholds.ForUsage(snap.CpuUsage, palette));
        MemBrush = Solid(Thresholds.ForUsage(snap.MemoryUsage, palette));
        GpuBrush = Solid(Thresholds.ForUsage(snap.GpuUsage, palette));
        VramBrush = Solid(Thresholds.ForUsage(snap.VramUsage, palette));
        CpuTempBrush = Solid(Thresholds.ForTemp(snap.CpuTemp, palette));
        GpuTempBrush = Solid(Thresholds.ForTemp(snap.GpuTemp, palette));
    }

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
