#requires -Version 5.1
<#
.SYNOPSIS
    Smoke test end-to-end della replica S3 dei documenti
    (`IS3DocumentReplica` → IONOS / AWS / MinIO).

.DESCRIPTION
    Invoca `accanto smoke-s3` (CLI). Il comando:
      1. Crea un file di prova in `Storage:RootPath/_smoke/`.
      2. Fa 2 PUT (per generare 2 versioni sul bucket versionato).
      3. Verifica via `ListVersionsAsync` che ce ne siano >= 2.
      4. Chiama `DeleteAllVersionsAsync`.
      5. Verifica che la lista versioni sia ora vuota.
      6. Pulisce il file locale.

    Da lanciare PRIMA di attivare `S3DocumentReplica:Enabled = true` in
    produzione: convalida credenziali, endpoint, bucket, versioning, e
    che le delete cascade GDPR rimuovano effettivamente tutte le versioni.

.PARAMETER EnvFile
    File .env da caricare. Default: .env.s3-replica.
    Formato (sintassi shell-like, ignorato se variabili gia' presenti
    nell'env del processo):

      S3DocumentReplica__Enabled=true
      S3DocumentReplica__ServiceUrl=https://s3-eu-central-1.ionoscloud.com
      S3DocumentReplica__Region=de
      S3DocumentReplica__Bucket=accanto-docs
      S3DocumentReplica__Prefix=documents/
      S3DocumentReplica__AccessKeyId=...
      S3DocumentReplica__SecretAccessKey=...

    Il file e' in .gitignore (NON committarlo). Le credenziali devono
    avere PutObject + GetObject + ListBucket + DeleteObject (la replica
    cancella per GDPR le proprie versioni — il bucket `accanto-docs`
    NON deve avere Object Lock, ed e' separato da `accanto-backups`
    proprio per ridurre blast radius della key).

.EXAMPLE
    # Prima volta:
    Copy-Item scripts/smoke/.env.s3-replica.example .env.s3-replica
    # ... edit con credenziali reali ...
    ./scripts/smoke/s3-replica-smoke.ps1
    # Exit 0 = PASS, 2 = FAIL.

.NOTES
    Non tocca il DB. Il CLI risolve solo le dipendenze S3 + storage
    locale, non apre transazioni Postgres.
#>
[CmdletBinding()]
param(
    [string] $EnvFile = ".env.s3-replica"
)

$ErrorActionPreference = "Stop"

# 1) Carica .env.s3-replica se esiste (variabili gia' presenti vincono).
if (Test-Path $EnvFile) {
    Write-Host "[smoke-s3] carico $EnvFile" -ForegroundColor Cyan
    Get-Content $EnvFile | Where-Object { $_ -match '^\s*[A-Za-z_][A-Za-z0-9_]*\s*=' -and $_ -notmatch '^\s*#' } | ForEach-Object {
        $kv = $_ -split '=', 2
        $k = $kv[0].Trim(); $v = $kv[1].Trim().Trim('"').Trim("'")
        if (-not (Get-Item "env:$k" -ErrorAction SilentlyContinue)) {
            Set-Item "env:$k" $v
        }
    }
}

# 2) Pre-flight: requisiti minimi prima di chiamare il CLI (errore piu' chiaro).
$missing = @()
foreach ($req in @('S3DocumentReplica__Bucket', 'S3DocumentReplica__AccessKeyId', 'S3DocumentReplica__SecretAccessKey')) {
    if (-not (Get-Item "env:$req" -ErrorAction SilentlyContinue)) {
        $missing += $req
    }
}
if ($missing.Count -gt 0) {
    Write-Error "Variabili mancanti: $($missing -join ', '). Setta $EnvFile o esportale nell'ambiente."
    exit 2
}

# Forza Enabled=true per la durata di questo smoke (anche se appsettings.json
# lo lascia a false in dev).
$env:S3DocumentReplica__Enabled = "true"

# 3) Lancia il CLI smoke-s3.
$cliProject = Join-Path $PSScriptRoot "..\..\backend\src\Accanto.Cli\Accanto.Cli.csproj"
$cliProject = (Resolve-Path $cliProject).Path

Write-Host "[smoke-s3] CLI: $cliProject" -ForegroundColor Cyan
Write-Host "[smoke-s3] bucket: $env:S3DocumentReplica__Bucket  prefix: $env:S3DocumentReplica__Prefix" -ForegroundColor Cyan

& dotnet run --project $cliProject -c Release --no-build -- smoke-s3
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Host "[smoke-s3] --no-build ha fallito, riprovo con build..." -ForegroundColor Yellow
    & dotnet run --project $cliProject -c Release -- smoke-s3
    $code = $LASTEXITCODE
}

if ($code -eq 0) {
    Write-Host "[smoke-s3] PASS" -ForegroundColor Green
} else {
    Write-Host "[smoke-s3] FAIL (exit $code)" -ForegroundColor Red
}
exit $code
