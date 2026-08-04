<#
.SYNOPSIS
  Pipeline screenshot store (Android emulator + composizione canvas iOS).

.DESCRIPTION
  Modalita':
    capture-android   Cattura le 5 schermate dall'emulatore Android attivo via adb
                      e le compone su canvas 1080x1920 (Play Console phone).
    compose-ios       Prende i PNG raw del simulator iOS (da CI macOS o airdrop)
                      e li ridimensiona ai formati App Store 6.9" (1320x2868) e 6.5" (1284x2778).
    frames            Compositore marketing: sfondo brand + titolo/sottotitolo +
                      screenshot con angoli arrotondati e ombra. Copy/colori in
                      store/screenshots/frames.config.json (per locale).
    all               capture-android + compose-ios (frames resta opt-in).

  La composizione avviene con System.Drawing (nativo Windows, nessuna dipendenza).
  Gli output finali vanno in mobile/store/screenshots/{android,ios}/... (gitignored).

.PARAMETER Mode
  capture-android | compose-ios | all

.PARAMETER RawIosDir
  Cartella con i PNG raw iOS (default: store/screenshots/_raw/ios). Atteso contenuto:
  01-dashboard.png 02-timeline.png 03-documents.png 04-doctor-questions.png 05-self-care.png

.PARAMETER CircleName
  Nome del primo cerchio demo sulla dashboard (usato solo come riferimento nel log).

.EXAMPLE
  pwsh scripts/store-screenshots.ps1 -Mode capture-android
.EXAMPLE
  pwsh scripts/store-screenshots.ps1 -Mode compose-ios -RawIosDir .\store\screenshots\_raw\ios
#>
[CmdletBinding()]
param(
  [ValidateSet('capture-android', 'compose-ios', 'compose-android', 'feature-graphic', 'icon-android', 'frames', 'all')]
  [string]$Mode = 'all',

  [string]$RawIosDir = (Join-Path $PSScriptRoot '..\store\screenshots\_raw\ios'),

  [string]$CircleName = 'Famiglia Rossi',

  # Locali per -Mode frames (devono esistere in frames.config.json)
  [string[]]$Locales = @('it-IT')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$MobileRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
$StoreRoot    = Join-Path $MobileRoot 'store\screenshots'
$AndroidOut   = Join-Path $StoreRoot 'android\phone'
$Ios69Out     = Join-Path $StoreRoot 'ios\6.9-inch'
$Ios65Out     = Join-Path $StoreRoot 'ios\6.5-inch'
$FeatureOut   = Join-Path $StoreRoot 'android\feature-graphic'
$TmpDir       = Join-Path $StoreRoot '_tmp'

# Le 5 schermate target, nell'ordine del flow Maestro .maestro/screenshots.yaml
$Shots = @(
  '01-dashboard',
  '02-timeline',
  '03-documents',
  '04-doctor-questions',
  '05-self-care'
)

function Assert-Tool([string]$Name) {
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "Tool '$Name' non trovato nel PATH. Installa Android SDK platform-tools e riprova."
  }
}

# Ridimensiona $SrcPath in un canvas $Width x $Height (cover + crop centrale) e salva in $DstPath.
function Save-Canvas {
  param(
    [Parameter(Mandatory)] [string]$SrcPath,
    [Parameter(Mandatory)] [string]$DstPath,
    [Parameter(Mandatory)] [int]$Width,
    [Parameter(Mandatory)] [int]$Height
  )
  $src = [System.Drawing.Image]::FromFile($SrcPath)
  try {
    $scale = [Math]::Max($Width / $src.Width, $Height / $src.Height)
    $scaledW = [int][Math]::Ceiling($src.Width * $scale)
    $scaledH = [int][Math]::Ceiling($src.Height * $scale)
    $offsetX = [int](($Width - $scaledW) / 2)
    $offsetY = [int](($Height - $scaledH) / 2)

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    try {
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      try {
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::White)
        $g.DrawImage($src, $offsetX, $offsetY, $scaledW, $scaledH)
      } finally { $g.Dispose() }
      $bmp.Save($DstPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
  } finally { $src.Dispose() }
}

# Prima font disponibile da una lista comma-separated (es. "Inter, Segoe UI").
function Get-FontFamily([string]$Names) {
  foreach ($n in ($Names -split ',')) {
    $n = $n.Trim()
    if ($n) {
      try { return New-Object System.Drawing.FontFamily($n) } catch { }
    }
  }
  return New-Object System.Drawing.FontFamily('Microsoft Sans Serif')
}

function New-RoundedRectPath([float]$X, [float]$Y, [float]$W, [float]$H, [float]$R) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $R * 2
  $p.AddArc($X, $Y, $d, $d, 180, 90)
  $p.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
  $p.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
  $p.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

# Frame marketing: sfondo brand, titolo+sottotitolo in alto, screenshot con
# angoli arrotondati e ombra in basso. Output = stesse dimensioni del source.
function Save-Framed {
  param(
    [Parameter(Mandatory)] [string]$SrcPath,
    [Parameter(Mandatory)] [string]$DstPath,
    [Parameter(Mandatory)] [string]$Title,
    [Parameter(Mandatory)] [string]$Subtitle,
    [Parameter(Mandatory)] $Config
  )
  $src = [System.Drawing.Image]::FromFile($SrcPath)
  try {
    $W = $src.Width; $H = $src.Height
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    try {
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      try {
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAlias

        $bg  = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.background)
        $fg  = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.foreground)
        $sub = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.subtle)
        $g.Clear($bg)

        $family = Get-FontFamily ([string]$Config.fontFamily)
        $padX = [float]($W * 0.08)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Near
        $sf.Trimming      = [System.Drawing.StringTrimming]::EllipsisWord

        # Titolo: auto-shrink finche' non entra nel box (max ~2 righe)
        $titleSize = [float]($W * 0.075)
        $titleRect = New-Object System.Drawing.RectangleF($padX, [float]($H * 0.05), [float]($W - 2 * $padX), [float]($H * 0.13))
        $measureArea = New-Object System.Drawing.SizeF([float]$titleRect.Width, [float]10000)
        $f = $null
        while ($titleSize -gt ($W * 0.04)) {
          if ($f) { $f.Dispose() }
          $f = New-Object System.Drawing.Font($family, $titleSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
          $measured = $g.MeasureString($Title, $f, $measureArea)
          if ($measured.Height -le $titleRect.Height) { break }
          $titleSize *= 0.92
        }
        $brushFg = New-Object System.Drawing.SolidBrush($fg)
        $g.DrawString($Title, $f, $brushFg, $titleRect, $sf)
        $f.Dispose()

        # Sottotitolo
        $subFont = New-Object System.Drawing.Font($family, [float]($W * 0.036), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $subRect = New-Object System.Drawing.RectangleF($padX, [float]($H * 0.185), [float]($W - 2 * $padX), [float]($H * 0.07))
        $brushSub = New-Object System.Drawing.SolidBrush($sub)
        $g.DrawString($Subtitle, $subFont, $brushSub, $subRect, $sf)
        $subFont.Dispose()
        $sf.Dispose()

        # Screenshot: fit nell'area inferiore, angoli arrotondati + ombra
        $maxW = $W * 0.82
        $maxH = $H * 0.64
        $scale = [Math]::Min($maxW / $src.Width, $maxH / $src.Height)
        $iw = [float]($src.Width * $scale)
        $ih = [float]($src.Height * $scale)
        $ix = [float](($W - $iw) / 2)
        $iy = [float]($H - ($H * 0.055) - $ih)
        $radius = [float]($W * 0.035)

        # Ombra approssimata: 3 layer sfalsati con alpha crescente
        for ($i = 3; $i -ge 1; $i--) {
          $shadowPath = New-RoundedRectPath ($ix + $i * 2) ($iy + $i * 6) $iw $ih $radius
          $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, 0, 0, 0))
          $g.FillPath($shadowBrush, $shadowPath)
          $shadowBrush.Dispose(); $shadowPath.Dispose()
        }

        $clipPath = New-RoundedRectPath $ix $iy $iw $ih $radius
        $g.SetClip($clipPath)
        $g.DrawImage($src, $ix, $iy, $iw, $ih)
        $g.ResetClip()

        # Bordo sottile chiaro
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), [float]($W * 0.003))
        $g.DrawPath($pen, $clipPath)
        $pen.Dispose(); $clipPath.Dispose()
        $brushFg.Dispose(); $brushSub.Dispose()
      } finally { $g.Dispose() }
      $bmp.Save($DstPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
  } finally { $src.Dispose() }
}

function New-Dir([string]$Path) {
  if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
}

function Capture-Android {
  Assert-Tool adb
  New-Dir $TmpDir; New-Dir $AndroidOut

  Write-Host "==> Cattura da emulatore Android attivo (adb)" -ForegroundColor Cyan
  Write-Host "    Assicurati che l'app sia gia' sulla schermata giusta."
  Write-Host "    Per una cattura automatica usa prima: maestro test .maestro/screenshots.yaml"
  Write-Host "    (poi copia i PNG da ~/.maestro/tests/ in $TmpDir e rilancia con -SkipCapture)." -ForegroundColor DarkGray

  foreach ($shot in $Shots) {
    $dst = Join-Path $TmpDir "$shot.png"
    Write-Host ("  - {0} ... premi INVIO quando l'emulatore mostra la schermata" -f $shot) -NoNewline
    [void][Console]::ReadLine()
    adb exec-out screencap -p | Set-Content -Path $dst -Encoding Byte -NoNewline
    Write-Host " catturato."

    $final = Join-Path $AndroidOut "$shot.png"
    Save-Canvas -SrcPath $dst -DstPath $final -Width 1080 -Height 1920
    Write-Host ("    -> {0} (1080x1920)" -f $final) -ForegroundColor Green
  }
}

function Compose-Ios {
  New-Dir $Ios69Out; New-Dir $Ios65Out

  if (-not (Test-Path $RawIosDir)) {
    throw "Cartella raw iOS non trovata: $RawIosDir`nScarica prima gli artifact dal workflow GitHub 'store-screenshots' (runner macOS) oppure copia qui i PNG del simulator."
  }

  Write-Host "==> Composizione canvas iOS da $RawIosDir" -ForegroundColor Cyan
  foreach ($shot in $Shots) {
    $src = Join-Path $RawIosDir "$shot.png"
    if (-not (Test-Path $src)) {
      Write-Warning "  ! manca $shot.png in $RawIosDir - salto."
      continue
    }
    Save-Canvas -SrcPath $src -DstPath (Join-Path $Ios69Out "$shot.png") -Width 1320 -Height 2868
    Save-Canvas -SrcPath $src -DstPath (Join-Path $Ios65Out "$shot.png") -Width 1284 -Height 2778
    Write-Host ('  - {0}: 6.9-inch (1320x2868) + 6.5-inch (1284x2778)' -f $shot) -ForegroundColor Green
  }
}

function Compose-Frames {
  $configPath = Join-Path $StoreRoot 'frames.config.json'
  if (-not (Test-Path $configPath)) {
    throw "Config frame non trovata: $configPath"
  }
  # UTF8 esplicito: i copy contengono accenti e il file potrebbe non avere BOM
  $cfg = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json

  # Frame su tutti i set gia' composti che esistono
  $targets = @(
    @{ Dir = $AndroidOut; Rel = 'android\phone' },
    @{ Dir = $Ios69Out;   Rel = 'ios\6.9-inch' },
    @{ Dir = $Ios65Out;   Rel = 'ios\6.5-inch' }
  ) | Where-Object { Test-Path $_.Dir }

  if (-not $targets) {
    throw "Nessun set composto trovato. Esegui prima -Mode capture-android e/o compose-ios."
  }

  foreach ($locale in $Locales) {
    $locCfg = $cfg.locales.$locale
    if (-not $locCfg) {
      Write-Warning "Locale '$locale' assente in frames.config.json - salto."
      continue
    }
    foreach ($t in $targets) {
      foreach ($shot in $Shots) {
        $src = Join-Path $t.Dir "$shot.png"
        if (-not (Test-Path $src)) { continue }
        $cap = $locCfg.$shot
        if (-not $cap) {
          Write-Warning "  ! caption mancante per $locale/$shot - salto."
          continue
        }
        $dst = Join-Path $StoreRoot "framed\$locale\$($t.Rel)\$shot.png"
        New-Dir (Split-Path $dst)
        Save-Framed -SrcPath $src -DstPath $dst -Title $cap.title -Subtitle $cap.subtitle -Config $cfg
        Write-Host "  - framed\$locale\$($t.Rel)\$shot.png" -ForegroundColor Green
      }
    }
  }

  Write-Host "`nFrame marketing in: $(Join-Path $StoreRoot 'framed')" -ForegroundColor Cyan
}

# Compone i canvas telefono Google Play (1080x1920, ratio 9:16) dai raw iOS.
# Google Play accetta screenshot da qualsiasi fonte: riusiamo i PNG del
# simulator iOS senza bisogno di un emulatore Android.
function Compose-Android {
  New-Dir $AndroidOut

  if (-not (Test-Path $RawIosDir)) {
    throw "Cartella raw non trovata: $RawIosDir`nScarica prima gli artifact dal workflow 'store-screenshots' o copia qui i PNG."
  }

  Write-Host "==> Composizione canvas Android (Play) da $RawIosDir" -ForegroundColor Cyan
  foreach ($shot in $Shots) {
    $src = Join-Path $RawIosDir "$shot.png"
    if (-not (Test-Path $src)) {
      Write-Warning "  ! manca $shot.png in $RawIosDir - salto."
      continue
    }
    Save-Canvas -SrcPath $src -DstPath (Join-Path $AndroidOut "$shot.png") -Width 1080 -Height 1920
    Write-Host ('  - {0}: phone (1080x1920)' -f $shot) -ForegroundColor Green
  }
}

# Feature graphic Google Play (1024x500): banner brand con titolo, sottotitolo
# e (se presente) l'icona app centrata a destra. Obbligatorio per Play Console.
function Save-FeatureGraphic {
  param(
    [Parameter(Mandatory)] [string]$DstPath,
    [Parameter(Mandatory)] $Config,
    [Parameter(Mandatory)] [string]$Title,
    [Parameter(Mandatory)] [string]$Subtitle,
    [string]$IconPath
  )
  $W = 1024; $H = 500
  $bmp = New-Object System.Drawing.Bitmap $W, $H
  try {
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
      $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

      $bg  = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.background)
      $fg  = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.foreground)
      $sub = [System.Drawing.ColorTranslator]::FromHtml([string]$Config.subtle)
      $g.Clear($bg)

      $family = Get-FontFamily ([string]$Config.fontFamily)

      # Icona app a destra (se esiste), tonda con angoli arrotondati.
      $iconBox = 300
      $hasIcon = $IconPath -and (Test-Path $IconPath)
      if ($hasIcon) {
        $icon = [System.Drawing.Image]::FromFile($IconPath)
        try {
          $ix = [float]($W - $iconBox - 60)
          $iy = [float](($H - $iconBox) / 2)
          $radius = [float]($iconBox * 0.22)
          $clip = New-RoundedRectPath $ix $iy $iconBox $iconBox $radius
          $g.SetClip($clip)
          $g.DrawImage($icon, $ix, $iy, [float]$iconBox, [float]$iconBox)
          $g.ResetClip()
          $clip.Dispose()
        } finally { $icon.Dispose() }
      }

      # Testo a sinistra, allineato verticalmente al centro.
      $textLeft  = 70
      $textWidth = if ($hasIcon) { $W - $iconBox - 160 } else { $W - 140 }

      $sf = New-Object System.Drawing.StringFormat
      $sf.Alignment     = [System.Drawing.StringAlignment]::Near
      $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
      $sf.Trimming      = [System.Drawing.StringTrimming]::EllipsisWord

      $titleFont = New-Object System.Drawing.Font($family, 58, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
      $titleRect = New-Object System.Drawing.RectangleF([float]$textLeft, [float]140, [float]$textWidth, [float]140)
      $brushFg = New-Object System.Drawing.SolidBrush($fg)
      $g.DrawString($Title, $titleFont, $brushFg, $titleRect, $sf)
      $titleFont.Dispose()

      $subFont = New-Object System.Drawing.Font($family, 30, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
      $subRect = New-Object System.Drawing.RectangleF([float]$textLeft, [float]290, [float]$textWidth, [float]90)
      $brushSub = New-Object System.Drawing.SolidBrush($sub)
      $g.DrawString($Subtitle, $subFont, $brushSub, $subRect, $sf)
      $subFont.Dispose()

      $brushFg.Dispose(); $brushSub.Dispose(); $sf.Dispose()
    } finally { $g.Dispose() }
    $bmp.Save($DstPath, [System.Drawing.Imaging.ImageFormat]::Png)
  } finally { $bmp.Dispose() }
}

function Compose-FeatureGraphic {
  New-Dir $FeatureOut
  $configPath = Join-Path $StoreRoot 'frames.config.json'
  if (-not (Test-Path $configPath)) { throw "Config non trovata: $configPath" }
  $cfg = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json

  # Icona app: prova assets/icon.png (1024x1024 tipico Expo).
  $iconPath = Join-Path $MobileRoot 'assets\icon.png'
  if (-not (Test-Path $iconPath)) { $iconPath = $null }

  # Copy: preferisce la sezione dedicata `featureGraphic.<locale>` (testi corti
  # pensati per il banner 1024x500); fallback sulla caption 01-dashboard.
  Write-Host "==> Feature graphic Google Play (1024x500)" -ForegroundColor Cyan
  foreach ($locale in $Locales) {
    $cap = $null
    if ($cfg.PSObject.Properties.Name -contains 'featureGraphic' -and
        $cfg.featureGraphic.PSObject.Properties.Name -contains $locale) {
      $cap = $cfg.featureGraphic.$locale
    }
    if (-not $cap) {
      $locCfg = $cfg.locales.$locale
      if ($locCfg -and $locCfg.'01-dashboard') { $cap = $locCfg.'01-dashboard' }
    }
    if (-not $cap) {
      Write-Warning "Locale '$locale' senza copy feature graphic - salto."
      continue
    }
    $dst = Join-Path $FeatureOut "$locale.png"
    Save-FeatureGraphic -DstPath $dst -Config $cfg -Title $cap.title -Subtitle $cap.subtitle -IconPath $iconPath
    Write-Host ("  - {0}.png (1024x500){1}" -f $locale, $(if ($iconPath) { ' + icona' } else { ' (senza icona: assets/icon.png assente)' })) -ForegroundColor Green
  }
  Write-Host "`nFeature graphic in: $FeatureOut" -ForegroundColor Cyan
}

# Icona Play Store 512x512: resize di assets/icon.png (Expo, 1024x1024).
function Compose-IconAndroid {
  New-Dir $AndroidOut
  $iconSrc = Join-Path $MobileRoot 'assets\icon.png'
  if (-not (Test-Path $iconSrc)) { throw "assets/icon.png non trovato: $iconSrc" }
  $dst = Join-Path (Join-Path $StoreRoot 'android') 'icon-512.png'
  New-Dir (Split-Path $dst)
  Save-Canvas -SrcPath $iconSrc -DstPath $dst -Width 512 -Height 512
  Write-Host "==> Icona Play Store" -ForegroundColor Cyan
  Write-Host ("  - {0} (512x512)" -f $dst) -ForegroundColor Green
}

switch ($Mode) {
  'capture-android' { Capture-Android }
  'compose-ios'     { Compose-Ios }
  'compose-android' { Compose-Android }
  'feature-graphic' { Compose-FeatureGraphic }
  'icon-android'    { Compose-IconAndroid }
  'frames'          { Compose-Frames }
  'all'             { Capture-Android; Compose-Ios }
}

Write-Host "`nFatto. Output in:" -ForegroundColor Cyan
Write-Host "  Android (Play) : $AndroidOut"
Write-Host "  iOS 6.9-inch   : $Ios69Out"
Write-Host "  iOS 6.5-inch   : $Ios65Out"
Write-Host "`nCarica questi PNG direttamente in App Store Connect / Play Console." -ForegroundColor DarkGray
