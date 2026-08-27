# AGENTS.md

## Cursor Cloud specific instructions

Fab Hardware Monitor is a **Windows 11–only WPF desktop app** (`net10.0-windows`,
`win-x64`, `UseWPF=true`) that embeds a widget into the Windows taskbar and reads
hardware sensors via `LibreHardwareMonitorLib` (and the PawnIO driver). See
`README.md` for the product overview and the standard build/release commands.

### What works in the Cloud (Linux) environment

The cloud agent VM is Linux, so the app can be **compiled and published** but
**cannot be run** here (WPF needs the Windows-only `Microsoft.WindowsDesktop.App`
runtime; the widget also needs the Windows taskbar/tray and Windows hardware
sensors). Running/GUI testing must happen on a real Windows 11 machine.

- .NET 10 SDK is installed at `~/.dotnet` and is on `PATH` for interactive shells.
- `EnableWindowsTargeting=true` is exported (in `~/.bashrc`). This MSBuild
  property is **required** to build a `net10.0-windows` project on Linux;
  without it you get `error NETSDK1100`. Non-interactive shells that don't source
  `~/.bashrc` must set it themselves (e.g. `EnableWindowsTargeting=true dotnet build ...`).

### Build / lint

- Build (solution): `dotnet build FabHardwareMonitor.slnx`
- Publish the Windows exe (as in `README.md`):
  `dotnet publish FabHardwareMonitor/FabHardwareMonitor.csproj -c Release -r win-x64 --self-contained true`
- There is no test project. The effective checks are a clean build (0 warnings)
  and `dotnet format FabHardwareMonitor/FabHardwareMonitor.csproj --verify-no-changes`
  (no `.editorconfig`, so this uses default whitespace/style rules).
- `dotnet format` does not accept `-p:` args; pass `EnableWindowsTargeting` via the
  environment variable instead (already exported for interactive shells).

The `scripts/*.ps1` packaging (Velopack MSI/Setup) is PowerShell + Windows-only and
runs on the `windows-latest` GitHub Actions runner (`.github/workflows/release.yml`).
