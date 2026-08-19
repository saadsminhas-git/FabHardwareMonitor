<#
Renders the Fab Hardware Monitor mark: a red hardware pulse on a plain
white squircle, with a green status LED floating above the start of the
line.

  app.ico / tray.ico / app-icon.png  the same mark, used everywhere
                                     except the Fabric Visuals lockup
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $OutputDirectory = Join-Path $root '..\FabHardwareMonitor\Assets'
}

Add-Type -AssemblyName System.Drawing

$BrandRed = [System.Drawing.Color]::FromArgb(255, 230, 28, 28)
$LedGreen = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)
$TileFill = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function New-RoundedPath {
    param([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Add-Pulse {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Thickness,
        [System.Drawing.Color]$Color
    )

    $pen = New-Object System.Drawing.Pen $Color, $Thickness
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @(
        (New-Object System.Drawing.PointF ($X + $Width * 0.08), ($Y + $Height * 0.52)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.30), ($Y + $Height * 0.52)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.40), ($Y + $Height * 0.18)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.52), ($Y + $Height * 0.82)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.62), ($Y + $Height * 0.48)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.68), ($Y + $Height * 0.52)),
        (New-Object System.Drawing.PointF ($X + $Width * 0.92), ($Y + $Height * 0.52))
    )
    $path.AddLines($pts)
    $Graphics.DrawPath($pen, $path)
    $path.Dispose()
    $pen.Dispose()
}

function New-Frame {
    param([int]$Size)

    $scale = 8
    $big = $Size * $scale
    $canvas = New-Object System.Drawing.Bitmap $big, $big, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = $big * 0.02
    $tile = $big - (2 * $inset)
    $path = New-RoundedPath -X $inset -Y $inset -Width $tile -Height $tile -Radius ($big * 0.22)
    $fill = New-Object System.Drawing.SolidBrush $TileFill
    $g.FillPath($fill, $path)
    $fill.Dispose()
    $path.Dispose()

    $content = $tile * 0.78
    $cx = $inset + (($tile - $content) / 2)
    $cy = $inset + (($tile - $content) / 2) + ($tile * 0.04)
    $thickness = [Math]::Max(7.0, $big * 0.08)
    Add-Pulse -Graphics $g -X $cx -Y $cy -Width $content -Height $content -Thickness $thickness -Color $BrandRed

    # Float the LED above the pulse start, with a clear gap (matches the reference).
    $ledR = [Math]::Max(5.0, $tile * 0.055)
    $pulseStartX = $cx + $content * 0.08
    $pulseStartY = $cy + $content * 0.52
    $gap = [Math]::Max(10.0, $tile * 0.09)
    $ledX = $pulseStartX
    $ledY = $pulseStartY - ($thickness / 2) - $gap - $ledR
    $led = New-Object System.Drawing.SolidBrush $LedGreen
    $g.FillEllipse($led, $ledX - $ledR, $ledY - $ledR, $ledR * 2, $ledR * 2)
    $led.Dispose()

    $g.Dispose()

    $frame = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $fg = [System.Drawing.Graphics]::FromImage($frame)
    $fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $fg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $fg.Clear([System.Drawing.Color]::Transparent)
    $fg.DrawImage($canvas, (New-Object System.Drawing.Rectangle 0, 0, $Size, $Size))
    $fg.Dispose()
    $canvas.Dispose()
    return $frame
}

function Get-DibBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream

    $writer.Write([uint32]40)
    $writer.Write([int32]$w)
    $writer.Write([int32]($h * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)
    $writer.Write([uint32]($w * $h * 4))
    0..3 | ForEach-Object { $writer.Write([uint32]0) }

    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $p = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte]$p.B)
            $writer.Write([byte]$p.G)
            $writer.Write([byte]$p.R)
            $writer.Write([byte]$p.A)
        }
    }

    $maskStride = [int][Math]::Floor(($w + 31) / 32) * 4
    $writer.Write((New-Object byte[] ($maskStride * $h)))
    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()
    return , $bytes
}

function Get-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return , $bytes
}

function Save-Icon {
    param([string]$Path)

    $payloads = @()
    foreach ($size in $Sizes) {
        $frame = New-Frame -Size $size
        $bytes = if ($size -ge 128) { Get-PngBytes -Bitmap $frame } else { Get-DibBytes -Bitmap $frame }
        $frame.Dispose()
        $payloads += , @{ Size = $size; Bytes = [byte[]]$bytes }
    }

    $file = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter $file
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$payloads.Count)

    $offset = 6 + (16 * $payloads.Count)
    foreach ($payload in $payloads) {
        $dim = if ($payload.Size -ge 256) { 0 } else { $payload.Size }
        $writer.Write([byte]$dim)
        $writer.Write([byte]$dim)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$payload.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $payload.Bytes.Length
    }

    foreach ($payload in $payloads) {
        $writer.Write($payload.Bytes, 0, $payload.Bytes.Length)
    }

    $writer.Flush()
    $writer.Dispose()
    $file.Dispose()
    Write-Host "wrote $Path"
}

$OutputDirectory = (Resolve-Path $OutputDirectory).Path
Save-Icon -Path (Join-Path $OutputDirectory 'app.ico')
Save-Icon -Path (Join-Path $OutputDirectory 'tray.ico')

$png = New-Frame -Size 256
$png.Save((Join-Path $OutputDirectory 'app-icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()
Write-Host "wrote $(Join-Path $OutputDirectory 'app-icon.png')"
