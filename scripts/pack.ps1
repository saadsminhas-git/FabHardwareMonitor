param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet publish FabHardwareMonitor/FabHardwareMonitor.csproj -c Release -r win-x64 --self-contained true -o publish /p:Version=$Version
dotnet tool update -g vpk --version 1.2.0
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
vpk pack --packId FabHardwareMonitor --packVersion $Version --packDir publish --mainExe FabHardwareMonitor.exe --packTitle "Fab Hardware Monitor" --packAuthors "Saad Minhas" --outputDir Releases
Write-Host "Installer is in Releases\FabHardwareMonitor-win-Setup.exe (name may vary slightly by vpk version)."
