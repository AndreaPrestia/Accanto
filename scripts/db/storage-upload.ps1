#requires -Version 5.1
<#
.SYNOPSIS
    Sync incrementale del bind mount `./storage` (PDF medicali, immagini
    upload-ate dagli utenti) verso storage S3-compatibile.

.DESCRIPTION
    Usa `aws s3 sync` (via container amazon/aws-cli) per copiare solo
    i file nuovi/modificati. Idempotente e veloce sui run successivi.

    **NON applica Object Lock**: i file in `./storage` sono medical
    documents soggetti al **diritto all'oblio GDPR (art. 17)**. Se un
    utente esercita il diritto di cancellazione, dobbiamo poter rimuovere
    anche la copia offsite — Object Lock lo impedirebbe. La protezione
    qui viene da:
      - SSE-S3 bucket encryption (cifratura at-rest lato provider)
      - Cifratura applicativa dei metadati nel DB (IFieldProtector)
      - Versioning ON sul bucket (recupero da delete accidentale)
      - Chiave IAM senza PutObjectAcl/PutBucketPolicy (no escalation)

    Per la cancellazione legittima (utente che chiede erasure) usa
    `aws s3 rm s3://<bucket>/storage/<path> --recursive` da uno script
    on-demand (NON automatizzato — richiede ticket + audit log).

    Credenziali e endpoint via `.env.backup-offsite` (stesse del backup
    DB — non duplicare).

.PARAMETER InputDir
    Cartella sorgente. Default: ./storage.

.PARAMETER EnvFile
    File env da caricare. Default: .env.backup-offsite.

.PARAMETER Delete
    Se passato, sincronizza anche le delete (rimuove dal bucket i file
    non piu' presenti localmente). Default: OFF (defense-in-depth, evita
    delete accidentali da bug applicativo).

.EXAMPLE
    ./scripts/db/storage-upload.ps1

.EXAMPLE
    # Con delete (solo quando sei sicuro che lo stato locale e' la
    # verita' canonica — es. dopo un restore completo).
    ./scripts/db/storage-upload.ps1 -Delete
#>
[CmdletBinding()]
param(
    [string] $InputDir = "./storage",
    [string] $EnvFile  = ".env.backup-offsite",
    [switch] $Delete
)

$ErrorActionPreference = "Stop"

if (Test-Path $EnvFile) {
    Write-Host "[storage-upload] carico $EnvFile" -ForegroundColor Cyan
    Get-Content $EnvFile | Where-Object { $_ -match '^\s*[A-Z_]+\s*=' -and $_ -notmatch '^\s*#' } | ForEach-Object {
        $kv = $_ -split '=', 2
        $k = $kv[0].Trim(); $v = $kv[1].Trim().Trim('"').Trim("'")
        if (-not (Get-Item "env:$k" -ErrorAction SilentlyContinue)) {
            Set-Item "env:$k" $v
        }
    }
}

foreach ($req in @('S3_BUCKET', 'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY')) {
    if (-not (Get-Item "env:$req" -ErrorAction SilentlyContinue)) {
        throw "$req non impostata. Configura $EnvFile."
    }
}
$bucket   = $env:S3_BUCKET
$region   = if ($env:S3_REGION) { $env:S3_REGION } else { "us-east-1" }
# Prefix dedicato a storage, NON quello dei backup (che e' backups/daily/).
$prefix   = "storage/"
$endpoint = $env:S3_ENDPOINT_URL

if (-not (Test-Path $InputDir)) {
    Write-Host "[storage-upload] cartella $InputDir non esiste, nulla da sincronizzare." -ForegroundColor Yellow
    return
}
$count = (Get-ChildItem $InputDir -Recurse -File -ErrorAction SilentlyContinue).Count
if ($count -eq 0) {
    Write-Host "[storage-upload] $InputDir vuota, nulla da sincronizzare." -ForegroundColor Yellow
    return
}

docker image inspect amazon/aws-cli >$null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[storage-upload] pull amazon/aws-cli ..." -ForegroundColor Cyan
    docker pull --quiet amazon/aws-cli | Out-Null
}

$absDir = (Resolve-Path $InputDir).Path
$awsArgs = @()
if ($endpoint) { $awsArgs += @('--endpoint-url', $endpoint) }
$awsArgs += @('--region', $region)

$syncArgs = @('s3', 'sync', '/work', "s3://$bucket/$prefix")
if ($Delete) {
    Write-Warning "[storage-upload] modalita' --delete attiva: i file rimossi localmente verranno cancellati anche su S3."
    $syncArgs += '--delete'
}

Write-Host "[storage-upload] sync $InputDir ($count file) -> s3://$bucket/$prefix" -ForegroundColor Cyan
docker run --rm `
    -e AWS_ACCESS_KEY_ID=$env:AWS_ACCESS_KEY_ID `
    -e AWS_SECRET_ACCESS_KEY=$env:AWS_SECRET_ACCESS_KEY `
    -v "${absDir}:/work:ro" `
    amazon/aws-cli @awsArgs @syncArgs
if ($LASTEXITCODE -ne 0) { throw "aws s3 sync fallito (exit $LASTEXITCODE)." }

Write-Host ""
Write-Host "[storage-upload] OK" -ForegroundColor Green
Write-Host "  bucket   : s3://$bucket/$prefix"
Write-Host "  endpoint : $(if ($endpoint) { $endpoint } else { 'AWS default' })"
Write-Host "  delete   : $($Delete.IsPresent)"
Write-Host "  lock     : DISABILITATO (storage GDPR-erasable)"

# Heartbeat opt-in (parallelo al backup DB).
if ($env:HEARTBEAT_STORAGE_URL) {
    try {
        Invoke-WebRequest -Uri $env:HEARTBEAT_STORAGE_URL -Method Post `
            -Body "files=$count" -TimeoutSec 10 -UseBasicParsing | Out-Null
        Write-Host "  ping     : heartbeat inviato"
    }
    catch {
        Write-Warning "Heartbeat fallito (sync OK, heartbeat NON pingato): $($_.Exception.Message)"
    }
}
