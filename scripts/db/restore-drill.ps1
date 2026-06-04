#requires -Version 5.1
<#
.SYNOPSIS
    Restore drill: verifica che un backup cifrato sia ripristinabile e
    contenga dati sani, su un Postgres effimero isolato.

.DESCRIPTION
    1. Decifra il backup .dump.enc usando $env:BACKUP_PASSPHRASE.
    2. Spinge un container Postgres 16-alpine temporaneo (porta 55432,
       volume tmpfs, password random) — NON tocca il DB di produzione/dev.
    3. Esegue pg_restore sul DB temporaneo.
    4. Lancia sanity check SQL:
       - tabelle attese esistono;
       - row count > 0 sulle tabelle critiche (configurabile);
       - foreign key constraints non hanno violazioni orfane evidenti;
       - schema_migrations history coerente.
    5. Tear down del container temporaneo.
    6. Stampa report PASS/FAIL con exit code != 0 in caso di failure.

    Pensato per essere lanciato:
    - Manualmente dopo ogni cambio infrastrutturale grosso.
    - Schedulato MENSILMENTE (cron/Task Scheduler) sull'ultimo backup.
    - In CI come smoke test prima del cutover di restore reale.

.PARAMETER BackupFile
    Path al file .dump.enc da testare. Default: il piu' recente in ./backups.

.PARAMETER TempPort
    Porta host per il container Postgres temporaneo. Default: 55432.

.PARAMETER MinUsers
    Soglia minima di righe nella tabella `users` per considerare il dump
    "sano". Default: 1 (alza in prod dopo aver visto il baseline reale).

.EXAMPLE
    $env:BACKUP_PASSPHRASE = '...'
    ./scripts/db/restore-drill.ps1

.EXAMPLE
    ./scripts/db/restore-drill.ps1 -BackupFile ./backups/accanto-20260604-093000.dump.enc -MinUsers 10
#>
[CmdletBinding()]
param(
    [string] $BackupFile,
    [int]    $TempPort = 55432,
    [int]    $MinUsers = 1
)

$ErrorActionPreference = "Stop"

if (-not $env:BACKUP_PASSPHRASE) {
    throw "BACKUP_PASSPHRASE non impostata."
}

if (-not $BackupFile) {
    $latest = Get-ChildItem ./backups -Filter "accanto-*.dump.enc" -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latest) { throw "Nessun backup trovato in ./backups. Lancia prima backup.ps1." }
    $BackupFile = $latest.FullName
    Write-Host "[drill] backup auto-selezionato: $BackupFile" -ForegroundColor Cyan
}
if (-not (Test-Path $BackupFile)) { throw "File non trovato: $BackupFile" }

# Verifica integrita' SHA256 se c'e' il sidecar .sha256.
$shaSidecar = "$BackupFile.sha256"
if (Test-Path $shaSidecar) {
    $expected = (Get-Content $shaSidecar -Raw).Split(' ')[0].Trim().ToLowerInvariant()
    $actual   = (Get-FileHash -Algorithm SHA256 $BackupFile).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        throw "SHA256 mismatch! file corrotto o manomesso.`n  expected: $expected`n  actual  : $actual"
    }
    Write-Host "[drill] SHA256 OK ($actual)" -ForegroundColor Green
} else {
    Write-Warning "Nessun .sha256 sidecar accanto al backup: integrita' non verificabile."
}

$containerName = "accanto-restore-drill-$([guid]::NewGuid().ToString('N').Substring(0,8))"
$tempPw = [guid]::NewGuid().ToString('N')
$backupDir = (Resolve-Path (Split-Path -Parent $BackupFile)).Path
$backupName = Split-Path -Leaf $BackupFile
$decryptedName = $backupName -replace '\.enc$',''

$exitCode = 1
try {
    Write-Host "[drill] avvio Postgres effimero su porta $TempPort (container: $containerName)" -ForegroundColor Cyan
    # tmpfs su /var/lib/postgresql/data → tutto in RAM, niente residui su disco
    # dopo il tear-down. fsync=off + full_page_writes=off per restore veloce.
    docker run -d `
        --name $containerName `
        -e POSTGRES_PASSWORD=$tempPw `
        -e POSTGRES_DB=accanto `
        -p "${TempPort}:5432" `
        --tmpfs /var/lib/postgresql/data:rw,size=2g `
        postgres:16-alpine `
        -c fsync=off -c full_page_writes=off -c synchronous_commit=off | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker run del container temporaneo fallito." }

    # Attesa healthy (max 60s).
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 1
        $r = docker exec -e PGPASSWORD=$tempPw $containerName pg_isready -U postgres -d accanto 2>$null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    }
    if (-not $ready) { throw "Postgres temporaneo non e' diventato healthy entro 60s." }
    Write-Host "[drill] Postgres temporaneo healthy" -ForegroundColor Green

    Write-Host "[drill] decifratura backup ..." -ForegroundColor Cyan
    docker image inspect alpine/openssl >$null 2>&1
    if ($LASTEXITCODE -ne 0) {
        docker pull --quiet alpine/openssl | Out-Null
    }
    docker run --rm -i `
        -e PASS=$env:BACKUP_PASSPHRASE `
        -v "${backupDir}:/work" `
        alpine/openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -d `
            -pass env:PASS `
            -in  "/work/$backupName" `
            -out "/work/$decryptedName"
    if ($LASTEXITCODE -ne 0) { throw "Decifratura fallita (passphrase errata?)." }

    try {
        $decryptedPath = Join-Path $backupDir $decryptedName
        Write-Host "[drill] pg_restore sul DB temporaneo ..." -ForegroundColor Cyan
        # Copia nel container e restore. -O e -x evitano errori di ownership/grants
        # su un DB neonato che non ha gli stessi ruoli del prod.
        docker cp $decryptedPath "${containerName}:/tmp/dump"
        docker exec -e PGPASSWORD=$tempPw $containerName `
            pg_restore -U postgres -d accanto --no-owner --no-privileges --exit-on-error /tmp/dump
        if ($LASTEXITCODE -ne 0) { throw "pg_restore fallito (exit $LASTEXITCODE)." }
        Write-Host "[drill] restore OK" -ForegroundColor Green
    }
    finally {
        Remove-Item -Force (Join-Path $backupDir $decryptedName) -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "[drill] === SANITY CHECKS ===" -ForegroundColor Yellow

    # Helper: esegue una query SQL e ritorna il valore scalare.
    # SQL via STDIN per evitare il mangling dei doppi apici fatto da PowerShell
    # quando passa argomenti a docker exec (Postgres lower-case-erebbe gli
    # identifier "PascalCase" come "__EFMigrationsHistory" se non quotati).
    function Invoke-Psql([string]$sql) {
        $out = $sql | docker exec -i -e PGPASSWORD=$tempPw $containerName `
            psql -U postgres -d accanto -tA 2>&1
        if ($LASTEXITCODE -ne 0) { throw "psql failed: $out" }
        return ($out | Out-String).Trim()
    }

    $checks = @()

    # Check 1: tabelle critiche esistono.
    $expectedTables = @(
        'users','care_circles','care_circle_members','timeline_entries',
        'medical_documents','audit_log_entries','security_audit_log_entries',
        'refresh_tokens'
    )
    foreach ($t in $expectedTables) {
        $exists = Invoke-Psql "SELECT to_regclass('public.$t') IS NOT NULL;"
        $checks += [pscustomobject]@{
            Check  = "Tabella $t esiste"
            Result = $exists
            Pass   = ($exists -eq 't')
        }
    }

    # Check 2: row count tabelle chiave.
    $countChecks = @{
        'users'                    = $MinUsers
        'care_circles'             = 0   # puo' essere 0 in dev appena seedato
        '__EFMigrationsHistory'    = 1   # almeno una migration applicata
    }
    foreach ($t in $countChecks.Keys) {
        $count = [int](Invoke-Psql "SELECT COUNT(*) FROM `"$t`";")
        $min = $countChecks[$t]
        $checks += [pscustomobject]@{
            Check  = "$t ha >= $min righe"
            Result = "$count"
            Pass   = ($count -ge $min)
        }
    }

    # Check 3: ultima migration applicata (segnale che lo schema e' coerente).
    $lastMig = Invoke-Psql "SELECT `"MigrationId`" FROM `"__EFMigrationsHistory`" ORDER BY `"MigrationId`" DESC LIMIT 1;"
    $checks += [pscustomobject]@{
        Check  = "Ultima migration leggibile"
        Result = $lastMig
        Pass   = (-not [string]::IsNullOrWhiteSpace($lastMig))
    }

    # Check 4: nessuna FK orfana tra care_circle_members e care_circles.
    # Tabelle snake_case, colonne PascalCase quoted (convenzione naming EF).
    $orphans = [int](Invoke-Psql @"
SELECT COUNT(*) FROM care_circle_members m
LEFT JOIN care_circles c ON c."Id" = m."CareCircleId"
WHERE c."Id" IS NULL;
"@)
    $checks += [pscustomobject]@{
        Check  = "Nessun care_circle_members orfano"
        Result = "$orphans orfani"
        Pass   = ($orphans -eq 0)
    }

    # Stampa tabella riassuntiva.
    $checks | Format-Table -AutoSize

    $failed = @($checks | Where-Object { -not $_.Pass })
    if ($failed.Count -eq 0) {
        Write-Host "[drill] TUTTI I CHECK PASSATI ($($checks.Count)/$($checks.Count))" -ForegroundColor Green
        $exitCode = 0
    } else {
        Write-Host "[drill] $($failed.Count) CHECK FALLITI su $($checks.Count)" -ForegroundColor Red
        $exitCode = 2
    }
}
finally {
    Write-Host ""
    Write-Host "[drill] tear-down container $containerName ..." -ForegroundColor Cyan
    docker rm -f $containerName 2>$null | Out-Null
}

# Dead-man's switch: ping verso Healthchecks.io se il drill ha PASSATO.
# Drill fallito -> nessun ping -> alert dopo la grace window.
if ($exitCode -eq 0 -and $env:HEARTBEAT_RESTORE_URL) {
    try {
        Invoke-WebRequest -Uri $env:HEARTBEAT_RESTORE_URL -Method Post `
            -Body "checks=$($checks.Count) passed" -TimeoutSec 10 -UseBasicParsing | Out-Null
        Write-Host "[drill] heartbeat inviato"
    }
    catch {
        Write-Warning "Heartbeat fallito (drill OK, dead-man's switch NON pingato): $($_.Exception.Message)"
    }
}

exit $exitCode
