# Fab Hardware Monitor

Windows 11 taskbar widget for network, CPU, RAM, GPU, VRAM, and temperatures. Built as a .NET Windows app (Deskband11Lib requires .NET 10) with a one-click Velopack installer and GitHub Releases updates.

## Layout

```
↑  0.0 K/s     CPU  12%     GPU   0%     CPU  45°C
↓  0.0 K/s     MEM  60%     VRAM  8%     GPU  50°C
```

Right-click the widget or tray icon: **Settings**, **About**, **Exit**.

## Requirements

- Windows 11
- One UAC prompt on first launch (needed for CPU temperature via PawnIO and logon autostart)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build

CPU temperature uses [LibreHardwareMonitorLib](https://www.nuget.org/packages/LibreHardwareMonitorLib) and the official [PawnIO](https://pawnio.eu) driver. WinRing0 is not shipped.

## Build

```powershell
dotnet publish FabHardwareMonitor/FabHardwareMonitor.csproj -c Release -r win-x64 --self-contained true
```

Run `FabHardwareMonitor.exe` elevated to embed the widget left of the notification area.

## Release

1. `pwsh scripts/release.ps1 -Bump patch` (or `minor` / `major`)
2. GitHub Actions packs Velopack assets and publishes a GitHub Release
3. Installed copies check Releases on launch. Auto-install is on by default; turn it off in Settings and install from About.

## Settings

`%AppData%\FabHardwareMonitor\settings.json`
