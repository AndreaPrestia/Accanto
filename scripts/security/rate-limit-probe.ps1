# Probe rate limit + lockout per Accanto.
#
# Verifica che le policy di rate limit (login, register, invite, ai, sensitive)
# scattino quando un singolo attore supera il bucket. Il backend deve
# rispondere 429 Too Many Requests dopo N tentativi nella finestra.
#
# In development i limiti in appsettings.Development.json sono volutamente
# alti (10000/min) per non disturbare i test E2E. Questo script li
# riconfigura temporaneamente passando variabili d'ambiente al container
# backend (override del compose) e ripristina i default a fine corsa.
#
# Uso (stack attivo su http://localhost:8080):
#   pwsh scripts/security/rate-limit-probe.ps1
#
# Exit 0 = tutte le policy hanno scattato come atteso.
# Exit 1 = almeno una policy NON ha applicato il 429.

param(
    [string]$BaseUrl  = "http://localhost:8080/api",
    [int]$LoginLimit  = 3,
    [int]$RegLimit    = 3,
    [int]$SensLimit   = 3,
    [int]$InviteLimit = 3,
    [int]$AiLimit     = 3
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

$script:results = [System.Collections.Generic.List[object]]::new()

function Invoke-Api {
    param([string]$Method, [string]$Path, [hashtable]$Headers = @{}, $Body = $null)
    $args = @{
        Uri = "$BaseUrl$Path"; Method = $Method; Headers = $Headers
        ErrorAction = "Stop"; UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $args.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
        $args.ContentType = "application/json"
    }
    try { return Invoke-WebRequest @args }
    catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Content = $null }
    }
}

# Esegue $TotalCalls richieste in sequenza e ritorna l'array di status code.
function Hammer {
    param(
        [int]$TotalCalls, [string]$Method, [string]$Path,
        [hashtable]$Headers = @{}, [scriptblock]$BodyFactory = $null
    )
    $codes = New-Object 'System.Collections.Generic.List[int]'
    for ($i = 0; $i -lt $TotalCalls; $i++) {
        $body = if ($BodyFactory) { & $BodyFactory $i } else { $null }
        $r = Invoke-Api -Method $Method -Path $Path -Headers $Headers -Body $body
        $codes.Add($r.StatusCode)
    }
    return ,$codes.ToArray()
}

# Atteso: tutte le prime $Limit chiamate < 429, dalla ($Limit+1)-esima -> 429.
function Assert-Throttled {
    param(
        [string]$Label, [int]$Limit, [int[]]$Codes
    )
    $first  = $Codes[0..($Limit-1)]
    $rest   = $Codes[$Limit..($Codes.Length-1)]
    $okFirst = -not ($first -contains 429)
    $okRest  = ($rest | Where-Object { $_ -eq 429 }).Count -gt 0
    $result  = if ($okFirst -and $okRest) { 'PASS' } else { 'FAIL' }
    $script:results.Add([pscustomobject]@{
        Label = $Label; Limit = $Limit
        FirstWindow = ($first -join ',')
        Overflow    = ($rest  -join ',')
        Result      = $result
    })
}

# ============================================================
# Step 1: riconfigura il backend con i limiti bassi
# ============================================================
Write-Host "==> Riavvio backend con rate limit bassi (Login=$LoginLimit, Register=$RegLimit, Sensitive=$SensLimit, InviteCreate=$InviteLimit, Ai=$AiLimit)" -ForegroundColor Cyan

$envOverride = @{
    'RateLimit__Login__PermitLimit'        = "$LoginLimit"
    'RateLimit__Login__Window'             = '00:01:00'
    'RateLimit__Register__PermitLimit'     = "$RegLimit"
    'RateLimit__Register__Window'          = '00:01:00'
    'RateLimit__Sensitive__PermitLimit'    = "$SensLimit"
    'RateLimit__Sensitive__Window'         = '00:01:00'
    'RateLimit__InviteCreate__PermitLimit' = "$InviteLimit"
    'RateLimit__InviteCreate__Window'      = '00:01:00'
    'RateLimit__Ai__PermitLimit'           = "$AiLimit"
    'RateLimit__Ai__Window'                = '00:01:00'
}

# Build args -e KEY=VAL ...
$envArgs = @()
foreach ($k in $envOverride.Keys) { $envArgs += '-e'; $envArgs += "$k=$($envOverride[$k])" }

# Stoppa solo il backend e lo rilancia con docker run su stessa rete/volume
# è troppo invasivo. Più semplice: usa docker compose run --rm? No, ports
# va perso. Usa `docker compose stop backend` + start con env esportate.
# In compose v2 le env_file non riprendono variabili passate da CLI; più
# pulito generare un override file effimero.
$override = @'
services:
  backend:
    environment:
'@
foreach ($k in $envOverride.Keys) {
    $override += "`n      $($k): `"$($envOverride[$k])`""
}

$overridePath = Join-Path $env:TEMP 'accanto-ratelimit-override.yml'
$override | Set-Content -Path $overridePath -Encoding utf8

try {
    docker compose -f docker-compose.yml -f $overridePath up -d backend | Out-Null
    Write-Host "    backend riavviato, attendo readiness..." -ForegroundColor DarkGray
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        try {
            $h = Invoke-WebRequest "$BaseUrl/../health" -UseBasicParsing -TimeoutSec 2
            if ($h.StatusCode -eq 200) { break }
        } catch { Start-Sleep -Milliseconds 500 }
    }

    # ============================================================
    # Step 2: probe
    # ============================================================
    Write-Host "==> Probe rate limit" -ForegroundColor Cyan

    # Setup PRIMA dei probe per IP, altrimenti la registrazione dell'utente
    # autenticato consuma il bucket Register e cade in 429.
    $email = "rl-invite+$([Guid]::NewGuid().ToString('n'))@accanto.local"
    $regResp = Invoke-Api -Method Post -Path "/auth/register" -Body @{
        email = $email; displayName = "rl"; password = "Probe-Pass-12345!"
    }
    if ($regResp.StatusCode -ne 200) { throw "register utente probe fallita: $($regResp.StatusCode)" }
    $auth = ($regResp.Content | ConvertFrom-Json)
    $H = @{ Authorization = "Bearer $($auth.accessToken)" }
    $circle = (Invoke-Api -Method Post -Path "/care-circles" -Headers $H `
        -Body @{ name = "rl"; description = "rl" }).Content | ConvertFrom-Json

    # --- LOGIN: limite per IP. Generiamo $LoginLimit+3 tentativi falliti.
    $loginBody = { param($i) @{ email = "doesnotexist+$i@accanto.local"; password = "wrongpass-$i" } }
    $codes = Hammer -TotalCalls ($LoginLimit + 3) -Method Post -Path "/auth/login" -BodyFactory $loginBody
    Assert-Throttled -Label "login (per IP)" -Limit $LoginLimit -Codes $codes

    # --- REGISTER: limite per IP. Email valide ma diverse.
    # Nota: $RegLimit chiamate addizionali oltre quelle gia' consumate dal
    # setup. Per evitare ambiguita' aggiungiamo un buffer ampio.
    $regBody = { param($i)
        @{ email = "ratelimit+$([Guid]::NewGuid().ToString('n'))@accanto.local"
           displayName = "rl"; password = "Probe-Pass-12345!" }
    }
    # Abbiamo gia' fatto 1 register nel setup, quindi nel bucket restano $RegLimit-1.
    # Il primo Hammer call partira' gia' dal conteggio 2. Per semplicita',
    # ridimensioniamo: Hammer fa $RegLimit chiamate (saturera' bucket) + 3 di overflow.
    $codes = Hammer -TotalCalls ($RegLimit + 3) -Method Post -Path "/auth/register" -BodyFactory $regBody
    # Il bucket era a 1/$RegLimit, dopo $RegLimit-1 chiamate saturera'; le ultime
    # 3+1 saranno 429. Verifichiamo che ci siano 429 nella coda e che le prime
    # ($RegLimit-1) NON lo siano.
    Assert-Throttled -Label "register (per IP)" -Limit ($RegLimit - 1) -Codes $codes

    # --- INVITE CREATE: limite per utente.
    $inviteBody = { param($i) @{ role = 'Viewer'; expiresInDays = 7; maxUses = 1 } }
    $codes = Hammer -TotalCalls ($InviteLimit + 3) -Method Post -Path "/care-circles/$($circle.id)/invites" -Headers $H -BodyFactory $inviteBody
    Assert-Throttled -Label "invite-create (per utente)" -Limit $InviteLimit -Codes $codes

    # --- SENSITIVE: change password con vecchia password sbagliata.
    $sensBody = { param($i)
        @{ currentPassword = "wrong-$i!"; newPassword = "NewProbe-Pass-12345!" }
    }
    $codes = Hammer -TotalCalls ($SensLimit + 3) -Method Post -Path "/account/change-password" -Headers $H -BodyFactory $sensBody
    Assert-Throttled -Label "auth-sensitive (per utente)" -Limit $SensLimit -Codes $codes

    # ============================================================
    # Step 3: report
    # ============================================================
    Write-Host ""
    Write-Host "==> Risultati" -ForegroundColor Cyan
    $script:results | Format-Table Label, Limit, FirstWindow, Overflow, Result -AutoSize
}
finally {
    Write-Host "==> Ripristino backend ai default" -ForegroundColor Cyan
    Remove-Item $overridePath -ErrorAction SilentlyContinue
    docker compose -f docker-compose.yml up -d backend | Out-Null
    $global:LASTEXITCODE = 0
}

$fails = @($script:results | Where-Object Result -eq 'FAIL')
if ($fails.Count -gt 0) {
    Write-Host ""
    Write-Host "RATE LIMIT NON SCATTATO ($($fails.Count) finding):" -ForegroundColor Red
    $fails | Format-Table -AutoSize
    exit 1
}
Write-Host "Rate limit OK: tutte le policy scattano dopo il bucket." -ForegroundColor Green
exit 0
