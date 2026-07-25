<#
    New-AppIcon.ps1 - draws the Follower Forge app icon and writes a multi-size .ico.

    The mark: a gold faceted gem (the motif from the project banner) over the app's dark
    plate, with a teal spark. Drawn as vector shapes so it stays crisp at 16px.
#>
[CmdletBinding()]
param(
    [string]$OutPath = (Join-Path $PSScriptRoot '..\src\Ui\Assets\appicon.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$plate = [System.Drawing.ColorTranslator]::FromHtml('#16161b')
$gold  = [System.Drawing.ColorTranslator]::FromHtml('#e8c877')
$gold2 = [System.Drawing.ColorTranslator]::FromHtml('#b98f3e')
$teal  = [System.Drawing.ColorTranslator]::FromHtml('#4fd6d2')

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded dark plate
    $r = [math]::Max(2, [int]($Size * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($Size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($Size - $d, $Size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $Size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $plate), $path)

    # gem: upper crown + lower point, centred
    $cx = $Size / 2.0
    $top = $Size * 0.20
    $mid = $Size * 0.46
    $bot = $Size * 0.82
    $halfW = $Size * 0.28
    $innerW = $Size * 0.14

    $crown = @(
        (New-Object System.Drawing.PointF (($cx - $innerW), $top)),
        (New-Object System.Drawing.PointF (($cx + $innerW), $top)),
        (New-Object System.Drawing.PointF (($cx + $halfW), $mid)),
        (New-Object System.Drawing.PointF (($cx - $halfW), $mid))
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $gold), [System.Drawing.PointF[]]$crown)

    $pavilion = @(
        (New-Object System.Drawing.PointF (($cx - $halfW), $mid)),
        (New-Object System.Drawing.PointF (($cx + $halfW), $mid)),
        (New-Object System.Drawing.PointF $cx, $bot)
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $gold2), [System.Drawing.PointF[]]$pavilion)

    # facet highlight so the gem reads as faceted, not a flat blob
    $facet = @(
        (New-Object System.Drawing.PointF (($cx - $innerW), $top)),
        (New-Object System.Drawing.PointF $cx, $mid),
        (New-Object System.Drawing.PointF (($cx - $halfW), $mid))
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 255, 255, 255))),
        [System.Drawing.PointF[]]$facet)

    # teal spark under the gem (only where it will still be visible)
    if ($Size -ge 32) {
        $pen = New-Object System.Drawing.Pen $teal, ([single]([math]::Max(1, $Size * 0.035)))
        $g.DrawLine($pen, [single]($cx - $halfW * 0.9), [single]($Size * 0.90),
                          [single]($cx + $halfW * 0.9), [single]($Size * 0.90))
        $pen.Dispose()
    }

    $g.Dispose()
    return $bmp
}

$sizes = 256, 128, 64, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap -Size $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
}

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

# ICO container: PNG-compressed frames (supported since Vista).
$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))   # 0 means 256
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush(); $bw.Dispose(); $fs.Dispose()

Write-Host "Icon written: $OutPath ($((Get-Item $OutPath).Length) bytes, sizes: $($sizes -join ', '))"
