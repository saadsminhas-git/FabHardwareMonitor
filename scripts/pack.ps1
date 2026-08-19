param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
    $Version = $props.SelectSingleNode("//Version").InnerText
}

function New-MsiBitmaps {
    param(
        [string]$SourcePng,
        [string]$IconPng,
        [string]$DialogBmp,
        [string]$BannerBmp
    )

    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Bitmap]::FromFile($SourcePng)
    $icon = [System.Drawing.Bitmap]::FromFile($IconPng)

    function Draw-CoverInto(
        [System.Drawing.Graphics]$g,
        [System.Drawing.Image]$image,
        [int]$dx, [int]$dy, [int]$dw, [int]$dh
    ) {
        $scale = [Math]::Max($dw / [double]$image.Width, $dh / [double]$image.Height)
        $srcW = [Math]::Max(1, [int][Math]::Round($dw / $scale))
        $srcH = [Math]::Max(1, [int][Math]::Round($dh / $scale))
        if ($srcW -gt $image.Width) { $srcW = $image.Width }
        if ($srcH -gt $image.Height) { $srcH = $image.Height }
        $srcX = [Math]::Max(0, [int](($image.Width - $srcW) / 2))
        $srcY = [Math]::Max(0, [int](($image.Height - $srcH) / 2))
        if ($srcX + $srcW -gt $image.Width) { $srcW = $image.Width - $srcX }
        if ($srcY + $srcH -gt $image.Height) { $srcH = $image.Height - $srcY }
        $dest = New-Object System.Drawing.Rectangle $dx, $dy, $dw, $dh
        $source = New-Object System.Drawing.Rectangle $srcX, $srcY, $srcW, $srcH
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.DrawImage($image, $dest, $source, [System.Drawing.GraphicsUnit]::Pixel)
    }

    # WixUI dialog bitmap is the FULL welcome/exit background. Text starts at
    # ~180px, so only the left 164px sidebar may hold artwork.
    $sidebar = 164
    $dialog = New-Object System.Drawing.Bitmap 493, 312, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($dialog)
    $g.Clear([System.Drawing.Color]::White)
    Draw-CoverInto $g $src 0 0 $sidebar 312
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(196, 191, 184))
    $g.DrawLine($pen, $sidebar, 0, $sidebar, 311)
    $pen.Dispose()
    $g.Dispose()
    $dialog.Save($DialogBmp, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $dialog.Dispose()

    # Banner titles sit on the left. Keep that white; mark only the far right.
    $banner = New-Object System.Drawing.Bitmap 493, 58, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($banner)
    $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($icon, 445, 9, 40, 40)
    $g.Dispose()
    $banner.Save($BannerBmp, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $banner.Dispose()
    $icon.Dispose()
    $src.Dispose()
}

dotnet publish FabHardwareMonitor/FabHardwareMonitor.csproj -c Release -r win-x64 --self-contained true -o publish /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$releases = Join-Path $root "Releases"
if (Test-Path $releases) {
    Get-ChildItem $releases | Remove-Item -Force -Recurse
}

$vendor = Join-Path $root "vendor"
New-Item -ItemType Directory -Force -Path $vendor | Out-Null
$pawnIo = Join-Path $vendor "PawnIO_setup.exe"
if (-not (Test-Path $pawnIo)) {
    Write-Host "Downloading official PawnIO_setup.exe"
    Invoke-WebRequest -Uri "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe" -OutFile $pawnIo
}
Copy-Item -Force $pawnIo (Join-Path $root "publish\PawnIO_setup.exe")

$packAssets = Join-Path $root "pack-assets"
New-Item -ItemType Directory -Force -Path $packAssets | Out-Null
$dialogBmp = Join-Path $packAssets "msi-dialog.bmp"
$bannerBmp = Join-Path $packAssets "msi-banner.bmp"
New-MsiBitmaps `
    -SourcePng (Join-Path $root "FabHardwareMonitor\Assets\installer-art.png") `
    -IconPng (Join-Path $root "FabHardwareMonitor\Assets\app-icon.png") `
    -DialogBmp $dialogBmp `
    -BannerBmp $bannerBmp

dotnet tool update -g vpk --version 1.2.0
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
$icon = Join-Path $root "FabHardwareMonitor\Assets\app.ico"
$license = Join-Path $root "legal\PRIVACY-AND-TERMS.md"
$welcome = Join-Path $root "legal\INSTALL-WELCOME.txt"
$conclusion = Join-Path $root "legal\INSTALL-CONCLUSION.txt"

vpk pack --packId FabHardwareMonitor --packVersion $Version --packDir publish --mainExe FabHardwareMonitor.exe --packTitle "Fab Hardware Monitor" --packAuthors "Fabric Visuals Ltd" --icon $icon --msi --instLicense $license --instWelcome $welcome --instConclusion $conclusion --msiLogo $dialogBmp --msiBanner $bannerBmp --outputDir Releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

$msi = Join-Path $root "Releases\FabHardwareMonitor-win.msi"
if (-not (Test-Path $msi)) { throw "MSI was not produced" }
cscript //nologo (Join-Path $root "scripts\patch-msi-pawnio.vbs") $msi $pawnIo $dialogBmp $bannerBmp (Join-Path $root "FabHardwareMonitor\Assets\app.ico")
if ($LASTEXITCODE -ne 0) { throw "MSI PawnIO patch failed" }

Write-Host "The MSI shows the privacy policy and a PawnIO option before install. Setup.exe is one-click and has no EULA page."
Write-Host "Installer is in Releases\ (FabHardwareMonitor-win-Setup.exe and FabHardwareMonitor-win.msi)."
