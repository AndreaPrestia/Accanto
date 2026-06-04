#requires -Version 5.1
<#
.SYNOPSIS
    Backup cifrato del database Accanto (Postgres) usando pg_dump + AES-256.

.DESCRIPTION
    Esegue un dump in formato custom (`pg_dump -Fc`, compresso) tramite il
    container `accanto-db-1` e lo cifra con `openssl enc -aes-256-cbc
    -pbkdf2 -iter 600000 -salt` (via container `alpine/openssl`, nessuna
    dipendenza host). L'output e' un singolo file
    `backups/accanto-YYYYMMDD-HHMMSS.dump.enc` con relativo `.sha256`.

    Passphrase letta da `$env:BACKUP_PASSPHRASE` (REQUIRED). Conservala in
    un password manager separato dal repo; senza di essa il backup non
    e' recuperabile (e' il punto, deve sopravvivere a furto del file ma
    non essere indistruttibile dal team).

.PARAMETER OutputDir
    Cartella dove scrivere il backup cifrato. Default: ./backups (creata
    se non esiste, gia' in .gitignore tramite pattern *.dump.enc).

.PARAMETER DbContainer
    Nome del container Postgres. Default: accanto-db-1.

.PARAMETER DbUser
    Ruolo Postgres usato per il dump. Default: accanto (owner, ha SELECT
    su tutto incluse tabelle audit append-only).

.PARAMETER DbName
    Database target. Default: accanto.

.EXAMPLE
    $env:BACKUP_PASSPHRASE = (Read-Host -AsSecureString | ConvertFrom-SecureString -AsPlainText)
    ./scripts/db/backup.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDir   = "./backups",
    [string] $DbContainer = "accanto-db-1",
    [string] $DbUser      = "accanto",
    [string] $DbName      = "accanto"
)

$ErrorActionPreference = "Stop"

if (-not $env:BACKUP_PASSPHRASE) {
    throw "BACKUP_PASSPHRASE non impostata. Esporta la passphrase prima di lanciare lo script (es. \$env:BACKUP_PASSPHRASE = '...'). Lunghezza consigliata: 32+ char random."
}
if ($env:BACKUP_PASSPHRASE.Length -lt 20) {
    Write-Warning "Passphrase corta (<20 char). Usa qualcosa di robusto, es. 'openssl rand -base64 32'."
}

# Verifica che il container DB esista e sia healthy.
$state = docker inspect -f '{{.State.Status}}' $DbContainer 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Container '$DbContainer' non trovato. Avvia lo stack: docker compose up -d db"
}
if ($state -ne "running") {
    throw "Container '$DbContainer' non e' running (stato: $state)."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$timestamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
$baseName  = "accanto-$timestamp"
$rawPath   = Join-Path $OutputDir "$baseName.dump"
$encPath   = Join-Path $OutputDir "$baseName.dump.enc"
$shaPath   = Join-Path $OutputDir "$baseName.dump.enc.sha256"

Write-Host "[backup] pg_dump -Fc -Z 6 -U $DbUser $DbName ..." -ForegroundColor Cyan
# Dump in file dentro al container (evita streaming binario via PowerShell pipe).
docker exec $DbContainer pg_dump -U $DbUser -d $DbName -Fc -Z 6 -f "/tmp/$baseName.dump"
if ($LASTEXITCODE -ne 0) { throw "pg_dump fallito (exit $LASTEXITCODE)." }

Write-Host "[backup] copia su host ..." -ForegroundColor Cyan
docker cp "${DbContainer}:/tmp/$baseName.dump" $rawPath
if ($LASTEXITCODE -ne 0) { throw "docker cp fallito." }
docker exec $DbContainer rm -f "/tmp/$baseName.dump" | Out-Null

$rawBytes = (Get-Item $rawPath).Length
Write-Host "[backup] dump grezzo: $([math]::Round($rawBytes/1MB,2)) MB" -ForegroundColor Cyan

Write-Host "[backup] openssl enc -aes-256-cbc -pbkdf2 -iter 600000 ..." -ForegroundColor Cyan
# Pre-pull idempotente: evita che lo stderr 'Unable to find image locally' al
# primo run inneschi il throw via $ErrorActionPreference='Stop'.
docker image inspect alpine/openssl >$null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[backup] pull alpine/openssl ..." -ForegroundColor Cyan
    docker pull --quiet alpine/openssl | Out-Null
}
# Cifra via container alpine/openssl. Passphrase passata come env, mai sulla
# command line (sarebbe visibile in `ps`/`docker inspect`).
$absDir = (Resolve-Path $OutputDir).Path
docker run --rm -i `
    -e PASS=$env:BACKUP_PASSPHRASE `
    -v "${absDir}:/work" `
    alpine/openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -salt `
        -pass env:PASS `
        -in  "/work/$baseName.dump" `
        -out "/work/$baseName.dump.enc"
if ($LASTEXITCODE -ne 0) {
    Remove-Item -Force $rawPath -ErrorAction SilentlyContinue
    throw "openssl enc fallito (exit $LASTEXITCODE)."
}

# Cancella il dump in chiaro IMMEDIATAMENTE dopo la cifratura.
Remove-Item -Force $rawPath

$encBytes = (Get-Item $encPath).Length
$hash = (Get-FileHash -Algorithm SHA256 $encPath).Hash.ToLowerInvariant()
"$hash  $baseName.dump.enc" | Set-Content -Encoding ascii $shaPath

Write-Host ""
Write-Host "[backup] OK" -ForegroundColor Green
Write-Host "  file   : $encPath"
Write-Host "  size   : $([math]::Round($encBytes/1MB,2)) MB"
Write-Host "  sha256 : $hash"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Carica il file .dump.enc e .sha256 su storage offsite (S3/B2/rclone)."
Write-Host "  2. Verifica restore: ./scripts/db/restore-drill.ps1 -BackupFile '$encPath'"
Write-Host "  3. Conserva BACKUP_PASSPHRASE in un password manager separato dal backup."
