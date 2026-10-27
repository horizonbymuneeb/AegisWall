$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing

function New-RoundPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Shield($g, [float]$x, [float]$y, [float]$size, [System.Drawing.Color]$color, [float]$stroke) {
    $points = @(
        (New-Object System.Drawing.PointF(($x + $size * 0.5), ($y + $size * 0.08))),
        (New-Object System.Drawing.PointF(($x + $size * 0.88), ($y + $size * 0.24))),
        (New-Object System.Drawing.PointF(($x + $size * 0.88), ($y + $size * 0.55))),
        (New-Object System.Drawing.PointF(($x + $size * 0.5), ($y + $size * 0.92))),
        (New-Object System.Drawing.PointF(($x + $size * 0.12), ($y + $size * 0.55))),
        (New-Object System.Drawing.PointF(($x + $size * 0.12), ($y + $size * 0.24)))
    )
    $pen = New-Object System.Drawing.Pen($color, $stroke)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($pen, $points)
    $pen.Dispose()

    $hole = New-Object System.Drawing.SolidBrush($color)
    $g.FillEllipse($hole, ($x + $size * 0.42), ($y + $size * 0.38), ($size * 0.16), ($size * 0.16))
    $g.FillRectangle($hole, ($x + $size * 0.47), ($y + $size * 0.48), ($size * 0.06), ($size * 0.18))
    $hole.Dispose()
}

# Logo
$logo = New-Object System.Drawing.Bitmap 256, 256
$lg = [System.Drawing.Graphics]::FromImage($logo)
$lg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$lg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$lg.Clear([System.Drawing.Color]::Transparent)
$blue = [System.Drawing.Color]::FromArgb(37, 99, 235)
$blue2 = [System.Drawing.Color]::FromArgb(29, 78, 216)
$rect = New-Object System.Drawing.RectangleF(8, 8, 240, 240)
$path = New-RoundPath $rect 52
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $blue, $blue2, 45)
$lg.FillPath($brush, $path)
$brush.Dispose()
$path.Dispose()
Draw-Shield $lg 48 44 160 ([System.Drawing.Color]::White) 11
$logo.Save((Join-Path $root 'assets\logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$lg.Dispose()
$logo.Dispose()

# Social preview
$w = 1280
$h = 640
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.Clear([System.Drawing.Color]::FromArgb(246, 247, 249))

$accent = [System.Drawing.Color]::FromArgb(37, 99, 235)
$strong = [System.Drawing.Color]::FromArgb(15, 23, 42)
$muted = [System.Drawing.Color]::FromArgb(107, 114, 128)
$border = [System.Drawing.Color]::FromArgb(229, 231, 235)
$white = [System.Drawing.Color]::White
$green = [System.Drawing.Color]::FromArgb(21, 128, 61)
$greenSoft = [System.Drawing.Color]::FromArgb(240, 253, 244)

# Soft background rings
$ringPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(16, 37, 99, 235), 2)
$g.DrawEllipse($ringPen, 900, -180, 560, 560)
$g.DrawEllipse($ringPen, 980, -100, 400, 400)
$ringPen.Dispose()

# Logo tile
$tileRect = New-Object System.Drawing.RectangleF(80, 74, 96, 96)
$tilePath = New-RoundPath $tileRect 22
$tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tileRect, $blue, $blue2, 45)
$g.FillPath($tileBrush, $tilePath)
$tileBrush.Dispose()
$tilePath.Dispose()
Draw-Shield $g 100 88 56 $white 5

$fontTitle = New-Object System.Drawing.Font('Segoe UI', 62, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontTag = New-Object System.Drawing.Font('Segoe UI', 25, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontBullet = New-Object System.Drawing.Font('Segoe UI', 23, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontSmall = New-Object System.Drawing.Font('Segoe UI', 18, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontPill = New-Object System.Drawing.Font('Segoe UI', 17, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontStat = New-Object System.Drawing.Font('Segoe UI', 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontStatLabel = New-Object System.Drawing.Font('Segoe UI', 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$g.DrawString('AegisWall', $fontTitle, (New-Object System.Drawing.SolidBrush($strong)), 198, 64)
$g.DrawString('Hardware-anchored application firewall', $fontTag, (New-Object System.Drawing.SolidBrush($muted)), 202, 146)

$linePen = New-Object System.Drawing.Pen($border, 2)
$g.DrawLine($linePen, 82, 210, 620, 210)
$linePen.Dispose()

$bullets = @(
    'Strict per-app allowlist with WFP enforcement',
    'Trigger-based network lockdown and grace period',
    'YubiKey HMAC or machine-bound USB key anchor',
    'Hash-chained audit log and recovery code'
)
$y = 250
foreach ($text in $bullets) {
    $dot = New-Object System.Drawing.SolidBrush($accent)
    $g.FillEllipse($dot, 84, ($y + 8), 16, 16)
    $dot.Dispose()
    $checkPen = New-Object System.Drawing.Pen($white, 2)
    $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLines($checkPen, @(
        (New-Object System.Drawing.PointF(88, ($y + 16))),
        (New-Object System.Drawing.PointF(91, ($y + 19))),
        (New-Object System.Drawing.PointF(96, ($y + 13)))
    ))
    $checkPen.Dispose()
    $g.DrawString($text, $fontBullet, (New-Object System.Drawing.SolidBrush($strong)), 116, $y)
    $y += 52
}

$g.DrawString('github.com/horizonbymuneeb/AegisWall', $fontSmall, (New-Object System.Drawing.SolidBrush($muted)), 84, 560)

# Right card
$cardRect = New-Object System.Drawing.RectangleF(720, 76, 480, 488)
$cardPath = New-RoundPath $cardRect 24
$shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(18, 15, 23, 42))
$shadowPath = New-RoundPath (New-Object System.Drawing.RectangleF(726, 86, 480, 488)) 24
$g.FillPath($shadow, $shadowPath)
$shadow.Dispose()
$shadowPath.Dispose()
$cardBrush = New-Object System.Drawing.SolidBrush($white)
$g.FillPath($cardBrush, $cardPath)
$cardBrush.Dispose()
$cardPen = New-Object System.Drawing.Pen($border, 2)
$g.DrawPath($cardPen, $cardPath)
$cardPen.Dispose()
$cardPath.Dispose()

$g.DrawString('Dashboard', $fontStat, (New-Object System.Drawing.SolidBrush($strong)), 756, 108)
$g.DrawString('Protection at a glance', $fontSmall, (New-Object System.Drawing.SolidBrush($muted)), 758, 146)

$pillRect = New-Object System.Drawing.RectangleF(1032, 108, 132, 34)
$pillPath = New-RoundPath $pillRect 17
$pillBrush = New-Object System.Drawing.SolidBrush($greenSoft)
$g.FillPath($pillBrush, $pillPath)
$pillBrush.Dispose()
$pillPath.Dispose()
$g.FillEllipse((New-Object System.Drawing.SolidBrush($green)), 1048, 121, 9, 9)
$g.DrawString('Protected', $fontPill, (New-Object System.Drawing.SolidBrush($green)), 1064, 115)

$heroRect = New-Object System.Drawing.RectangleF(756, 190, 408, 130)
$heroPath = New-RoundPath $heroRect 18
$heroBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(239, 246, 255))
$g.FillPath($heroBrush, $heroPath)
$heroBrush.Dispose()
$heroPath.Dispose()
Draw-Shield $g 780 212 64 $accent 5
$g.DrawString('Protected', $fontStat, (New-Object System.Drawing.SolidBrush($green)), 864, 208)
$g.DrawString('Only allowlisted apps reach the network.', $fontSmall, (New-Object System.Drawing.SolidBrush($muted)), 866, 250)
$g.DrawString('YubiKey connected', $fontPill, (New-Object System.Drawing.SolidBrush($green)), 866, 282)

$stats = @(
    @('6', 'Allowed apps'),
    @('12', 'Blocked'),
    @('2', 'Lockdowns')
)
$x = 756
foreach ($stat in $stats) {
    $sRect = New-Object System.Drawing.RectangleF($x, 340, 126, 104)
    $sPath = New-RoundPath $sRect 16
    $sBrush = New-Object System.Drawing.SolidBrush($white)
    $g.FillPath($sBrush, $sPath)
    $sBrush.Dispose()
    $sPen = New-Object System.Drawing.Pen($border, 2)
    $g.DrawPath($sPen, $sPath)
    $sPen.Dispose()
    $sPath.Dispose()
    $g.DrawString($stat[0], $fontStat, (New-Object System.Drawing.SolidBrush($strong)), ($x + 16), 356)
    $g.DrawString($stat[1], $fontStatLabel, (New-Object System.Drawing.SolidBrush($muted)), ($x + 16), 400)
    $x += 141
}

$g.DrawString('Protection checks', $fontStatLabel, (New-Object System.Drawing.SolidBrush($strong)), 756, 444)
$checkY = 466
$checks = @('WFP filters applied', 'Policy signature verified', 'Trigger monitor active')
foreach ($check in $checks) {
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($greenSoft)), 756, $checkY, 18, 18)
    $cp = New-Object System.Drawing.Pen($green, 2)
    $cp.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cp.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLines($cp, @(
        (New-Object System.Drawing.PointF(760, ($checkY + 9))),
        (New-Object System.Drawing.PointF(764, ($checkY + 13))),
        (New-Object System.Drawing.PointF(770, ($checkY + 6)))
    ))
    $cp.Dispose()
    $g.DrawString($check, $fontSmall, (New-Object System.Drawing.SolidBrush($strong)), 786, ($checkY - 1))
    $checkY += 30
}

$bmp.Save((Join-Path $root 'assets\social-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose()
$bmp.Dispose()
$fontTitle.Dispose(); $fontTag.Dispose(); $fontBullet.Dispose(); $fontSmall.Dispose(); $fontPill.Dispose(); $fontStat.Dispose(); $fontStatLabel.Dispose()

Write-Host 'Generated assets\logo.png and assets\social-preview.png'
