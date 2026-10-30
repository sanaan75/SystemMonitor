Add-Type -AssemblyName System.Drawing

$imagesDir = Join-Path $PSScriptRoot 'Images'
$providerAssetsDir = Join-Path $PSScriptRoot 'ProviderAssets'
New-Item -ItemType Directory -Force -Path $imagesDir, $providerAssetsDir | Out-Null

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-RoundedRect($graphics, $brush, [float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-RoundedPath $x $y $width $height $radius
    $graphics.FillPath($brush, $path)
    $path.Dispose()
}

function New-MonitorIcon([int]$size, [string]$outputPath) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle 0, 0, $size, $size),
        ([System.Drawing.ColorTranslator]::FromHtml('#6254E8')),
        ([System.Drawing.ColorTranslator]::FromHtml('#262047')),
        45)
    Draw-RoundedRect $graphics $background 1 1 ($size - 2) ($size - 2) ($size * 0.22)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#71D8C5'))
    $barWidth = $size * 0.12
    $baseline = $size * 0.73
    Draw-RoundedRect $graphics $white ($size * 0.2) ($size * 0.45) $barWidth ($baseline - $size * 0.45) ($barWidth / 2)
    Draw-RoundedRect $graphics $accent ($size * 0.43) ($size * 0.31) $barWidth ($baseline - $size * 0.31) ($barWidth / 2)
    Draw-RoundedRect $graphics $white ($size * 0.66) ($size * 0.53) $barWidth ($baseline - $size * 0.53) ($barWidth / 2)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#B6ACFF')), ($size * 0.035)
    $graphics.DrawLine($pen, $size * 0.17, $baseline, $size * 0.83, $baseline)

    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose(); $white.Dispose(); $accent.Dispose(); $background.Dispose()
    $graphics.Dispose(); $bitmap.Dispose()
}

function New-WidgetPreview([string]$outputPath) {
    $width = 600
    $height = 900
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
        ([System.Drawing.ColorTranslator]::FromHtml('#25385D')),
        ([System.Drawing.ColorTranslator]::FromHtml('#792F4C')),
        90)
    $graphics.FillRectangle($background, 0, 0, $width, $height)
    $overlay = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
        ([System.Drawing.Color]::FromArgb(40, 104, 84, 211)),
        ([System.Drawing.Color]::FromArgb(20, 238, 107, 115)),
        25)
    $graphics.FillRectangle($overlay, 0, 0, $width, $height)

    $panelBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(242, 28, 27, 43))
    $sectionBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(250, 40, 39, 55))
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#F7F5FF'))
    $mutedBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#A6A4B4'))
    $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#C8C0FF'))
    $barBgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#555363'))
    $barFillBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#ECEAF5'))

    $titleFont = New-Object System.Drawing.Font 'Segoe UI', 21, ([System.Drawing.FontStyle]::Bold)
    $labelFont = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Bold)
    $valueFont = New-Object System.Drawing.Font 'Segoe UI', 18, ([System.Drawing.FontStyle]::Bold)
    $bodyFont = New-Object System.Drawing.Font 'Segoe UI', 14, ([System.Drawing.FontStyle]::Regular)
    $smallFont = New-Object System.Drawing.Font 'Segoe UI', 12, ([System.Drawing.FontStyle]::Regular)

    Draw-RoundedRect $graphics $panelBrush 24 24 552 852 28
    $graphics.DrawString('SYSTEM MONITOR', $titleFont, $accentBrush, 52, 48)
    $graphics.DrawString('LIVE RESOURCE USAGE', $smallFont, $mutedBrush, 52, 82)

    $sections = @(
        @{ Top = 124; Height = 154; Title = 'CPU'; Value = '26%'; Details = 'Overall processor activity'; Fill = 0.26 },
        @{ Top = 294; Height = 166; Title = 'MEMORY'; Value = '16.8 GB / 24 GB'; Details = '70% in use'; Fill = 0.70 },
        @{ Top = 476; Height = 160; Title = 'GPU'; Value = '34%  ·  2.4 GB dedicated memory'; Details = '3D engine utilization'; Fill = 0.34 },
        @{ Top = 652; Height = 174; Title = 'NETWORK'; Value = 'Down 1.2 MB/s    Up 48 KB/s'; Details = 'Received             Sent'; Fill = 0.0 }
    )

    foreach ($section in $sections) {
        Draw-RoundedRect $graphics $sectionBrush 44 $section.Top 512 $section.Height 18
        $graphics.DrawString($section.Title, $labelFont, $accentBrush, 66, ($section.Top + 17))
        $graphics.DrawString($section.Value, $valueFont, $whiteBrush, 66, ($section.Top + 50))
        $graphics.DrawString($section.Details, $bodyFont, $mutedBrush, 66, ($section.Top + 88))
        if ($section.Fill -gt 0) {
            $graphics.FillRectangle($barBgBrush, 66, ($section.Top + 126), 468, 9)
            $graphics.FillRectangle($barFillBrush, 66, ($section.Top + 126), (468 * $section.Fill), 9)
        }
    }

    $graphics.DrawString('Refreshes while the Widgets Board is open', $smallFont, $mutedBrush, 52, 838)
    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $background.Dispose(); $overlay.Dispose(); $panelBrush.Dispose(); $sectionBrush.Dispose()
    $whiteBrush.Dispose(); $mutedBrush.Dispose(); $accentBrush.Dispose(); $barBgBrush.Dispose(); $barFillBrush.Dispose()
    $titleFont.Dispose(); $labelFont.Dispose(); $valueFont.Dispose(); $bodyFont.Dispose(); $smallFont.Dispose()
    $graphics.Dispose(); $bitmap.Dispose()
}

$iconPath = Join-Path $providerAssetsDir 'SystemMonitor.png'
New-MonitorIcon 256 $iconPath
New-WidgetPreview (Join-Path $providerAssetsDir 'SystemMonitorPreview.png')

foreach ($asset in @(
    @{ Name = 'StoreLogo.png'; Size = 50 },
    @{ Name = 'Square44x44Logo.png'; Size = 88 },
    @{ Name = 'Square150x150Logo.png'; Size = 300 }
)) {
    $icon = [System.Drawing.Image]::FromFile($iconPath)
    $resized = New-Object System.Drawing.Bitmap $asset.Size, $asset.Size
    $graphics = [System.Drawing.Graphics]::FromImage($resized)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($icon, 0, 0, $asset.Size, $asset.Size)
    $resized.Save((Join-Path $imagesDir $asset.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $resized.Dispose(); $icon.Dispose()
}
