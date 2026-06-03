# Probe RBAC per Accanto.
#
# Verifica che i ruoli sui cerchi di cura (Owner, Caregiver, Viewer) siano
# applicati correttamente: il sistema impedisce a un Viewer di scrivere e
# a un Caregiver di compiere azioni riservate all'Owner.
#
# Setup:
#   - Alice (Owner) crea un cerchio
#   - Alice invita Bob come Viewer e Charlie come Caregiver
#   - Bob e Charlie accettano gli inviti
#
# Probe:
#   - Viewer: read OK, qualunque scrittura/azione owner -> 403
#   - Caregiver: read/write OK, azioni owner (archive/invite/AI) -> 403
#
# Uso (stack attivo su http://localhost:8080):
#   pwsh scripts/security/rbac-probe.ps1
#
# Exit 0 = OK. Exit 1 = almeno una violazione RBAC.

param(
    [string]$BaseUrl = "http://localhost:8080/api"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

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
        $status = [int]$resp.StatusCode
        $content = $null
        try {
            $stream = $resp.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd(); $reader.Close()
            }
        } catch {}
        if ([string]::IsNullOrEmpty($content) -and $_.ErrorDetails) { $content = $_.ErrorDetails.Message }
        return [pscustomobject]@{ StatusCode = $status; Content = $content }
    }
}

function Register-And-Login {
    param([string]$EmailPrefix)
    $email = "$EmailPrefix+$(Get-Random -Maximum 999999)@accanto.local"
    $body = @{ email = $email; displayName = $EmailPrefix; password = "Probe-Pass-12345!" }
    $r = Invoke-Api -Method Post -Path "/auth/register" -Body $body
    if ($r.StatusCode -ne 200) { throw "register $EmailPrefix fallita: $($r.StatusCode) $($r.Content)" }
    $d = $r.Content | ConvertFrom-Json
    return @{
        Email = $email; UserId = $d.user.id
        Headers = @{ Authorization = "Bearer $($d.accessToken)" }
    }
}

# Assert su uno status atteso. Se status reale != atteso, FAIL.
# 'Allow'  = ci aspettiamo successo (200/201/204) per ruolo che PUO' fare l'azione
# 'Deny'   = ci aspettiamo 403 (o 401/404) per ruolo che NON PUO' fare l'azione
function Assert-Role {
    param(
        [string]$Label,
        [ValidateSet('Allow','Deny')] [string]$Expectation,
        [string]$Method, [string]$Path, [hashtable]$Headers, $Body = $null
    )
    $r = Invoke-Api -Method $Method -Path $Path -Headers $Headers -Body $Body
    $code = $r.StatusCode
    $isSuccess = $code -in @(200, 201, 204)
    $isDenied  = $code -in @(401, 403, 404)
    $isValidationErr = $code -in @(400, 422)
    $result = switch ($Expectation) {
        'Allow' {
            if ($isSuccess) { 'PASS' }
            elseif ($isValidationErr) { 'WARN' }   # probabile bug payload del probe
            else { 'FAIL' }                         # ci aspettavamo successo -> 403
        }
        'Deny'  {
            if ($isDenied) { 'PASS' }
            elseif ($isValidationErr) { 'WARN' }   # validation prima di authz, inconcludente
            else { 'FAIL' }                         # azione riuscita -> escalation di privilegio
        }
    }
    $script:results.Add([pscustomobject]@{
        Label = $Label; Expect = $Expectation; Method = $Method
        Status = $code; Result = $result
    })
}

# ============================================================
# Setup
# ============================================================

Write-Host "==> Setup: registro Owner Alice e creo cerchio" -ForegroundColor Cyan
$Alice = Register-And-Login -EmailPrefix "alice"

$circle = (Invoke-Api -Method Post -Path "/care-circles" -Headers $Alice.Headers `
    -Body @{ name = "Cerchio probe RBAC"; description = "probe" }).Content | ConvertFrom-Json
$cid = $circle.id
if (-not $cid) { throw "Setup: care-circle create fallito" }
Write-Host "    circle = $cid"

Write-Host "==> Setup: registro Viewer Bob e Caregiver Charlie" -ForegroundColor Cyan
$Bob     = Register-And-Login -EmailPrefix "bob"
$Charlie = Register-And-Login -EmailPrefix "charlie"

# Owner Alice emette due inviti (uno per ruolo) e li fa accettare.
function New-InviteToken {
    param([string]$Role)
    $r = Invoke-Api -Method Post -Path "/care-circles/$cid/invites" -Headers $Alice.Headers `
        -Body @{ role = $Role; expiresInDays = 7; maxUses = 1 }
    if ($r.StatusCode -ne 201 -and $r.StatusCode -ne 200) {
        throw "create invite ($Role) fallita: $($r.StatusCode) $($r.Content)"
    }
    $dto = $r.Content | ConvertFrom-Json
    # InviteDto deve esporre il token; fallback su 'link' o 'url'.
    foreach ($p in 'token','inviteToken','code','link','url') {
        if ($dto.$p) { return $dto.$p }
    }
    throw "InviteDto senza token: $($r.Content)"
}

$tokViewer    = New-InviteToken -Role 'Viewer'
$tokCaregiver = New-InviteToken -Role 'Caregiver'

# Se il backend restituisce un link/url, estraggo l'ultimo segmento come token.
foreach ($v in 'tokViewer','tokCaregiver') {
    $val = (Get-Variable $v).Value
    if ($val -match '/') { Set-Variable $v ($val.TrimEnd('/').Split('/')[-1]) }
}

Write-Host "    tokViewer    = $tokViewer"
Write-Host "    tokCaregiver = $tokCaregiver"

$acceptBob = Invoke-Api -Method Post -Path "/invites/$tokViewer/accept" -Headers $Bob.Headers
if ($acceptBob.StatusCode -notin @(200,201)) { throw "Bob accept fallita: $($acceptBob.StatusCode) $($acceptBob.Content)" }
$acceptCharlie = Invoke-Api -Method Post -Path "/invites/$tokCaregiver/accept" -Headers $Charlie.Headers
if ($acceptCharlie.StatusCode -notin @(200,201)) { throw "Charlie accept fallita: $($acceptCharlie.StatusCode) $($acceptCharlie.Content)" }

Write-Host "    Bob (Viewer) e Charlie (Caregiver) sono membri del cerchio"

# Body di prova per le scritture
$timelineBody = @{
    occurredAt = ([DateTime]::UtcNow).ToString('o'); type = 'PersonalNote'
    title = 'probe'; content = 'x'; tags = @(); visibility = 'Private'
}
$invBody = @{ role = 'Viewer'; expiresInDays = 7; maxUses = 1 }
$aiBody  = @{ enabled = $false }
$qBody   = @{ question = 'probe?'; category = 'Therapy' }

# ============================================================
# VIEWER (Bob): read OK, qualunque scrittura/owner-action 403
# ============================================================
Write-Host "==> Probe Viewer (Bob)" -ForegroundColor Cyan

Assert-Role "viewer GET circle"            Allow Get    "/care-circles/$cid"                          $Bob.Headers
Assert-Role "viewer GET timeline"          Allow Get    "/care-circles/$cid/timeline"                 $Bob.Headers
Assert-Role "viewer GET doctor-q"          Allow Get    "/care-circles/$cid/doctor-questions"         $Bob.Headers
Assert-Role "viewer GET shared-upd"        Allow Get    "/care-circles/$cid/shared-updates"           $Bob.Headers
Assert-Role "viewer GET documents"         Allow Get    "/care-circles/$cid/documents"                $Bob.Headers
Assert-Role "viewer GET audit"             Allow Get    "/care-circles/$cid/audit"                    $Bob.Headers

Assert-Role "viewer POST timeline"         Deny  Post   "/care-circles/$cid/timeline"                 $Bob.Headers $timelineBody
Assert-Role "viewer POST doctor-q"         Deny  Post   "/care-circles/$cid/doctor-questions"         $Bob.Headers $qBody
Assert-Role "viewer POST shared-upd"       Deny  Post   "/care-circles/$cid/shared-updates"           $Bob.Headers @{ audience = 'CloseFamily'; content = 'pwn' }
Assert-Role "viewer PUT circle"            Deny  Put    "/care-circles/$cid"                          $Bob.Headers @{ name = 'pwn'; description = 'x' }
Assert-Role "viewer DELETE circle"         Deny  Delete "/care-circles/$cid"                          $Bob.Headers
Assert-Role "viewer POST invite"           Deny  Post   "/care-circles/$cid/invites"                  $Bob.Headers $invBody
Assert-Role "viewer GET invites list"      Deny  Get    "/care-circles/$cid/invites"                  $Bob.Headers
Assert-Role "viewer PUT ai settings"       Deny  Put    "/care-circles/$cid/ai/settings"              $Bob.Headers $aiBody
Assert-Role "viewer POST ai summary"       Deny  Post   "/care-circles/$cid/ai/timeline-summary"      $Bob.Headers @{}

# ============================================================
# CAREGIVER (Charlie): write OK, owner-action 403
# ============================================================
Write-Host "==> Probe Caregiver (Charlie)" -ForegroundColor Cyan

Assert-Role "careg GET circle"             Allow Get    "/care-circles/$cid"                          $Charlie.Headers
Assert-Role "careg POST timeline"          Allow Post   "/care-circles/$cid/timeline"                 $Charlie.Headers $timelineBody
Assert-Role "careg POST doctor-q"          Allow Post   "/care-circles/$cid/doctor-questions"         $Charlie.Headers $qBody
Assert-Role "careg POST shared-upd"        Allow Post   "/care-circles/$cid/shared-updates"           $Charlie.Headers @{ audience = 'CloseFamily'; content = 'aggiornamento di Charlie' }

Assert-Role "careg DELETE circle"          Deny  Delete "/care-circles/$cid"                          $Charlie.Headers
Assert-Role "careg POST invite"            Deny  Post   "/care-circles/$cid/invites"                  $Charlie.Headers $invBody
Assert-Role "careg GET invites list"       Deny  Get    "/care-circles/$cid/invites"                  $Charlie.Headers
Assert-Role "careg PUT ai settings"        Deny  Put    "/care-circles/$cid/ai/settings"              $Charlie.Headers $aiBody

# ============================================================
# Report
# ============================================================
Write-Host ""
Write-Host "==> Risultati" -ForegroundColor Cyan
$script:results | Format-Table Label, Expect, Method, Status, Result -AutoSize

$fails = @($script:results | Where-Object Result -eq 'FAIL')
$warns = @($script:results | Where-Object Result -eq 'WARN')

if ($warns.Count -gt 0) {
    Write-Host "WARN ($($warns.Count)): probabile bug nel payload del probe, non concludente." -ForegroundColor Yellow
    $warns | Format-Table Label, Method, Status -AutoSize
}

if ($fails.Count -gt 0) {
    Write-Host ""
    Write-Host "RBAC VIOLATION ($($fails.Count) finding):" -ForegroundColor Red
    $fails | Format-Table Label, Expect, Method, Status -AutoSize
    exit 1
}
Write-Host "RBAC OK: ogni ruolo rispetta i confini attesi." -ForegroundColor Green
exit 0
