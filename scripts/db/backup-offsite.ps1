#requires -Version 5.1
<#
.SYNOPSIS
    Upload offsite dei backup .dump.enc verso uno storage S3-compatibile
    (IONOS S3, AWS S3, Backblaze B2, MinIO, ecc.) usando aws-cli via
    container.

.DESCRIPTION
    Carica TUTTI i file `backups/accanto-*.dump.enc` e relativi `.sha256`
    sul bucket configurato. Idempotente: usa `aws s3 cp` con check di
    esistenza preventivo (skip se gia' presente con la stessa size).

    Non installa nulla sull'host — usa `amazon/aws-cli` via docker.

    Credenziali e endpoint via variabili d'ambiente (vedi
    `.env.backup-offsite.example`):

      S3_ENDPOINT_URL       (opzionale per AWS, RICHIESTO per IONOS/B2/MinIO)
      S3_BUCKET             (es. "accanto-backups")
      S3_REGION             (es. "de" per IONOS Francoforte, "us-east-1" per AWS)
      S3_PREFIX             (opzionale, default vuoto; es. "backups/daily/")
      AWS_ACCESS_KEY_ID
      AWS_SECRET_ACCESS_KEY

    Object Lock (anti-ransomware / anti-insider, defense-in-depth oltre
    alla cifratura AES applicativa):

      S3_OBJECT_LOCK_ENABLED  ("true" / "false", default "true")
      S3_OBJECT_LOCK_MODE     ("GOVERNANCE" / "COMPLIANCE", default
                              "GOVERNANCE"). Governance ammette override
                              da admin del contratto IONOS in caso di
                              GDPR erasure obbligatorio. Compliance e'
                              irreversibile fino a scadenza.
      S3_OBJECT_LOCK_DAYS     numero di giorni di retention. Default 2555
                              (7 anni, allineato a retention sanitaria).

    Object Lock va settato AL PUT — non puo' essere aggiunto a posteriori.
    Il bucket deve avere Object Lock enabled e Versioning ON.

.PARAMETER InputDir
    Cartella sorgente con i backup. Default: ./backups.

.PARAMETER EnvFile
    File env da caricare prima dell'upload. Default: .env.backup-offsite
    (gia' in .gitignore — NON committarlo).

.EXAMPLE
    # Una volta sola: copia il template e compila le credenziali.
    Copy-Item .env.backup-offsite.example .env.backup-offsite
    # ... edit ...
    ./scripts/db/backup-offsite.ps1

.NOTES
    Pre-requisiti sul bucket (configurati una volta nella console del
    provider, NON da questo script — sono permission-sensitive):
      - Versioning ON
      - Object lock enabled (la retention per-object la setta questo
        script al PUT — vedi S3_OBJECT_LOCK_* sopra)
      - Server-side encryption ON (AES-256 o KMS)
      - Block public access ON
      - Access key dedicata: PutObject/GetObject/ListBucket ONLY, NO
        DeleteObject (la chiave non puo' cancellare a prescindere; il
        lock impedisce delete anche all'admin entro la retention)

    Vedi accanto-ops/backup-restore.md sezione "Storage offsite (IONOS S3)".
#>
[CmdletBinding()]
param(
    [string] $InputDir = "./backups",
    [string] $EnvFile  = ".env.backup-offsite"
)

$ErrorActionPreference = "Stop"

# Carica .env.backup-offsite se esiste (sovrascrivibile da env gia'
# presenti in PowerShell — utile in CI dove si passano via secret).
if (Test-Path $EnvFile) {
    Write-Host "[offsite] carico $EnvFile" -ForegroundColor Cyan
    Get-Content $EnvFile | Where-Object { $_ -match '^\s*[A-Z_]+\s*=' -and $_ -notmatch '^\s*#' } | ForEach-Object {
        $kv = $_ -split '=', 2
        $k = $kv[0].Trim(); $v = $kv[1].Trim().Trim('"').Trim("'")
        # Non sovrascrivere se gia' definito nell'env del processo.
        if (-not (Get-Item "env:$k" -ErrorAction SilentlyContinue)) {
            Set-Item "env:$k" $v
        }
    }
}

foreach ($req in @('S3_BUCKET', 'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY')) {
    if (-not (Get-Item "env:$req" -ErrorAction SilentlyContinue)) {
        throw "$req non impostata. Copia $EnvFile.example e compila."
    }
}
$bucket   = $env:S3_BUCKET
$region   = if ($env:S3_REGION) { $env:S3_REGION } else { "us-east-1" }
$prefix   = if ($env:S3_PREFIX) { $env:S3_PREFIX.TrimEnd('/') + '/' } else { '' }
$endpoint = $env:S3_ENDPOINT_URL  # vuoto => AWS S3 default

# Object Lock: opt-out esplicito con S3_OBJECT_LOCK_ENABLED=false.
# Default ON: il bucket deve avere ObjectLockEnabled=Enabled.
$lockEnabled = $true
if ($env:S3_OBJECT_LOCK_ENABLED -and $env:S3_OBJECT_LOCK_ENABLED.ToLower() -eq 'false') {
    $lockEnabled = $false
}
$lockMode = if ($env:S3_OBJECT_LOCK_MODE) { $env:S3_OBJECT_LOCK_MODE.ToUpper() } else { 'GOVERNANCE' }
if ($lockMode -notin @('GOVERNANCE', 'COMPLIANCE')) {
    throw "S3_OBJECT_LOCK_MODE deve essere GOVERNANCE o COMPLIANCE (valore: $lockMode)."
}
$lockDays = 2555  # 7 anni
if ($env:S3_OBJECT_LOCK_DAYS) {
    if (-not [int]::TryParse($env:S3_OBJECT_LOCK_DAYS, [ref]$lockDays) -or $lockDays -le 0) {
        throw "S3_OBJECT_LOCK_DAYS deve essere intero positivo (valore: $($env:S3_OBJECT_LOCK_DAYS))."
    }
}
# Data UTC ISO 8601 (formato richiesto da aws-cli per --object-lock-retain-until-date).
$retainUntil = (Get-Date).ToUniversalTime().AddDays($lockDays).ToString("yyyy-MM-ddTHH:mm:ssZ")

if (-not (Test-Path $InputDir)) { throw "Cartella $InputDir non trovata." }
$files = Get-ChildItem $InputDir -Include "accanto-*.dump.enc", "accanto-*.dump.enc.sha256" -File
if (-not $files) {
    Write-Host "[offsite] nessun backup in $InputDir, nulla da caricare." -ForegroundColor Yellow
    return
}

# Pre-pull idempotente.
docker image inspect amazon/aws-cli >$null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[offsite] pull amazon/aws-cli ..." -ForegroundColor Cyan
    docker pull --quiet amazon/aws-cli | Out-Null
}

$absDir = (Resolve-Path $InputDir).Path
$awsArgs = @()
if ($endpoint) { $awsArgs += @('--endpoint-url', $endpoint) }
$awsArgs += @('--region', $region)

function Invoke-Aws {
    param([string[]] $Cmd)
    # Credenziali via env (mai sulla command line — visibili in `ps`).
    docker run --rm `
        -e AWS_ACCESS_KEY_ID=$env:AWS_ACCESS_KEY_ID `
        -e AWS_SECRET_ACCESS_KEY=$env:AWS_SECRET_ACCESS_KEY `
        -v "${absDir}:/work" `
        amazon/aws-cli @awsArgs @Cmd
}

$uploaded = 0; $skipped = 0
if ($lockEnabled) {
    Write-Host "[offsite] Object Lock: $lockMode, retention $lockDays gg (fino al $retainUntil)" -ForegroundColor Cyan
} else {
    Write-Host "[offsite] Object Lock: DISABILITATO (S3_OBJECT_LOCK_ENABLED=false)" -ForegroundColor Yellow
}
foreach ($f in $files | Sort-Object Name) {
    $key = "$prefix$($f.Name)"
    # Check esistenza: head-object esce 0 se esiste, 254/255 altrimenti.
    & {
        Invoke-Aws @('s3api', 'head-object', '--bucket', $bucket, '--key', $key)
    } *>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[offsite] skip (gia' presente) s3://$bucket/$key" -ForegroundColor DarkGray
        $skipped++
        continue
    }
    Write-Host "[offsite] upload s3://$bucket/$key" -ForegroundColor Cyan
    $cpArgs = @('s3', 'cp', "/work/$($f.Name)", "s3://$bucket/$key")
    if ($lockEnabled) {
        # Object Lock va settato AL PUT, non e' modificabile dopo (a
        # parte estensione retention con bypass governance dall'admin).
        $cpArgs += @(
            '--object-lock-mode', $lockMode,
            '--object-lock-retain-until-date', $retainUntil
        )
    }
    Invoke-Aws $cpArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "upload fallito per $($f.Name)." }
    $uploaded++
}

Write-Host ""
Write-Host "[offsite] OK" -ForegroundColor Green
Write-Host "  bucket   : s3://$bucket/$prefix"
Write-Host "  endpoint : $(if ($endpoint) { $endpoint } else { 'AWS default' })"
Write-Host "  uploaded : $uploaded"
Write-Host "  skipped  : $skipped (gia' presenti)"
if ($lockEnabled) {
    Write-Host "  lock     : $lockMode fino al $retainUntil"
}
