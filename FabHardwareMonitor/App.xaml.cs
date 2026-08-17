using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FabHardwareMonitor.Models;
using FabHardwareMonitor.Services;
using FabHardwareMonitor.ViewModels;
using FabHardwareMonitor.Views;
using H.NotifyIcon;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using DrawingIcon = System.Drawing.Icon;

namespace FabHardwareMonitor;

public partial class App : Application
{
    private Mutex? _mutex;
    private TaskbarIcon? _tray;
    private TaskbarWidget? _widget;
    private AboutWindow? _about;
    private SettingsWindow? _settingsWindow;
    private ContextMenu? _menu;
    private MenuItem? _aboutItem;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private AutostartService _autostart = null!;
    private UpdateService _updates = null!;
    private HardwarePipeline _pipeline = null!;
    private TaskbarViewModel _viewModel = null!;
    private PawnIoGuard _pawnIo = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, AppConstants.MutexName, out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        _store = new SettingsStore();
        _settings = _store.Load();
        _autostart = new AutostartService();
        _pawnIo = new PawnIoGuard();
        _updates = new UpdateService(() => _settings.AutoUpdate);
        _viewModel = new TaskbarViewModel();
        _viewModel.ApplySettings(_settings);
        _pipeline = new HardwarePipeline(_settings);
        _pipeline.Updated += snapshot => Dispatcher.Invoke(() => _viewModel.Apply(snapshot));
        _pipeline.Start();
        _autostart.Apply(_settings.StartWithWindows);

        BuildMenu();
        BuildTray();
        await InitializeWidgetAsync();
        _ = _updates.CheckOnLaunchAsync();
        await MaybePromptPawnIoAsync();
    }

    public void ShowAppMenu()
    {
        if (_menu is null)
        {
            return;
        }

        _menu.Placement = PlacementMode.MousePoint;
        _menu.PlacementTarget = _widget;
        _menu.IsOpen = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeline.Dispose();
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private async Task InitializeWidgetAsync()
    {
        var widget = new TaskbarWidget { DataContext = _viewModel };
        _widget = widget;
        widget.TaskbarContentHost.TaskbarWindowRecreationRequired += OnRecreationRequired;
        await widget.PrepareTaskbarContentAsync();
        widget.Show();
    }

    private async void OnRecreationRequired(object? sender, EventArgs e)
    {
        var old = _widget;
        if (old is not null)
        {
            old.TaskbarContentHost.TaskbarWindowRecreationRequired -= OnRecreationRequired;
            old.TaskbarContentHost.Dispose();
        }

        await Task.Delay(1000);
        await InitializeWidgetAsync();
        if (old is not null)
        {
            try
            {
                old.Close();
            }
            catch
            {
                // Explorer may already have destroyed the HWND.
            }
        }
    }

    private void BuildMenu()
    {
        _aboutItem = new MenuItem { Header = _updates.AboutMenuHeader };
        _aboutItem.Click += (_, _) => ShowAbout();
        _updates.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateService.AboutMenuHeader) && _aboutItem is not null)
            {
                Dispatcher.Invoke(() => _aboutItem.Header = _updates.AboutMenuHeader);
            }
        };

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => ShowSettings();
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Shutdown();

        _menu = new ContextMenu();
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(_aboutItem);
        _menu.Items.Add(exitItem);
    }

    private void BuildTray()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = AppConstants.ProductName,
            ContextMenu = _menu,
            Icon = CreateTrayIcon()
        };
        _tray.ForceCreate();
        _tray.TrayMouseDoubleClick += (_, _) => ShowAbout();
    }

    private void ShowAbout()
    {
        if (_about is { IsVisible: true })
        {
            _about.Activate();
            return;
        }

        _about = new AboutWindow(new AboutViewModel(_updates));
        _about.Closed += (_, _) => _about = null;
        _about.Show();
        _ = _updates.CheckFromAboutAsync();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = new SettingsViewModel(_settings, _store, _autostart, _updates, _pipeline, ApplySettings);
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _viewModel.ApplySettings(settings);
        _pipeline.ApplySettings(settings);
        if (settings.AutoUpdate && _updates.Status == UpdateStatusKind.Available)
        {
            _ = _updates.InstallAsync();
        }
    }

    private async Task MaybePromptPawnIoAsync()
    {
        if (_pawnIo.IsInstalled() || _settings.PawnIoSkipped)
        {
            return;
        }

        var result = MessageBox.Show(
            "CPU temperature needs PawnIO, the official hardware driver. Install it now? Other metrics keep working if you skip.",
            AppConstants.ProductName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            _settings.PawnIoSkipped = true;
            _store.Save(_settings);
            return;
        }

        try
        {
            await _pawnIo.InstallAsync();
        }
        catch
        {
            MessageBox.Show(
                "Couldn't download PawnIO. CPU temperature will stay -- until it is installed.",
                AppConstants.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static DrawingIcon CreateTrayIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var fill = new SolidBrush(Color.FromArgb(255, 196, 165, 116));
        graphics.FillEllipse(fill, 2, 2, 28, 28);
        using var pen = new Pen(Color.FromArgb(255, 18, 20, 23), 3);
        graphics.DrawArc(pen, 9, 8, 14, 14, 200, 220);
        graphics.DrawLine(pen, 16, 15, 16, 22);
        var handle = bitmap.GetHicon();
        return DrawingIcon.FromHandle(handle);
    }
}
