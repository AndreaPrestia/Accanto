#requires -Version 5.1
<#
.SYNOPSIS
    Snapshot forense pre-incident: cattura lo stato attuale (DB + audit
    + container + log) PRIMA di modificare qualunque cosa durante la
    risposta a un incidente.

.DESCRIPTION
    Quando si sospetta una compromissione, la prima cosa da fare NON e'
    "ruotare i segreti / restartare i container" — quello distrugge le
    prove (sessioni attive, log volatili, stato del container). Questo
    script produce un singolo bundle .tar.gz con:

    1. Dump completo cifrato del DB (riusa scripts/db/backup.ps1).
    2. Export CSV degli audit log (ultimi 30 giorni di default).
    3. Snapshot dei refresh_token attivi (chi e' loggato adesso).
    4. Summary utenti (id, email, ruoli, last login, locked) — utile
       per correlare attivita' sospetta.
    5. docker inspect di TUTTI i container del compose (immagini,
       digest, env mask, mount, network).
    6. docker images con digest (per riprodurre lo stato esatto in lab).
    7. Log degli ultimi N giorni di backend, db, caddy (se attivi).
    8. SHA-256 di .env (NON il contenuto — solo per dimostrare che non
       e' cambiato dopo lo snapshot).
    9. manifest.json con timestamp, git rev, hostname, lista file +
       sha256 di ognuno (chain of custody).

    Il bundle finale e' un .tar.gz + .sha256 sidecar. Il dump DB al
    suo interno e' gia' cifrato; i CSV NON lo sono (per ovvi motivi di
    analisi), quindi il bundle nel complesso CONTIENE PII e va trattato
    di conseguenza: spostarlo subito su storage controllato (S3 con
    object-lock + KMS, o vault offline) e annotare nel ticket di
    incident chi vi ha avuto accesso.

.PARAMETER OutputDir
    Cartella dove scrivere il bundle. Default: ./forensic (gia' in
    .gitignore).

.PARAMETER DbContainer
    Nome del container Postgres. Default: accanto-db-1.

.PARAMETER AuditDays
    Quanti giorni di audit log esportare. Default: 30.

.PARAMETER LogHours
    Quante ore di docker logs catturare per ogni container. Default: 72.

.PARAMETER IncidentId
    Identificativo libero (es. "INC-2026-06-04-leak-sospetto") incluso
    nel manifest e nel nome del bundle. Default: timestamp.

.EXAMPLE
    $env:BACKUP_PASSPHRASE = '...'
    ./scripts/security/forensic-snapshot.ps1 -IncidentId 'INC-2026-06-04'

.NOTES
    Dopo aver creato il bundle e averlo messo al sicuro, PROCEDI con:
    1. Rotazione segreti (accanto-ops/secret-rotation.md, sezione
       "Compromise scenario").
    2. Analisi log audit (audit_log_entries + security_audit_log_entries
       sono append-only — vedi security-audit.md item 17 e 20).
    3. Post-mortem entro 7 giorni.
#>
[CmdletBinding()]
param(
    [string] $OutputDir   = "./forensic",
    [string] $DbContainer = "accanto-db-1",
    [int]    $AuditDays   = 30,
    [int]    $LogHours    = 72,
    [string] $IncidentId
)

$ErrorActionPreference = "Stop"

if (-not $env:BACKUP_PASSPHRASE) {
    throw "BACKUP_PASSPHRASE non impostata. Esporta la passphrase prima di lanciare lo script."
}

$timestamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
if (-not $IncidentId) { $IncidentId = "snapshot-$timestamp" }
# Sanitize per filesystem.
$IncidentId = $IncidentId -replace '[^a-zA-Z0-9\-_]', '-'

$caseDir = Join-Path $OutputDir "$IncidentId-$timestamp"
New-Item -ItemType Directory -Force -Path $caseDir | Out-Null
Write-Host "[forensic] case directory: $caseDir" -ForegroundColor Cyan

# Helper: SQL query -> CSV in $caseDir. Usa il ruolo owner per leggere
# anche le tabelle audit (accanto_app non ha SELECT su tutto in alcuni
# setup; accanto si').
function Invoke-DbCsvExport {
    param([string] $Sql, [string] $OutFile)
    $ownerPw = $env:POSTGRES_PASSWORD
    if (-not $ownerPw) {
        $envLine = Get-Content .env -ErrorAction SilentlyContinue | Where-Object { $_ -match '^POSTGRES_PASSWORD=' } | Select-Object -First 1
        if ($envLine) { $ownerPw = ($envLine -split '=', 2)[1] }
    }
    if (-not $ownerPw) { throw "POSTGRES_PASSWORD non disponibile (ne' env ne' .env)." }

    $absOut = Join-Path $caseDir $OutFile
    docker exec -e PGPASSWORD=$ownerPw $DbContainer `
        psql -U accanto -d accanto -At -F ',' -P "footer=off" -c $Sql > $absOut
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "[forensic] export fallito per $OutFile (exit $LASTEXITCODE)."
    }
}

# 1. Dump completo cifrato del DB ----------------------------------------
Write-Host "[forensic] 1/9 dump DB cifrato (chiamo backup.ps1)..." -ForegroundColor Cyan
& "$PSScriptRoot/../db/backup.ps1" -OutputDir $caseDir -DbContainer $DbContainer | Out-Host
if ($LASTEXITCODE -ne 0) { throw "backup.ps1 fallito." }

# 2. Export audit log ---------------------------------------------------
Write-Host "[forensic] 2/9 export audit_log_entries ultimi $AuditDays gg..." -ForegroundColor Cyan
Invoke-DbCsvExport -OutFile "audit_log.csv" -Sql @"
COPY (
  SELECT * FROM audit_log_entries
  WHERE ""Timestamp"" >= now() - interval '$AuditDays days'
  ORDER BY ""Timestamp""
) TO STDOUT WITH CSV HEADER
"@

Write-Host "[forensic] 3/9 export security_audit_log_entries ultimi $AuditDays gg..." -ForegroundColor Cyan
Invoke-DbCsvExport -OutFile "security_audit_log.csv" -Sql @"
COPY (
  SELECT * FROM security_audit_log_entries
  WHERE ""Timestamp"" >= now() - interval '$AuditDays days'
  ORDER BY ""Timestamp""
) TO STDOUT WITH CSV HEADER
"@

# 4. Refresh token attivi ----------------------------------------------
Write-Host "[forensic] 4/9 refresh_tokens attivi..." -ForegroundColor Cyan
Invoke-DbCsvExport -OutFile "refresh_tokens_active.csv" -Sql @"
COPY (
  SELECT ""Id"", ""UserId"", ""CreatedAt"", ""ExpiresAt"", ""RevokedAt""
  FROM refresh_tokens
  WHERE ""RevokedAt"" IS NULL AND ""ExpiresAt"" > now()
  ORDER BY ""CreatedAt""
) TO STDOUT WITH CSV HEADER
"@

# 5. Summary utenti (senza password hash) ------------------------------
Write-Host "[forensic] 5/9 users summary..." -ForegroundColor Cyan
Invoke-DbCsvExport -OutFile "users_summary.csv" -Sql @"
COPY (
  SELECT ""Id"", ""Email"", ""DisplayName"", ""CreatedAt"",
         ""LastFailedLoginAt"", ""FailedLoginAttempts"", ""LockoutEndsAt"",
         ""TwoFactorEnabled""
  FROM users ORDER BY ""CreatedAt"" DESC NULLS LAST
) TO STDOUT WITH CSV HEADER
"@

# 6. docker inspect dei container del compose --------------------------
Write-Host "[forensic] 6/9 docker inspect..." -ForegroundColor Cyan
$containers = docker ps -a --filter "name=accanto-" --format "{{.Names}}"
if ($containers) {
    docker inspect $containers > (Join-Path $caseDir "containers.json")
}

# 7. docker images con digest ------------------------------------------
Write-Host "[forensic] 7/9 docker images..." -ForegroundColor Cyan
docker images --digests --format "{{json .}}" > (Join-Path $caseDir "images.jsonl")

# 8. Log dei container -------------------------------------------------
Write-Host "[forensic] 8/9 docker logs ultime $LogHours h..." -ForegroundColor Cyan
foreach ($c in $containers) {
    $logFile = Join-Path $caseDir "$c.log"
    # docker logs scrive su stdout+stderr separati; ridirigiamo entrambi.
    docker logs --since "${LogHours}h" $c *>&1 | Out-File -Encoding utf8 $logFile
}

# 9. SHA-256 di .env (NON il contenuto) --------------------------------
Write-Host "[forensic] 9/9 hash .env + manifest..." -ForegroundColor Cyan
if (Test-Path .env) {
    (Get-FileHash -Algorithm SHA256 .env).Hash.ToLowerInvariant() |
        Set-Content -Encoding ascii (Join-Path $caseDir "env.sha256")
}

# Manifest con chain of custody ----------------------------------------
$gitRev = (& git rev-parse HEAD 2>$null); if (-not $gitRev) { $gitRev = "n/a" }
$gitRev = $gitRev.Trim()
$files = Get-ChildItem $caseDir -File | ForEach-Object {
    [PSCustomObject]@{
        name   = $_.Name
        bytes  = $_.Length
        sha256 = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
    }
}
$manifest = [PSCustomObject]@{
    incidentId  = $IncidentId
    createdAt   = (Get-Date).ToString("o")
    hostname    = $env:COMPUTERNAME
    operator    = $env:USERNAME
    gitRev      = $gitRev
    auditDays   = $AuditDays
    logHours    = $LogHours
    files       = $files
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $caseDir "manifest.json")

# Bundle finale .tar.gz ------------------------------------------------
$bundle = Join-Path $OutputDir "$IncidentId-$timestamp.forensic.tar.gz"
Write-Host "[forensic] bundle finale -> $bundle" -ForegroundColor Cyan
# tar e' disponibile su Windows 10+ (bsdtar). -C entra nella cartella per
# evitare percorsi assoluti nell'archivio.
tar -czf $bundle -C $OutputDir (Split-Path $caseDir -Leaf)
if ($LASTEXITCODE -ne 0) { throw "tar fallito." }

$bundleHash = (Get-FileHash -Algorithm SHA256 $bundle).Hash.ToLowerInvariant()
"$bundleHash  $(Split-Path $bundle -Leaf)" | Set-Content -Encoding ascii "$bundle.sha256"

# Cleanup directory intermedia (resta solo il bundle + hash).
Remove-Item -Recurse -Force $caseDir

$sizeMB = [math]::Round((Get-Item $bundle).Length / 1MB, 2)
Write-Host ""
Write-Host "[forensic] OK" -ForegroundColor Green
Write-Host "  bundle  : $bundle"
Write-Host "  size    : $sizeMB MB"
Write-Host "  sha256  : $bundleHash"
Write-Host ""
Write-Host "Next steps (chain of custody):" -ForegroundColor Yellow
Write-Host "  1. Sposta SUBITO il bundle su storage controllato (S3 object-lock o vault)."
Write-Host "  2. Apri ticket incident e allega: nome bundle, sha256, gitRev=$gitRev."
Write-Host "  3. Annota chi ha accesso al bundle (PII al suo interno)."
Write-Host "  4. SOLO ORA procedi con rotazione segreti (secret-rotation.md compromise scenario)."
