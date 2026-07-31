<#
.SYNOPSIS
    Imposta (o reimposta) la password di un admin del control plane direttamente
    nel DB admin, per test locali su Docker.

.DESCRIPTION
    L'admin viene seedato SENZA password (login bloccato finche' non completa il
    flusso di reset). Per testare velocemente l'admin-web su Docker senza SMTP,
    questo script calcola l'hash PBKDF2 nello STESSO formato di AdminPasswordHasher
    ("{iterations}.{saltB64}.{hashB64}", HMACSHA256, 100k iter, salt 16B, hash 32B)
    e fa UPDATE admin_users.PasswordHash via `docker compose exec postgres-admin psql`.

    NON e' pensato per la produzione: usa una password nota e debole di default.

.PARAMETER Email
    Email dell'admin (default: admin@example.com, quello del seed dev).

.PARAMETER Password
    Password in chiaro da impostare (default: "admin12345" - solo per test locali).

.PARAMETER ComposeService
    Nome del servizio Postgres admin nel compose (default: postgres-admin).

.PARAMETER Database / .PARAMETER DbUser
    Database e utente Postgres admin (default: accanto_admin / accanto_admin).

.EXAMPLE
    ./scripts/db/set-admin-password.ps1
    # imposta admin@example.com / admin12345

.EXAMPLE
    ./scripts/db/set-admin-password.ps1 -Email me@accanto.care -Password 'Str0ngLocal!'
#>
[CmdletBinding()]
param(
    [string]$Email = 'admin@example.com',
    [string]$Password = 'admin12345',
    [string]$ComposeService = 'postgres-admin',
    [string]$Database = 'accanto_admin',
    [string]$DbUser = 'accanto_admin'
)

$ErrorActionPreference = 'Stop'

# --- Parametri PBKDF2 identici a AdminPasswordHasher.cs -----------------------
$iterations = 100000
$saltBytes  = 16
$hashBytes  = 32

$salt = New-Object byte[] $saltBytes
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }

$pbkdf2 = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
    $Password,
    $salt,
    $iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    $hash = $pbkdf2.GetBytes($hashBytes)
} finally {
    $pbkdf2.Dispose()
}

$saltB64 = [Convert]::ToBase64String($salt)
$hashB64 = [Convert]::ToBase64String($hash)
$stored  = "$iterations.$saltB64.$hashB64"

$emailLower = $Email.Trim().ToLowerInvariant()

Write-Host "[set-admin-password] target : $emailLower" -ForegroundColor Cyan
Write-Host "[set-admin-password] hash   : $($stored.Substring(0, [Math]::Min(40, $stored.Length)))..." -ForegroundColor DarkGray

# --- SQL: UPDATE dell'hash. Attiva anche l'account per sicurezza. -------------
# Literal SQL con escaping single-quote (raddoppio degli apici). L'hash PBKDF2
# contiene solo caratteri base64 (A-Za-z0-9+/=), niente da escapare oltre agli
# apici. Passiamo lo script via stdin a psql (piu' robusto di -c con le variabili).
$phSql = $stored.Replace("'", "''")
$emSql = $emailLower.Replace("'", "''")
$sql = @"
UPDATE admin_users
SET "PasswordHash" = '$phSql', "IsActive" = true
WHERE lower("Email") = '$emSql';
SELECT "Email", "IsActive", left("PasswordHash", 12) AS hash_prefix
FROM admin_users WHERE lower("Email") = '$emSql';
"@

$psqlArgs = @(
    'compose', 'exec', '-T', $ComposeService,
    'psql', '-v', 'ON_ERROR_STOP=1',
    '-U', $DbUser, '-d', $Database
)

Write-Host "[set-admin-password] docker compose exec -T $ComposeService psql -U $DbUser -d $Database (SQL via stdin)" -ForegroundColor DarkGray
$output = $sql | & docker @psqlArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "psql fallito (exit $LASTEXITCODE):`n$output"
    exit 1
}
$outText = ($output | Out-String)
$outText | Write-Host

# Verifica che una riga sia stata aggiornata (psql stampa "UPDATE 1").
if ($outText -notmatch 'UPDATE\s+1') {
    Write-Warning "Nessuna riga aggiornata: l'admin '$emailLower' esiste? (il seed lo crea all'avvio dell'Admin API)."
    Write-Warning "Suggerimento: avvia lo stack (docker compose up -d accanto-admin-api) e assicurati che AdminSeed__Admins includa questa email."
    exit 2
}

Write-Host ""
Write-Host "[set-admin-password] OK. Ora puoi accedere all'admin-web:" -ForegroundColor Green
Write-Host "  URL      : http://localhost:5174" -ForegroundColor Green
Write-Host "  Email    : $emailLower" -ForegroundColor Green
Write-Host "  Password : $Password" -ForegroundColor Green
Write-Host ""
Write-Host "NOTA: password di test debole. Non usare in produzione." -ForegroundColor Yellow
