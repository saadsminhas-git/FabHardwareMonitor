using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FabHardwareMonitor.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

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
    [ObservableProperty] private Brush _uploadBrush = Brushes.White;
    [ObservableProperty] private Brush _downloadBrush = Brushes.White;
    [ObservableProperty] private Brush _cpuBrush = Brushes.White;
    [ObservableProperty] private Brush _memBrush = Brushes.White;
    [ObservableProperty] private Brush _gpuBrush = Brushes.White;
    [ObservableProperty] private Brush _vramBrush = Brushes.White;
    [ObservableProperty] private Brush _cpuTempBrush = Brushes.White;
    [ObservableProperty] private Brush _gpuTempBrush = Brushes.White;
    [ObservableProperty] private bool _showVram = true;

    private Color _base = Thresholds.White;

    public void ApplySettings(AppSettings settings)
    {
        _base = Thresholds.ParseOrDefault(settings.TextColor);
        ShowVram = settings.ShowVram;
        var fallback = Solid(_base);
        UploadBrush = fallback;
        DownloadBrush = fallback;
    }

    public void Apply(HardwareSnapshot snapshot)
    {
        var fallback = _base;
        UploadText = Thresholds.FormatRate(snapshot.UploadBytesPerSec);
        DownloadText = Thresholds.FormatRate(snapshot.DownloadBytesPerSec);
        CpuText = Thresholds.FormatPercent(snapshot.CpuUsage);
        MemText = Thresholds.FormatPercent(snapshot.MemoryUsage);
        GpuText = Thresholds.FormatPercent(snapshot.GpuUsage);
        VramText = Thresholds.FormatPercent(snapshot.VramUsage);
        CpuTempText = Thresholds.FormatTemp(snapshot.CpuTemp);
        GpuTempText = Thresholds.FormatTemp(snapshot.GpuTemp);

        UploadBrush = Solid(fallback);
        DownloadBrush = Solid(fallback);
        CpuBrush = Solid(Thresholds.ForUsage(snapshot.CpuUsage, fallback));
        MemBrush = Solid(Thresholds.ForUsage(snapshot.MemoryUsage, fallback));
        GpuBrush = Solid(Thresholds.ForUsage(snapshot.GpuUsage, fallback));
        VramBrush = Solid(Thresholds.ForUsage(snapshot.VramUsage, fallback));
        CpuTempBrush = Solid(Thresholds.ForTemp(snapshot.CpuTemp, fallback));
        GpuTempBrush = Solid(Thresholds.ForTemp(snapshot.GpuTemp, fallback));
    }

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
