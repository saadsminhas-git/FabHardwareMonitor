using CommunityToolkit.Mvvm.ComponentModel;
using Velopack;
using Velopack.Sources;

namespace FabHardwareMonitor.Services;

public enum UpdateStatusKind
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Restarting,
    Failed,
    NotInstalled
}

public sealed partial class UpdateService : ObservableObject
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
    private readonly Func<bool> _autoUpdate;
    private readonly GithubSource _source = new(AppConstants.RepoUrl, accessToken: null, prerelease: false);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private UpdateManager? _manager;
    private UpdateInfo? _pending;
    private CancellationTokenSource? _applyCts;

    [ObservableProperty] private UpdateStatusKind _status = UpdateStatusKind.Idle;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string? _availableVersion;
    [ObservableProperty] private bool _showInstallButton;
    [ObservableProperty] private bool _showCheckButton;
    [ObservableProperty] private bool _showRetryButton;
    [ObservableProperty] private string _aboutMenuHeader = "About";

    public UpdateService(Func<bool> autoUpdate)
    {
        _autoUpdate = autoUpdate;
    }

    public async Task CheckOnLaunchAsync()
    {
        await CheckAsync(applyIfFound: _autoUpdate());
    }

    public async Task CheckFromAboutAsync()
    {
        if (Status is UpdateStatusKind.Downloading or UpdateStatusKind.Restarting)
        {
            ApplyPresentation();
            return;
        }

        await CheckAsync(applyIfFound: _autoUpdate());
    }

    public async Task InstallAsync()
    {
        if (Status is UpdateStatusKind.Downloading or UpdateStatusKind.Restarting)
        {
            return;
        }

        await CheckAsync(applyIfFound: true);
    }

    public void CancelPendingApply()
    {
        _applyCts?.Cancel();
    }

    public void RefreshPresentation()
    {
        ApplyPresentation();
    }

    private async Task CheckAsync(bool applyIfFound)
    {
        if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (!Program.VelopackInitialized)
            {
                Status = UpdateStatusKind.NotInstalled;
                ApplyPresentation();
                return;
            }

            Status = UpdateStatusKind.Checking;
            ApplyPresentation();

            _manager ??= new UpdateManager(_source);
            if (!_manager.IsInstalled)
            {
                Status = UpdateStatusKind.NotInstalled;
                _pending = null;
                ApplyPresentation();
                return;
            }

            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                Status = UpdateStatusKind.UpToDate;
                _pending = null;
                ApplyPresentation();
                return;
            }

            _pending = update;
            AvailableVersion = update.TargetFullRelease.Version.ToString();
            Status = UpdateStatusKind.Available;
            ApplyPresentation();

            if (applyIfFound)
            {
                await DownloadAndApplyAsync(update);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("Updates", ex);
            Status = UpdateStatusKind.Failed;
            ApplyPresentation();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DownloadAndApplyAsync(UpdateInfo update)
    {
        _applyCts?.Cancel();
        _applyCts = new CancellationTokenSource();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_applyCts.Token);
        timeout.CancelAfter(DownloadTimeout);
        var token = timeout.Token;

        try
        {
            Status = UpdateStatusKind.Downloading;
            ApplyPresentation();
            _manager ??= new UpdateManager(_source);
            await _manager.DownloadUpdatesAsync(update, percent =>
            {
                var version = AvailableVersion;
                Message = string.IsNullOrEmpty(version)
                    ? $"Downloading update… {percent}%"
                    : $"Downloading version {version}… {percent}%";
            }, token);
            token.ThrowIfCancellationRequested();
            Status = UpdateStatusKind.Restarting;
            ApplyPresentation();
            _manager.ApplyUpdatesAndRestart(update);
        }
        catch (OperationCanceledException) when (_applyCts?.IsCancellationRequested == true)
        {
            Status = UpdateStatusKind.Available;
            ApplyPresentation();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Updates", ex);
            Status = UpdateStatusKind.Failed;
            ApplyPresentation();
        }
    }

    private void ApplyPresentation()
    {
        var auto = _autoUpdate();
        ShowInstallButton = false;
        ShowCheckButton = false;
        ShowRetryButton = false;
        AboutMenuHeader = "About";

        switch (Status)
        {
            case UpdateStatusKind.Checking:
                Message = "Checking for updates…";
                break;
            case UpdateStatusKind.UpToDate when auto:
                Message = "You're on the latest version. Updates install automatically.";
                break;
            case UpdateStatusKind.UpToDate:
                Message = "You're up to date.";
                ShowCheckButton = true;
                break;
            case UpdateStatusKind.Available:
                Message = string.IsNullOrEmpty(AvailableVersion)
                    ? "An update is available."
                    : $"Version {AvailableVersion} is available.";
                ShowInstallButton = true;
                AboutMenuHeader = "About (update available)";
                break;
            case UpdateStatusKind.Downloading:
                Message = string.IsNullOrEmpty(AvailableVersion)
                    ? "Downloading update…"
                    : $"Downloading version {AvailableVersion}…";
                break;
            case UpdateStatusKind.Restarting:
                Message = "Restarting to finish the update…";
                break;
            case UpdateStatusKind.Failed:
                Message = "Couldn't download the update from GitHub Releases. Try again, or install the latest MSI from the repository.";
                ShowRetryButton = true;
                break;
            case UpdateStatusKind.NotInstalled:
                Message = "Install the app to enable updates.";
                break;
            default:
                Message = string.Empty;
                break;
        }
    }
}
