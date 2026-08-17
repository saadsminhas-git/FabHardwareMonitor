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
    private readonly Func<bool> _autoUpdate;
    private readonly GithubSource _source = new(AppConstants.RepoUrl, accessToken: null, prerelease: false);
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
        await CheckAsync(installIfAuto: true);
    }

    public async Task CheckFromAboutAsync()
    {
        await CheckAsync(installIfAuto: false);
    }

    public async Task InstallAsync()
    {
        if (_pending is null)
        {
            await CheckAsync(installIfAuto: false);
        }

        if (_pending is null)
        {
            return;
        }

        await DownloadAndApplyAsync(_pending);
    }

    public void CancelPendingApply()
    {
        _applyCts?.Cancel();
    }

    public void RefreshPresentation()
    {
        ApplyPresentation();
    }

    private async Task CheckAsync(bool installIfAuto)
    {
        Status = UpdateStatusKind.Checking;
        ApplyPresentation();

        try
        {
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

            if (installIfAuto && _autoUpdate())
            {
                await DownloadAndApplyAsync(update);
            }
        }
        catch
        {
            Status = UpdateStatusKind.Failed;
            ApplyPresentation();
        }
    }

    private async Task DownloadAndApplyAsync(UpdateInfo update)
    {
        _applyCts?.Cancel();
        _applyCts = new CancellationTokenSource();
        var token = _applyCts.Token;

        try
        {
            Status = UpdateStatusKind.Downloading;
            ApplyPresentation();
            _manager ??= new UpdateManager(_source);
            await _manager.DownloadUpdatesAsync(update, cancelToken: token);
            token.ThrowIfCancellationRequested();
            Status = UpdateStatusKind.Restarting;
            ApplyPresentation();
            _manager.ApplyUpdatesAndRestart(update);
        }
        catch (OperationCanceledException)
        {
            Status = UpdateStatusKind.Available;
            ApplyPresentation();
        }
        catch
        {
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
            case UpdateStatusKind.Available when auto:
                Message = string.IsNullOrEmpty(AvailableVersion)
                    ? "An update is available."
                    : $"Downloading version {AvailableVersion}…";
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
                Message = "Couldn't check for updates.";
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
