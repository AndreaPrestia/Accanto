<#
.SYNOPSIS
  Build (e opzionalmente submit) della app iOS per App Store Connect via EAS.

.DESCRIPTION
  Esegue una serie di CONTROLLI PRELIMINARI prima di lanciare la build di
  produzione sui server EAS di Expo (nessun Mac locale richiesto):

    1. eas-cli presente nel PATH
    2. login EAS attivo (eas whoami)
    3. profilo 'production' presente in eas.json (no simulator, autoIncrement)
    4. git working tree pulito (EAS builda da un archivio del commit)
    5. riepilogo di cosa verra' pubblicato (bundle id, versione, ascAppId)

  Poi, previa CONFERMA esplicita, lancia:
    eas build --platform ios --profile production [--auto-submit]

  NB: la build di PRODUZIONE e' diversa da quella 'store-screenshots'
  (quest'ultima e' un .app per SIMULATORE, non firmato, non pubblicabile).
  Gli screenshot NON vanno rifatti: sono in store/screenshots/.

.PARAMETER Submit
  Se presente, aggiunge --auto-submit: a build finita invia automaticamente
  ad App Store Connect usando il profilo submit.production di eas.json.

.PARAMETER SkipGitCheck
  Salta il controllo del working tree pulito (sconsigliato).

.PARAMETER DryRun
  Esegue tutti i controlli e STAMPA il comando finale senza eseguirlo.

.EXAMPLE
  # Solo build (poi submit manuale con eas submit)
  powershell -ExecutionPolicy Bypass -File scripts/release-ios.ps1

.EXAMPLE
  # Build + invio automatico ad App Store Connect
  powershell -ExecutionPolicy Bypass -File scripts/release-ios.ps1 -Submit

.EXAMPLE
  # Verifica soltanto (nessuna build)
  powershell -ExecutionPolicy Bypass -File scripts/release-ios.ps1 -Submit -DryRun
#>
[CmdletBinding()]
param(
  [switch]$Submit,
  [switch]$SkipGitCheck,
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# La cartella mobile/ (parent di scripts/). Tutti i comandi eas girano qui.
$MobileRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$RepoRoot   = (Resolve-Path (Join-Path $MobileRoot '..')).Path

function Write-Step([string]$Msg)  { Write-Host "==> $Msg" -ForegroundColor Cyan }
function Write-Ok([string]$Msg)    { Write-Host "  OK  $Msg" -ForegroundColor Green }
function Write-Warn2([string]$Msg) { Write-Host "  !   $Msg" -ForegroundColor Yellow }
function Fail([string]$Msg)        { Write-Host "  X   $Msg" -ForegroundColor Red; exit 1 }

Push-Location $MobileRoot
try {
  # --- 1. eas-cli disponibile ------------------------------------------------
  Write-Step 'Controllo eas-cli'
  if (-not (Get-Command eas -ErrorAction SilentlyContinue)) {
    Fail "eas-cli non trovato nel PATH. Installa con: npm install -g eas-cli"
  }
  Write-Ok 'eas-cli presente'

  # --- 2. login EAS ----------------------------------------------------------
  Write-Step 'Controllo login EAS'
  # eas-cli scrive un warning "eas-cli e' ora disponibile" su STDERR: con
  # $ErrorActionPreference='Stop' un semplice `eas whoami 2>&1` verrebbe
  # trattato come errore terminante. Isoliamo il comando abbassando
  # temporaneamente ErrorActionPreference e catturando stdout+stderr.
  $who = ''
  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $who = (cmd /c 'eas whoami 2>&1') | Out-String
  } catch {
    $who = ''
  } finally {
    $ErrorActionPreference = $prevEap
  }
  # Lo username e' una riga tipo "andrea@prestia.dev". Escludiamo la riga di
  # warning "eas-cli@X is now available" che pure contiene caratteri strani.
  $userLine = ($who -split "`n") |
    Where-Object { $_ -match '@' -and $_ -notmatch 'eas-cli' -and $_ -notmatch 'available' } |
    Select-Object -First 1
  if ($who -match 'Not logged in' -or -not $userLine) {
    Fail "Non sei loggato in EAS (o output non riconosciuto). Esegui: eas login"
  }
  Write-Ok ("Loggato (" + $userLine.Trim() + ")")

  # --- 3. profilo production in eas.json -------------------------------------
  Write-Step 'Controllo profilo production in eas.json'
  $easJsonPath = Join-Path $MobileRoot 'eas.json'
  if (-not (Test-Path $easJsonPath)) { Fail "eas.json non trovato in $MobileRoot" }
  $easJson = Get-Content $easJsonPath -Raw | ConvertFrom-Json

  $prod = $easJson.build.production
  if (-not $prod) { Fail "Profilo build.production assente in eas.json" }

  # simulator NON deve essere true (build per dispositivo reale)
  $simulator = $false
  if ($prod.PSObject.Properties.Name -contains 'ios' -and
      $prod.ios.PSObject.Properties.Name -contains 'simulator') {
    $simulator = [bool]$prod.ios.simulator
  }
  if ($simulator) { Fail "build.production.ios.simulator = true: non pubblicabile. Rimuovilo." }
  Write-Ok 'production.ios.simulator non attivo (build per dispositivo reale)'

  $autoInc = $false
  if ($prod.PSObject.Properties.Name -contains 'autoIncrement') { $autoInc = [bool]$prod.autoIncrement }
  if ($autoInc) { Write-Ok 'autoIncrement attivo (build number gestito da EAS)' }
  else { Write-Warn2 'autoIncrement non attivo: assicurati di incrementare il build number manualmente' }

  # profilo submit (serve solo se -Submit)
  $submitCfg = $null
  if ($easJson.PSObject.Properties.Name -contains 'submit' -and
      $easJson.submit.PSObject.Properties.Name -contains 'production') {
    $submitCfg = $easJson.submit.production
  }
  if ($Submit) {
    if (-not $submitCfg -or -not $submitCfg.ios) {
      Fail "-Submit richiesto ma submit.production.ios assente in eas.json"
    }
    Write-Ok ("submit.production.ios pronto (ascAppId " + $submitCfg.ios.ascAppId + ")")
  }

  # --- 4. git working tree pulito --------------------------------------------
  if (-not $SkipGitCheck) {
    Write-Step 'Controllo git working tree'
    Push-Location $RepoRoot
    try {
      $dirty = (& git status --porcelain 2>&1 | Out-String).Trim()
    } finally { Pop-Location }
    if ($dirty) {
      Write-Warn2 'Working tree NON pulito. EAS builda dal commit archiviato: le modifiche non committate potrebbero NON entrare nella build.'
      Write-Host $dirty
      $ans = Read-Host 'Continuare comunque? (s/N)'
      if ($ans -notmatch '^[sSyY]') { Fail 'Interrotto: committa o stasha le modifiche.' }
    } else {
      Write-Ok 'Working tree pulito'
    }
  } else {
    Write-Warn2 'Controllo git saltato (-SkipGitCheck)'
  }

  # --- 5. riepilogo -----------------------------------------------------------
  Write-Step 'Riepilogo release'
  # version da app.config.ts (grep semplice, non eseguiamo TS)
  $appConfig = Get-Content (Join-Path $MobileRoot 'app.config.ts') -Raw
  $version = if ($appConfig -match "version:\s*'([^']+)'") { $Matches[1] } else { '?' }
  $env:APP_VARIANT = 'production'
  $bundleId = 'app.accanto.mobile'  # production: nessun suffisso (vedi app.config.ts)

  Write-Host ""
  Write-Host "  Piattaforma : iOS" -ForegroundColor White
  Write-Host "  Profilo     : production" -ForegroundColor White
  Write-Host "  Bundle ID   : $bundleId" -ForegroundColor White
  Write-Host "  Versione    : $version (build number auto-incrementato)" -ForegroundColor White
  Write-Host "  API         : $($prod.env.EXPO_PUBLIC_API_BASE_URL)" -ForegroundColor White
  if ($Submit) {
    Write-Host "  Submit      : SI (--auto-submit -> App Store Connect)" -ForegroundColor White
    Write-Host "  Apple ID    : $($submitCfg.ios.appleId)" -ForegroundColor White
    Write-Host "  ASC App ID  : $($submitCfg.ios.ascAppId)" -ForegroundColor White
    Write-Host "  Team ID     : $($submitCfg.ios.appleTeamId)" -ForegroundColor White
  } else {
    Write-Host "  Submit      : NO (solo build; usa poi: eas submit -p ios --profile production)" -ForegroundColor White
  }
  Write-Host ""

  # --- 6. costruisci ed esegui il comando ------------------------------------
  $easArgs = @('build', '--platform', 'ios', '--profile', 'production')
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
  Write-Host "  La build gira sui server EAS (~20-40 min). Puoi chiudere: monitori su expo.dev." -ForegroundColor DarkGray
  $go = Read-Host 'Procedere? (s/N)'
  if ($go -notmatch '^[sSyY]') { Fail 'Interrotto dall utente.' }

  Write-Step "Avvio: $cmdPreview"
  & eas @easArgs
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { Fail "eas build ha restituito exit code $exit" }
  Write-Ok 'Comando eas completato. Controlla lo stato su https://expo.dev'
}
finally {
  Pop-Location
}
