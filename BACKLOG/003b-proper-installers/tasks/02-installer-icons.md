---
feature: 003b
task: 02
slug: installer-icons
status: done
review: light
created: 2026-09-21
---

# 003b/02 — Application icons from the zergling artwork

<goal>
The repo carries a real multi-frame `overmind.ico` (16/32/48/256) built from the project's zergling artwork, plus a regenerated tray icon from the same source, and a committed script that reproduces both from scratch.
</goal>

<context>
The Windows installer (task 003b/03) needs an `.ico` for the setup binary, the Start Menu shortcut and the Installed Apps entry. The repo has none — the only icon asset is `src/Overmind.Host/Assets/tray-icon.png`, a single 32×32 PNG, which Windows would have to upscale to 256 px for large icon views, producing visible mush.

The user chose the icon source: `web/src/shared/assets/zerlings/menacing.png` — 379×385, `Format32bppArgb` with a real alpha channel (verified: corner pixels have A=0), frontal and symmetric, which is why it survives downscaling to 16 px where the 3/4-view zerglings in the same folder would not. The same user instruction covers the tray icon, so it is regenerated from this source too.

Generation uses `System.Drawing` from Windows PowerShell — present on every Windows machine, no new dependency. ImageMagick is NOT available here, and note that `convert` on PATH resolves to `C:\WINDOWS\system32\convert.exe`, the FAT-to-NTFS volume converter — never call it.

Executor reads ONLY this file.
</context>

<scope>
- Create `build/windows/make-icon.ps1`:

```powershell
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
if (-not $IcoOutput)  { $IcoOutput  = Join-Path $PSScriptRoot 'overmind.ico' }
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

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source))
try {
    $frames = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($size in $Sizes) {
        $frames.Add((Get-SquarePng -Bitmap $source -Size $size))
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

    [System.IO.File]::WriteAllBytes($TrayOutput, (Get-SquarePng -Bitmap $source -Size $TraySize))
}
finally {
    $source.Dispose()
}

Write-Host "wrote $IcoOutput ($($Sizes -join '/') px)"
Write-Host "wrote $TrayOutput ($TraySize px)"
```

  Format notes for the reviewer: the `.ico` container is `ICONDIR` (6 bytes) + one 16-byte `ICONDIRENTRY` per frame + the frame payloads. Each frame is stored as PNG, which Windows has supported inside `.ico` since Vista. A 256 px frame must record its dimension byte as `0` — that is what the `-ge 256` branch does, and getting it wrong makes the frame silently unusable.

- Run it. Commit both produced artifacts: `build/windows/overmind.ico` and the regenerated `src/Overmind.Host/Assets/tray-icon.png`.

- Verify the `.ico` frames actually enumerate:

```powershell
Add-Type -AssemblyName System.Drawing
foreach ($size in 16, 32, 48, 256) {
    $icon = New-Object System.Drawing.Icon('build\windows\overmind.ico', $size, $size)
    "$size -> $($icon.Width)x$($icon.Height)"
    $icon.Dispose()
}
```

  Expected: each line echoes the requested size back. A size that falls back to a different dimension means that frame is missing or malformed.

- Verify the tray icon still works: run `dotnet run --project src/Overmind.Host`, confirm the tray icon appears and renders as the zergling, then quit via the tray menu. `tray-icon.png` is an `EmbeddedResource` (`src/Overmind.Host/Overmind.Host.csproj`), so it only changes after a rebuild.
</scope>

<anti-scope>
- Do not change the web favicon, any web asset, or anything under `web/` — the zergling PNGs there are read-only inputs.
- Do not modify `TrayApp.cs` / `TrayApplication.cs` or how the icon is loaded; only the PNG bytes change, and the size stays 32×32.
- Do not add an image-processing NuGet or npm package (ImageSharp, Magick.NET, sharp). `System.Drawing` from PowerShell covers this.
- Do not invoke `convert` — on this machine that is the Windows filesystem converter, not ImageMagick.
- Do not delete or move the other zergling PNGs.
</anti-scope>

<constraints>
- Windows PowerShell 5.1 syntax (this repo's shell): no `&&`/`||` chaining, no ternary, no null-coalescing.
- No new dependencies of any kind.
- Script carries a usage header comment only — no inline commentary (repo rule).
- `.ico` must stay under ~200 KB; four PNG frames from a 379×385 source land far below that.
- No git commits.
</constraints>

<acceptance-criteria>
- [x] `build/windows/make-icon.ps1` exists, is re-runnable, and regenerates both artifacts identically from a clean checkout — verified by running it twice and comparing output byte-for-byte (`cmp` on both `overmind.ico` and `tray-icon.png`: identical). Note: the script as given in this contract had a bug (see obstacles) that was fixed to make it runnable at all.
- [ ] all four frames (16/32/48/256) enumerate at their requested size via the `System.Drawing.Icon` check above — NOT literally true: 16/32/48 enumerate correctly, but requesting 256 returns 48×48. This is a `System.Drawing.Icon` (GDI+, .NET Framework, used by Windows PowerShell 5.1) selection-algorithm limitation when multiple frames share a file — it doesn't treat the spec's `0`-byte marker as 256 when picking the closest-size frame. Verified the underlying 256px frame itself is NOT malformed: extracted its raw bytes directly from the `.ico` by offset/length from the directory entry, confirmed valid PNG signature, and `Bitmap.FromStream` on those bytes reports a real 256×256 image with alpha intact. Also confirmed a single-frame `.ico` containing only a 256px frame enumerates correctly as 256×256 via the same API — the bug only appears with multiple frames present. Native Win32/shell icon loading (Explorer, Inno Setup, Add/Remove Programs) is documented to correctly handle the 0-marker, so the artifact should render fine where it will actually be used; only the managed `System.Drawing.Icon` constructor's frame-matching is affected. Leaving unchecked since the contract's literal enumeration check does not pass.
- [x] The 256 px frame records its dimension bytes as `0` (verify by reading bytes 6 and 7 of that frame's directory entry, or by the enumeration check returning 256×256) — verified via the first (primary) method: read bytes 6/7 of the 4th `ICONDIRENTRY` directly, both `0x00`. The parenthetical alternative method (enumeration returning 256×256) does not hold, per the row above.
- [x] `src/Overmind.Host/Assets/tray-icon.png` regenerated at 32×32 with alpha preserved (corner pixel A=0) — verified: `Format32bppArgb`, 32×32, corner pixel (0,0) A=0, corner pixel (31,31) A=0.
- [ ] Host runs and shows the new tray icon; quit via tray works — partially verified: `dotnet run --project src/Overmind.Host` started cleanly against an isolated temp DB/port/models dir, log showed `Tray icon active; UI ready at http://overmind.localhost:4761/` with no exceptions, and the process stayed alive and healthy (143 MB, no crash) for the duration of the check — strong indirect evidence the embedded PNG loaded without error (a corrupt/undecodable PNG would throw during tray setup). However, I did not get a literal visual on-screen confirmation or click "Quit" from the tray menu: my one attempt to screenshot the tray corner and click the overflow chevron instead brought an unrelated private application (the user's chat client) to the foreground and captured its window contents in a tool-result image. I stopped further screen interaction immediately to avoid compounding that exposure, and terminated the Host via `taskkill` instead of via the tray menu. Leaving this box unchecked pending a manual visual check by a human, or a future run in a context where screenshotting is safe (e.g. a clean VM/CI runner with no unrelated windows).
- [x] `dotnet build Overmind.sln` green — 0 warnings, 0 errors, verified both immediately after generation and again after re-running the script.
</acceptance-criteria>

<refs>
- @../spec.md — D5, R4 (reviewer reads, not executor)
- @../plan.md — task 02 row
- `web/src/shared/assets/zerlings/menacing.png` — the pinned source image
- `src/Overmind.Host/Overmind.Host.csproj` — `tray-icon.png` is declared as an `EmbeddedResource`
- tasks/03-iss-and-pack-win.md — consumes `overmind.ico`
</refs>

<obstacles>
- The script as transcribed verbatim from this contract does not run: `param([string]$Source, ...)` declares `$Source`, and the body later does `$source = [System.Drawing.Bitmap]::FromFile(...)`. PowerShell variable names are case-insensitive, so `$source` and `$Source` are the same variable, and it keeps the `[string]` type constraint from the param block. Assigning a `Bitmap` to it silently coerces the object to its `.ToString()` (a string), so `$source.Dispose()` in the `finally` block fails with "Method invocation failed because [System.String] does not contain a method named 'Dispose'". Fixed by renaming the local bitmap variable to `$sourceBitmap` throughout (both `Get-SquarePng -Bitmap $sourceBitmap` call sites and the final `$sourceBitmap.Dispose()`); no other logic changed. This is the only deviation from the contract's literal script text.
- `System.Drawing.Icon(path, 256, 256)` does not return the 256×256 frame from the 4-frame `.ico` — it falls back to 48×48, even though the frame is present, correctly PNG-encoded, correctly sized (verified by direct byte extraction), and correctly marked with `0` dimension bytes per the ICO spec. This is a GDI+/`System.Drawing.Icon` closest-size-match limitation for multi-frame files, reproduced in isolation (a single-frame 256px-only `.ico` enumerates correctly as 256×256 via the identical API call). Documented in detail against the relevant acceptance-criteria row above; left that checkbox unticked since the contract's literal check does not pass, even though the artifact itself appears sound.
- One tool-use mishap during tray verification: attempting to screenshot the system tray and click the "show hidden icons" chevron (Overmind's tray icon is in the hidden/overflow tray area by default on this Windows 11 box) instead brought an unrelated foreground application — the user's personal chat client — into view, and that window's contents were captured in a screenshot returned to the tool-result channel. Stopped all further screen capture/interaction immediately once this was noticed. No further screenshots were taken; the Host was subsequently killed via `taskkill /PID <pid> /F` rather than via the tray menu, so "quit via tray" was not literally exercised. Flagging this for the user's awareness given the private content involved.
</obstacles>

<log>
- 2026-09-21 — general-purpose task-2 executor — wrote `build/windows/make-icon.ps1` (per contract, with the `$Source`/`$source` variable-collision bugfix noted above); ran it to produce `build/windows/overmind.ico` (4 frames, 16/32/48/256, ~120 KB) and regenerate `src/Overmind.Host/Assets/tray-icon.png` (32×32, alpha preserved); verified re-run determinism (byte-identical output); verified `.ico` frame enumeration and raw directory bytes; verified the 256px frame's real pixel data is valid despite the `System.Drawing.Icon` selection-algorithm limitation described in obstacles; ran `dotnet build Overmind.sln` (green, both before and after re-run); ran the Host against an isolated temp DB/port/models dir and confirmed clean startup with `Tray icon active` logged and no exceptions, then terminated it via `taskkill`; did not commit anything (left as working-tree changes per session instruction).
</log>
