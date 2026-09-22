<#
make-icon.ps1 - regenerate the Overmind application icons from the zergling artwork.

Usage: powershell -ExecutionPolicy Bypass -File make-icon.ps1 [-Source <png>] [-IcoOutput <ico>] [-TrayOutput <png>]

Run from anywhere; paths default to the repo layout relative to this script.
#>

param(
    [string]$Source,
    [string]$IcoOutput,
    [string]$TrayOutput,
    [int[]]$Sizes = @(16, 32, 48, 256),
    [int]$TraySize = 32
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Source)     { $Source     = Join-Path $repoRoot 'web\src\shared\assets\zerlings\menacing.png' }
if (-not $IcoOutput)  { $IcoOutput  = Join-Path $repoRoot 'src\Overmind.Host\Assets\overmind.ico' }
if (-not $TrayOutput) { $TrayOutput = Join-Path $repoRoot 'src\Overmind.Host\Assets\tray-icon.png' }

if (-not (Test-Path $Source)) { throw "icon source not found: $Source" }

function Get-SquarePng {
    param([System.Drawing.Bitmap]$Bitmap, [int]$Size)

    $canvas = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $square = [Math]::Max($Bitmap.Width, $Bitmap.Height)
        $scale = $Size / $square
        $width = [int][Math]::Round($Bitmap.Width * $scale)
        $height = [int][Math]::Round($Bitmap.Height * $scale)
        $x = [int][Math]::Round(($Size - $width) / 2)
        $y = [int][Math]::Round(($Size - $height) / 2)
        $graphics.DrawImage($Bitmap, $x, $y, $width, $height)
    }
    finally {
        $graphics.Dispose()
    }

    $stream = New-Object System.IO.MemoryStream
    $canvas.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
    return $stream.ToArray()
}

$sourceBitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source))
try {
    $frames = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($size in $Sizes) {
        $frames.Add((Get-SquarePng -Bitmap $sourceBitmap -Size $size))
    }

    $output = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($output)
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$Sizes.Count)

    $offset = 6 + (16 * $Sizes.Count)
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $dimension = if ($Sizes[$i] -ge 256) { 0 } else { $Sizes[$i] }
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frames[$i].Length)
        $writer.Write([UInt32]$offset)
        $offset += $frames[$i].Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($IcoOutput, $output.ToArray())
    $writer.Dispose()

    [System.IO.File]::WriteAllBytes($TrayOutput, (Get-SquarePng -Bitmap $sourceBitmap -Size $TraySize))
}
finally {
    $sourceBitmap.Dispose()
}

Write-Host "wrote $IcoOutput ($($Sizes -join '/') px)"
Write-Host "wrote $TrayOutput ($TraySize px)"
