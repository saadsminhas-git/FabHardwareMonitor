using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FabHardwareMonitor.Services;

namespace FabHardwareMonitor.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    public AboutViewModel(UpdateService updates)
    {
        Updates = updates;
        ProductName = AppConstants.ProductName;
        Version = AppVersion.Display;
        Author = AppConstants.Author;
        WebsiteLabel = AppConstants.WebsiteLabel;
        WebsiteUrl = AppConstants.WebsiteUrl;
        RepoUrl = AppConstants.RepoUrl;
    }

    public UpdateService Updates { get; }
    public string ProductName { get; }
    public string Version { get; }
    public string Author { get; }
    public string WebsiteLabel { get; }
    public string WebsiteUrl { get; }
    public string RepoUrl { get; }

    [RelayCommand]
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private Task CheckAsync() => Updates.CheckFromAboutAsync();

    [RelayCommand]
    private Task InstallAsync() => Updates.InstallAsync();
}
