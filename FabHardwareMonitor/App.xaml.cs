using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FabHardwareMonitor.Interop;
using FabHardwareMonitor.Models;
using FabHardwareMonitor.Services;
using FabHardwareMonitor.ViewModels;
using FabHardwareMonitor.Views;
using H.NotifyIcon;
using Application = System.Windows.Application;
using Point = System.Windows.Point;

namespace FabHardwareMonitor;

public partial class App : Application
{
    private NamedIpcMutex? _mutex;
    private TaskbarIcon? _tray;
    private TaskbarWidget? _widget;
    private AboutWindow? _about;
    private SettingsWindow? _settingsWindow;
    private AppMenuWindow? _menu;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private AutostartService _autostart = null!;
    private UpdateService _updates = null!;
    private HardwarePipeline _pipeline = null!;
    private TaskbarViewModel _viewModel = null!;
    private PawnIoGuard _pawnIo = null!;
    private bool _menuQueued;
    private EventWaitHandle? _reattach;
    private CancellationTokenSource? _reattachCts;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CrashLog.Attach(this);
        _mutex = NamedIpcMutex.Create(AppConstants.MutexName, true, out var created);
        if (!created)
        {
            _mutex.Dispose();
            _mutex = null;
            Program.SignalRunningInstance();
            Shutdown();
            return;
        }

        StartReattachListener();

        ProcessCleanup.TerminateDetachers();
        ProcessCleanup.ScheduleFollowUp();
        LaunchLog.Write($"shell pid={Environment.ProcessId}");

        ShortcutFix.Rewrite();
        LaunchTask.EnsureRegistered();
        SensorIpc.EnsureOpen();

        _store = new SettingsStore();
        _settings = _store.Load();
        ThemeService.Start();

        _autostart = new AutostartService();
        _pawnIo = new PawnIoGuard();
        await MaybeInstallPawnIoFromSetupAsync();
        _pawnIo.TryEnsureDriverRunning();

        _updates = new UpdateService(() => _settings.AutoUpdate);
        _viewModel = new TaskbarViewModel();
        _viewModel.ApplySettings(_settings);
        _viewModel.ShowPawnIoWarning = !_pawnIo.CanReadCpuTemps();
        ThemeService.Changed += () => Dispatcher.Invoke(_viewModel.ApplyShellTheme);
        _pipeline = new HardwarePipeline(_settings);
        _pipeline.Updated += snapshot => Dispatcher.Invoke(() =>
        {
            _viewModel.Apply(snapshot);
            _viewModel.ShowPawnIoWarning = !_pawnIo.CanReadCpuTemps();
        });
        _pipeline.Start();
        _autostart.Apply(_settings.StartWithWindows);

        BuildTray();
        try
        {
            await InitializeWidgetAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("WidgetAttach", ex);
            await Task.Delay(1000);
            await InitializeWidgetAsync();
        }

        if (_widget is null)
        {
            return;
        }

        ProcessCleanup.TerminateDetachersAfterUi();
        _widget?.TaskbarContentHost.RefreshLayout();

        SensorHost.EnsureHelper();
        _ = _updates.CheckOnLaunchAsync();
    }

    private async Task MaybeInstallPawnIoFromSetupAsync()
    {
        if (_pawnIo.IsInstalled() || !_pawnIo.InstallerRequested())
        {
            _pawnIo.ClearInstallerRequest();
            return;
        }

        try
        {
            await _pawnIo.InstallAsync(silent: true);
        }
        catch
        {
            // Setup already finished; Settings can retry.
        }

        _pawnIo.ClearInstallerRequest();
    }

    public void ShowAppMenu()
    {
        // Must not open a popup/menu inside the taskbar's mouse-up. The leftover
        // button-up is treated as a click-outside and the menu vanishes at once.
        // Do not use ApplicationIdle: attach/layout can starve it and freeze the tray.
        if (_menuQueued)
        {
            return;
        }

        _menuQueued = true;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(80);
                OpenMenu();
            }
            catch (Exception ex)
            {
                CrashLog.Write("ShowAppMenu", ex);
            }
            finally
            {
                _menuQueued = false;
            }
        });
    }

    private void OpenMenu()
    {
        if (_menu is { IsVisible: true })
        {
            return;
        }

        var menu = new AppMenuWindow
        {
            AboutLabel = _updates.AboutMenuHeader
        };
        menu.SettingsChosen += () => Dispatcher.BeginInvoke(() => ShowSettings());
        menu.AboutChosen += () => Dispatcher.BeginInvoke(ShowAbout);
        menu.ExitChosen += () => Dispatcher.BeginInvoke(Shutdown);
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_menu, menu))
            {
                _menu = null;
            }
        };
        _menu = menu;
        menu.ShowAt(CursorInDip());
    }

    private Point CursorInDip()
    {
        Native.GetCursorPos(out var cursor);
        var device = new Point(cursor.X, cursor.Y);
        var visual = _widget as Visual ?? _menu as Visual;
        if (visual is not null && PresentationSource.FromVisual(visual) is { CompositionTarget: { } target })
        {
            return target.TransformFromDevice.Transform(device);
        }

        return device;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _widget?.Close();
        }
        catch
        {
            // Explorer may already have destroyed the HWND.
        }

        ThemeService.Stop();
        StopReattachListener();
        _pipeline?.Dispose();
        _tray?.Dispose();
        _mutex?.Dispose();
        _mutex = null;
        base.OnExit(e);
    }

    private void StartReattachListener()
    {
        _reattach = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.ReattachEventName);
        _reattachCts = new CancellationTokenSource();
        var cancel = _reattachCts.Token;
        var ev = _reattach;
        _ = Task.Run(() =>
        {
            while (!cancel.IsCancellationRequested)
            {
                if (!ev.WaitOne(500) || cancel.IsCancellationRequested)
                {
                    continue;
                }

                LaunchLog.Write("reattach requested");
                Dispatcher.BeginInvoke(async () =>
                {
                    if (_widget is { } widget)
                    {
                        await widget.RetryAttachAsync();
                    }
                });
            }
        }, cancel);
    }

    private void StopReattachListener()
    {
        try
        {
            _reattachCts?.Cancel();
            _reattach?.Set();
        }
        catch
        {
            // ignored
        }

        _reattachCts?.Dispose();
        _reattachCts = null;
        _reattach?.Dispose();
        _reattach = null;
    }

    private async Task InitializeWidgetAsync()
    {
        var widget = new TaskbarWidget { DataContext = _viewModel };
        _widget = widget;
        widget.TaskbarContentHost.TaskbarWindowRecreationRequired += OnRecreationRequired;
        if (!await widget.AttachAndShowAsync())
        {
            LaunchLog.Write("widget never hosted");
            widget.TaskbarContentHost.TaskbarWindowRecreationRequired -= OnRecreationRequired;
            widget.AbandonAttach();
            _widget = null;
            if (!Program.ShellRetry)
            {
                await RestartShellOnceAsync();
            }

            return;
        }

        _viewModel.ShowPawnIoWarning = !_pawnIo.CanReadCpuTemps();
    }

    private async Task RestartShellOnceAsync()
    {
        LaunchLog.Write("shell retry scheduled");
        StopReattachListener();
        _mutex?.Dispose();
        _mutex = null;
        await Task.Delay(4000);
        var exe = ShortcutFix.InstalledExe();
        var started = !string.IsNullOrWhiteSpace(exe)
                      && Native.StartProcessFromExplorer(
                          exe,
                          $"{AppConstants.ShellArgument} {AppConstants.ShellRetryArgument}");
        LaunchLog.Write($"shell retry start={started}");
        Shutdown();
    }

    internal TaskbarWidget? Widget => _widget;

    private async void OnRecreationRequired(object? sender, EventArgs e)
    {
        var old = _widget;
        if (old is not null)
        {
            old.TaskbarContentHost.TaskbarWindowRecreationRequired -= OnRecreationRequired;
        }

        await Task.Delay(1000);
        await InitializeWidgetAsync();
        old?.Close();
    }

    private void BuildTray()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = AppConstants.ProductName,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray.ico"))
        };
        _tray.ForceCreate();
        _tray.TrayMouseDoubleClick += (_, _) => ShowAbout();
        _tray.TrayRightMouseUp += (_, _) => ShowAppMenu();
    }

    private void ShowAbout()
    {
        try
        {
            if (_about is { IsVisible: true })
            {
                _about.Activate();
                return;
            }

            _about = new AboutWindow(new AboutViewModel(_updates));
            _about.Closed += (_, _) => _about = null;
            _about.Show();
            _about.Activate();
            _ = _updates.CheckFromAboutAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShowAbout", ex);
        }
    }

    public void ShowSettings(bool highlightPawnIo = false)
    {
        try
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                if (highlightPawnIo)
                {
                    _settingsWindow.HighlightPawnIo();
                }

                return;
            }

            var vm = new SettingsViewModel(_settings, _store, _autostart, _updates, _pipeline, ApplySettings);
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
            if (highlightPawnIo)
            {
                Dispatcher.BeginInvoke(
                    () => _settingsWindow?.HighlightPawnIo(),
                    DispatcherPriority.ApplicationIdle);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShowSettings", ex);
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _viewModel.ApplySettings(settings);
        _pipeline.ApplySettings(settings);
        _viewModel.ShowPawnIoWarning = !_pawnIo.CanReadCpuTemps();
        if (settings.AutoUpdate && _updates.Status == UpdateStatusKind.Available)
        {
            _ = _updates.InstallAsync();
        }
    }
}
