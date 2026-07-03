param()
Add-Type -AssemblyName System.Drawing

function New-IconPng {
  param([string]$Path,[int]$W,[int]$H,[string]$Bg,[string]$Fg,[string]$Letter = 'A')
  $bmp = New-Object System.Drawing.Bitmap($W, $H)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'AntiAlias'
  if ($Bg -eq 'transparent') {
    $g.Clear([System.Drawing.Color]::Transparent)
  }
  else {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml($Bg))
  }
  if ($Letter) {
    $fontSize = [int]([Math]::Min($W, $H) * 0.55)
    $font = New-Object System.Drawing.Font('Arial Black', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Fg))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'
    $sf.LineAlignment = 'Center'
    $g.DrawString($Letter, $font, $brush, (New-Object System.Drawing.RectangleF(0, 0, $W, $H)), $sf)
    $brush.Dispose(); $font.Dispose()
  }
  $g.Dispose()
  $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

$assetsDir = Join-Path $PSScriptRoot '..\mobile\assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
New-IconPng -Path (Join-Path $assetsDir 'icon.png')              -W 1024 -H 1024 -Bg '#0f766e' -Fg '#ffffff'
New-IconPng -Path (Join-Path $assetsDir 'adaptive-icon.png')     -W 1024 -H 1024 -Bg '#0f766e' -Fg '#ffffff'
New-IconPng -Path (Join-Path $assetsDir 'splash.png')            -W 1284 -H 2778 -Bg '#f8fafc' -Fg '#0f766e'
New-IconPng -Path (Join-Path $assetsDir 'favicon.png')           -W 48   -H 48   -Bg '#0f766e' -Fg '#ffffff'
New-IconPng -Path (Join-Path $assetsDir 'notification-icon.png') -W 96   -H 96   -Bg 'transparent' -Fg '#ffffff'
Get-ChildItem $assetsDir | Select-Object Name, Length
