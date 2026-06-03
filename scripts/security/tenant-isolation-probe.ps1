# Probe di tenant isolation per Accanto.
#
# Crea due utenti (A e B) con cerchi di cura separati e prova che B NON
# possa leggere/scrivere/cancellare risorse del cerchio di A.
#
# Uso (con lo stack docker compose attivo su http://localhost:8080):
#   pwsh scripts/security/tenant-isolation-probe.ps1
#
# Exit code 0 = nessuna violazione. Exit code 1 = almeno una violazione.

param(
    [string]$BaseUrl = "http://localhost:8080/api"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Risultati: 'PASS' | 'FAIL'. FAIL = la richiesta ha avuto successo quando
# avrebbe dovuto essere negata (200 invece di 403/404).
$script:results = [System.Collections.Generic.List[object]]::new()

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers = @{},
        $Body = $null
    )
    $uri = "$BaseUrl$Path"
    $args = @{
        Uri         = $uri
        Method      = $Method
        Headers     = $Headers
        ErrorAction = "Stop"
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $args.Body        = ($Body | ConvertTo-Json -Depth 8 -Compress)
        $args.ContentType = "application/json"
    }
    try {
        return Invoke-WebRequest @args
    }
    catch {
        # Sia Windows PowerShell 5.1 (WebException) sia PS 7+ (HttpResponseException)
        # espongono $_.Exception.Response per le risposte non-2xx.
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $status = [int]$resp.StatusCode
        $content = $null
        try {
            $stream = $resp.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
                $reader.Close()
            }
        }
        catch { }
        if ([string]::IsNullOrEmpty($content) -and $_.ErrorDetails) {
            $content = $_.ErrorDetails.Message
        }
        return [pscustomobject]@{ StatusCode = $status; Content = $content }
    }
}

function Register-And-Login {
    param([string]$EmailPrefix)
    $email = "$EmailPrefix+$(Get-Random -Maximum 999999)@accanto.local"
    $body = @{
        email       = $email
        displayName = $EmailPrefix
        password    = "Probe-Pass-12345!"
    }
    $r = Invoke-Api -Method Post -Path "/auth/register" -Body $body
    if ($r.StatusCode -ne 200) {
        throw "register $EmailPrefix fallita: $($r.StatusCode) $($r.Content)"
    }
    $data = $r.Content | ConvertFrom-Json
    return @{
        Email   = $email
        Token   = $data.accessToken
        UserId  = $data.user.id
        Headers = @{ Authorization = "Bearer $($data.accessToken)" }
    }
}

function Assert-Forbidden {
    param(
        [string]$Label,
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers,
        $Body = $null
    )
    $r = Invoke-Api -Method $Method -Path $Path -Headers $Headers -Body $Body
    # 401/403/404 = isolation OK. 200/201/204 = LEAK.
    # 400 = body rifiutato per validation: indica probabile errore del probe,
    # NON conclude nulla sull'IDOR. Lo segnaliamo come WARN (non FAIL).
    $ok = $r.StatusCode -in @(401, 403, 404)
    $warn = $r.StatusCode -eq 400
    $result = if ($ok) { 'PASS' } elseif ($warn) { 'WARN' } else { 'FAIL' }
    $script:results.Add([pscustomobject]@{
        Label  = $Label
        Method = $Method
        Path   = $Path
        Status = $r.StatusCode
        Result = $result
    })
}

Write-Host "==> Setup: registro utente A e creo risorse" -ForegroundColor Cyan
$A = Register-And-Login -EmailPrefix "alice"

# Care circle di Alice
$circleA = (Invoke-Api -Method Post -Path "/care-circles" -Headers $A.Headers `
    -Body @{ name = "Cerchio di Alice"; description = "probe" }).Content | ConvertFrom-Json
Write-Host "    circleA = $($circleA.id)"

# Timeline entry
$entryA = (Invoke-Api -Method Post -Path "/care-circles/$($circleA.id)/timeline" -Headers $A.Headers `
    -Body @{
        occurredAt = '2025-01-01T00:00:00Z'
        type       = 'PersonalNote'
        title      = 'Nota privata'
        content    = 'Contenuto riservato di Alice'
        tags       = @('privato')
        visibility = 'Private'
    }).Content | ConvertFrom-Json
Write-Host "    entryA  = $($entryA.id)"
if (-not $entryA.id) { throw "Setup fallito: timeline create non ha restituito un id" }

# Doctor question
$questionA = (Invoke-Api -Method Post -Path "/care-circles/$($circleA.id)/doctor-questions" -Headers $A.Headers `
    -Body @{ question = 'Posologia?'; category = 'Therapy' }).Content | ConvertFrom-Json
Write-Host "    questionA = $($questionA.id)"
if (-not $questionA.id) { throw "Setup fallito: doctor-question create" }

# Shared update
$updateA = (Invoke-Api -Method Post -Path "/care-circles/$($circleA.id)/shared-updates" -Headers $A.Headers `
    -Body @{ audience = 'CloseFamily'; content = 'Aggiornamento privato' }).Content | ConvertFrom-Json
Write-Host "    updateA = $($updateA.id)"
if (-not $updateA.id) { throw "Setup fallito: shared-update create" }

Write-Host "==> Setup: registro utente B (attaccante)" -ForegroundColor Cyan
$B = Register-And-Login -EmailPrefix "bob"

# Sanity: B vede il proprio cerchio vuoto
$mineB = (Invoke-Api -Method Get -Path "/care-circles" -Headers $B.Headers).Content | ConvertFrom-Json
if ($mineB.Count -ne 0) {
    Write-Host "WARN: B vede $($mineB.Count) cerchi propri (atteso 0)" -ForegroundColor Yellow
}

Write-Host "==> Probe IDOR: B tenta accesso alle risorse di A" -ForegroundColor Cyan

$cid = $circleA.id
$tid = $entryA.id
$qid = $questionA.id
$uid = $updateA.id

Assert-Forbidden "circle GetById"       Get    "/care-circles/$cid"                                   $B.Headers
Assert-Forbidden "circle Update"        Put    "/care-circles/$cid"                                   $B.Headers @{ name = "hijack"; description = "x" }
Assert-Forbidden "circle Archive"       Delete "/care-circles/$cid"                                   $B.Headers
Assert-Forbidden "circle Export PDF"    Get    "/care-circles/$cid/export/pdf"                        $B.Headers
Assert-Forbidden "circle Audit list"    Get    "/care-circles/$cid/audit"                             $B.Headers
Assert-Forbidden "timeline list"        Get    "/care-circles/$cid/timeline"                          $B.Headers
Assert-Forbidden "timeline GetById"     Get    "/care-circles/$cid/timeline/$tid"                     $B.Headers
Assert-Forbidden "timeline Update"      Put    "/care-circles/$cid/timeline/$tid"                     $B.Headers @{ occurredAt=(Get-Date).ToString('o'); type='PersonalNote'; title='pwn'; content='x'; tags=@(); visibility='Private' }
Assert-Forbidden "timeline Delete"      Delete "/care-circles/$cid/timeline/$tid"                     $B.Headers
Assert-Forbidden "timeline bulkPatch"   Patch  "/care-circles/$cid/timeline/bulk"                     $B.Headers @{ entryIds = @($tid); newVisibility = 'Circle' }
Assert-Forbidden "doctor-q list"        Get    "/care-circles/$cid/doctor-questions"                  $B.Headers
Assert-Forbidden "doctor-q Update"      Put    "/care-circles/$cid/doctor-questions/$qid"             $B.Headers @{ question = 'pwn'; category = 'Therapy' }
Assert-Forbidden "doctor-q Delete"      Delete "/care-circles/$cid/doctor-questions/$qid"             $B.Headers
Assert-Forbidden "shared-upd list"      Get    "/care-circles/$cid/shared-updates"                    $B.Headers
Assert-Forbidden "shared-upd GetById"   Get    "/care-circles/$cid/shared-updates/$uid"               $B.Headers
Assert-Forbidden "shared-upd Delete"    Delete "/care-circles/$cid/shared-updates/$uid"               $B.Headers
Assert-Forbidden "documents list"       Get    "/care-circles/$cid/documents"                         $B.Headers
Assert-Forbidden "invites list"         Get    "/care-circles/$cid/invites"                           $B.Headers
Assert-Forbidden "invites Create"       Post   "/care-circles/$cid/invites"                           $B.Headers @{ role = 'Caregiver'; expiresInDays = 7; maxUses = 1 }
Assert-Forbidden "ai settings Set"      Put    "/care-circles/$cid/ai/settings"                       $B.Headers @{ enabled = $false }
Assert-Forbidden "ai timeline-summary"  Post   "/care-circles/$cid/ai/timeline-summary"               $B.Headers @{}

Write-Host ""
Write-Host "==> Risultati" -ForegroundColor Cyan
$script:results | Format-Table Label, Method, Status, Result -AutoSize

$fails = @($script:results | Where-Object Result -eq 'FAIL')
if ($fails.Count -gt 0) {
    Write-Host ""
    Write-Host "TENANT ISOLATION LEAK ($($fails.Count) finding):" -ForegroundColor Red
    $fails | Format-Table -AutoSize
    exit 1
}
Write-Host "Tenant isolation OK: tutti i tentativi cross-tenant respinti." -ForegroundColor Green
exit 0
