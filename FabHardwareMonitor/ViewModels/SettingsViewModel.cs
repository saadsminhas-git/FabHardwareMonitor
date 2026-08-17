using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FabHardwareMonitor.Models;
using FabHardwareMonitor.Services;

namespace FabHardwareMonitor.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly AutostartService _autostart;
    private readonly UpdateService _updates;
    private readonly HardwarePipeline _pipeline;
    private readonly Action<AppSettings> _applied;
    private readonly AppSettings _settings;

    public SettingsViewModel(
        AppSettings settings,
        SettingsStore store,
        AutostartService autostart,
        UpdateService updates,
        HardwarePipeline pipeline,
        Action<AppSettings> applied)
    {
        _settings = settings;
        _store = store;
        _autostart = autostart;
        _updates = updates;
        _pipeline = pipeline;
        _applied = applied;

        RefreshIntervals =
        [
            new NamedOption { Id = "500", Name = "0.5 seconds" },
            new NamedOption { Id = "1000", Name = "1 second" },
            new NamedOption { Id = "2000", Name = "2 seconds" }
        ];
        TextColors =
        [
            new NamedOption { Id = "#FFFFFFFF", Name = "White" },
            new NamedOption { Id = "#FFF4F1EA", Name = "Warm white" },
            new NamedOption { Id = "#FFC8C4BB", Name = "Soft gray" }
        ];

        RefreshNics(pipeline.ListNics());
        RefreshGpus(pipeline.ListGpus());
        RefreshSensors(pipeline.ListCpuTempSensors());

        SelectedInterval = RefreshIntervals.FirstOrDefault(i => i.Id == settings.RefreshIntervalMs.ToString())
                           ?? RefreshIntervals[1];
        SelectedNic = Nics.FirstOrDefault(n => n.Id == (settings.NicId ?? "")) ?? Nics[0];
        SelectedGpu = Gpus.FirstOrDefault(g => g.Id == (settings.GpuId ?? "")) ?? Gpus[0];
        SelectedSensor = CpuSensors.FirstOrDefault(s => s.Id == (settings.CpuTempSensor ?? "")) ?? CpuSensors[0];
        SelectedTextColor = TextColors.FirstOrDefault(c => c.Id.Equals(settings.TextColor, StringComparison.OrdinalIgnoreCase))
                            ?? TextColors[0];
        ShowVram = settings.ShowVram;
        StartWithWindows = settings.StartWithWindows;
        AutoUpdate = settings.AutoUpdate;
        ShowPawnIoInstall = !new PawnIoGuard().IsInstalled();
    }

    public ObservableCollection<NamedOption> RefreshIntervals { get; }
    public ObservableCollection<NamedOption> Nics { get; } = [];
    public ObservableCollection<NamedOption> Gpus { get; } = [];
    public ObservableCollection<NamedOption> CpuSensors { get; } = [];
    public ObservableCollection<NamedOption> TextColors { get; }

    [ObservableProperty] private NamedOption? _selectedInterval;
    [ObservableProperty] private NamedOption? _selectedNic;
    [ObservableProperty] private NamedOption? _selectedGpu;
    [ObservableProperty] private NamedOption? _selectedSensor;
    [ObservableProperty] private NamedOption? _selectedTextColor;
    [ObservableProperty] private bool _showVram;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _autoUpdate;
    [ObservableProperty] private bool _showPawnIoInstall;
    [ObservableProperty] private string _pawnIoStatus = string.Empty;

    [RelayCommand]
    private void Save()
    {
        _settings.RefreshIntervalMs = int.TryParse(SelectedInterval?.Id, out var ms) ? ms : 1000;
        _settings.NicId = string.IsNullOrWhiteSpace(SelectedNic?.Id) ? null : SelectedNic!.Id;
        _settings.GpuId = string.IsNullOrWhiteSpace(SelectedGpu?.Id) ? null : SelectedGpu!.Id;
        _settings.CpuTempSensor = string.IsNullOrWhiteSpace(SelectedSensor?.Id) ? null : SelectedSensor!.Id;
        _settings.TextColor = SelectedTextColor?.Id ?? "#FFFFFFFF";
        _settings.ShowVram = ShowVram;
        _settings.StartWithWindows = StartWithWindows;
        var autoWas = _settings.AutoUpdate;
        _settings.AutoUpdate = AutoUpdate;
        _store.Save(_settings);
        _autostart.Apply(_settings.StartWithWindows);
        if (autoWas && !_settings.AutoUpdate)
        {
            _updates.CancelPendingApply();
        }

        _updates.RefreshPresentation();
        _applied(_settings);
    }

    [RelayCommand]
    private async Task InstallPawnIoAsync()
    {
        PawnIoStatus = "Downloading PawnIO…";
        try
        {
            var ok = await new PawnIoGuard().InstallAsync();
            ShowPawnIoInstall = !ok;
            PawnIoStatus = ok
                ? "PawnIO installed. CPU temperature should appear on the next sample."
                : "PawnIO installer closed. Temps stay -- until it is installed.";
        }
        catch
        {
            PawnIoStatus = "Couldn't download PawnIO. Visit pawnio.eu if you want CPU temps.";
        }
    }

    private void RefreshNics(IReadOnlyList<NamedOption> items)
    {
        Nics.Clear();
        foreach (var item in items)
        {
            Nics.Add(item);
        }
    }

    private void RefreshGpus(IReadOnlyList<NamedOption> items)
    {
        Gpus.Clear();
        foreach (var item in items)
        {
            Gpus.Add(item);
        }
    }

    private void RefreshSensors(IReadOnlyList<NamedOption> items)
    {
        CpuSensors.Clear();
        foreach (var item in items)
        {
            CpuSensors.Add(item);
        }
    }
}
