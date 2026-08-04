<#
.SYNOPSIS
  Build (e opzionalmente submit) della app Android per Google Play via EAS.

.DESCRIPTION
  Controlli preliminari + `eas build --platform android --profile production`
  (produce un Android App Bundle .aab). La build gira sui server EAS, nessun
  Android SDK locale richiesto.

  IMPORTANTE sul PRIMO rilascio: Google Play NON accetta il PRIMISSIMO .aab via
  API (`eas submit`). Il primo bundle va caricato MANUALMENTE dalla Play Console
  (Crea release -> Internal testing / Produzione -> carica .aab). Solo dai
  rilasci SUCCESSIVI funziona `eas submit` con un service account JSON in
  secrets/play-service-account.json (referenziato da eas.json submit.production.android).

  Per questo lo script di default fa SOLO la build e stampa il link per scaricare
  l'.aab. Usa -Submit solo se hai gia' fatto il primo upload manuale E hai il
  service account configurato.

.PARAMETER Submit
  Aggiunge --auto-submit (richiede secrets/play-service-account.json e primo
  upload manuale gia' avvenuto). Lo script verifica la presenza del file.

.PARAMETER SkipGitCheck
  Salta il controllo del working tree pulito (sconsigliato).

.PARAMETER DryRun
  Esegue i controlli e stampa il comando finale senza eseguirlo.

.EXAMPLE
  # Primo rilascio: build .aab da caricare a mano in Play Console
  powershell -ExecutionPolicy Bypass -File scripts/release-android.ps1

.EXAMPLE
  # Rilasci successivi (service account configurato)
  powershell -ExecutionPolicy Bypass -File scripts/release-android.ps1 -Submit
#>
[CmdletBinding()]
param(
  [switch]$Submit,
  [switch]$SkipGitCheck,
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MobileRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$RepoRoot   = (Resolve-Path (Join-Path $MobileRoot '..')).Path

function Write-Step([string]$Msg)  { Write-Host "==> $Msg" -ForegroundColor Cyan }
function Write-Ok([string]$Msg)    { Write-Host "  OK  $Msg" -ForegroundColor Green }
function Write-Warn2([string]$Msg) { Write-Host "  !   $Msg" -ForegroundColor Yellow }
function Fail([string]$Msg)        { Write-Host "  X   $Msg" -ForegroundColor Red; exit 1 }

Push-Location $MobileRoot
try {
  # --- 1. eas-cli ------------------------------------------------------------
  Write-Step 'Controllo eas-cli'
  if (-not (Get-Command eas -ErrorAction SilentlyContinue)) {
    Fail "eas-cli non trovato nel PATH. Installa con: npm install -g eas-cli"
  }
  Write-Ok 'eas-cli presente'

  # --- 2. login EAS (stderr-safe, vedi release-ios.ps1) ----------------------
  Write-Step 'Controllo login EAS'
  $who = ''
  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { $who = (cmd /c 'eas whoami 2>&1') | Out-String } catch { $who = '' } finally { $ErrorActionPreference = $prevEap }
  $userLine = ($who -split "`n") |
    Where-Object { $_ -match '@' -and $_ -notmatch 'eas-cli' -and $_ -notmatch 'available' } |
    Select-Object -First 1
  if ($who -match 'Not logged in' -or -not $userLine) {
    Fail "Non sei loggato in EAS. Esegui: eas login"
  }
  Write-Ok ("Loggato (" + $userLine.Trim() + ")")

  # --- 3. profilo production -------------------------------------------------
  Write-Step 'Controllo profilo production in eas.json'
  $easJsonPath = Join-Path $MobileRoot 'eas.json'
  if (-not (Test-Path $easJsonPath)) { Fail "eas.json non trovato in $MobileRoot" }
  $easJson = Get-Content $easJsonPath -Raw | ConvertFrom-Json

  $prod = $easJson.build.production
  if (-not $prod) { Fail "Profilo build.production assente in eas.json" }

  $buildType = $null
  if ($prod.PSObject.Properties.Name -contains 'android' -and
      $prod.android.PSObject.Properties.Name -contains 'buildType') {
    $buildType = [string]$prod.android.buildType
  }
  if ($buildType -ne 'app-bundle') {
    Write-Warn2 "android.buildType = '$buildType' (atteso 'app-bundle' per Play). Continuo comunque."
  } else {
    Write-Ok "android.buildType = app-bundle (.aab per Play)"
  }

  $autoInc = $false
  if ($prod.PSObject.Properties.Name -contains 'autoIncrement') { $autoInc = [bool]$prod.autoIncrement }
  if ($autoInc) { Write-Ok 'autoIncrement attivo (versionCode gestito da EAS)' }
  else { Write-Warn2 'autoIncrement non attivo: gestisci il versionCode manualmente' }

  # --- 4. submit: service account --------------------------------------------
  if ($Submit) {
    Write-Step 'Controllo prerequisiti submit Android'
    $saPath = $null
    if ($easJson.submit.production.android.PSObject.Properties.Name -contains 'serviceAccountKeyPath') {
      $saPath = Join-Path $MobileRoot ([string]$easJson.submit.production.android.serviceAccountKeyPath)
    }
    if (-not $saPath -or -not (Test-Path $saPath)) {
      Fail "Service account Play mancante: $saPath`nIl PRIMO .aab va caricato A MANO in Play Console. Rilancia SENZA -Submit, scarica l'.aab e caricalo manualmente."
    }
    Write-Ok "service account presente ($saPath)"
    Write-Warn2 "Assicurati che il PRIMO upload su Play sia gia' avvenuto MANUALMENTE: l'API rifiuta il primissimo bundle."
  }

  # --- 5. git ----------------------------------------------------------------
  if (-not $SkipGitCheck) {
    Write-Step 'Controllo git working tree'
    Push-Location $RepoRoot
    try { $dirty = (& git status --porcelain 2>&1 | Out-String).Trim() } finally { Pop-Location }
    if ($dirty) {
      Write-Warn2 'Working tree NON pulito. EAS builda dal commit archiviato.'
      Write-Host $dirty
      $ans = Read-Host 'Continuare comunque? (s/N)'
      if ($ans -notmatch '^[sSyY]') { Fail 'Interrotto: committa o stasha le modifiche.' }
    } else { Write-Ok 'Working tree pulito' }
  } else { Write-Warn2 'Controllo git saltato (-SkipGitCheck)' }

  # --- 6. riepilogo ----------------------------------------------------------
  Write-Step 'Riepilogo release Android'
  $appConfig = Get-Content (Join-Path $MobileRoot 'app.config.ts') -Raw
  $version = if ($appConfig -match "version:\s*'([^']+)'") { $Matches[1] } else { '?' }
  Write-Host ""
  Write-Host "  Piattaforma : Android" -ForegroundColor White
  Write-Host "  Profilo     : production (app-bundle .aab)" -ForegroundColor White
  Write-Host "  Package     : app.accanto.mobile" -ForegroundColor White
  Write-Host "  Versione    : $version (versionCode auto-incrementato)" -ForegroundColor White
  Write-Host "  API         : $($prod.env.EXPO_PUBLIC_API_BASE_URL)" -ForegroundColor White
  if ($Submit) {
    Write-Host "  Submit      : SI (--auto-submit -> Play track '$($easJson.submit.production.android.track)')" -ForegroundColor White
  } else {
    Write-Host "  Submit      : NO (build only). Scarica l'.aab e caricalo a mano in Play Console (primo rilascio)." -ForegroundColor White
  }
  Write-Host ""

  # --- 7. esegui -------------------------------------------------------------
  $easArgs = @('build', '--platform', 'android', '--profile', 'production')
  if ($Submit) { $easArgs += '--auto-submit' }
  $cmdPreview = 'eas ' + ($easArgs -join ' ')

  if ($DryRun) {
    Write-Step 'DRY RUN: comando che verrebbe eseguito'
    Write-Host "  $cmdPreview" -ForegroundColor Magenta
    Write-Ok 'Nessuna build lanciata (-DryRun).'
    return
  }

  Write-Step 'Conferma finale'
  Write-Host "  Sto per eseguire:  $cmdPreview" -ForegroundColor Magenta
  Write-Host "  La build gira sui server EAS (~15-30 min). Monitori su expo.dev." -ForegroundColor DarkGray
  $go = Read-Host 'Procedere? (s/N)'
  if ($go -notmatch '^[sSyY]') { Fail 'Interrotto dall utente.' }

  Write-Step "Avvio: $cmdPreview"
  & eas @easArgs
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { Fail "eas build ha restituito exit code $exit" }
  Write-Ok 'Comando eas completato. Scarica l''.aab dal link e caricalo in Play Console (primo rilascio).'
}
finally {
  Pop-Location
}
