<#
.SYNOPSIS
  Seed dell'account demo per gli screenshot store, via API pubbliche.

.DESCRIPTION
  Crea (o riusa) un account demo su staging e popola:
    - 1 care circle "Famiglia Rossi" (attivo, con descrizione)
    - 4 voci timeline (Symptom, Medication, Appointment, PersonalNote) - date recenti
    - 3 domande per il medico (ToAsk, Asked, Answered)
    - 5 documenti PDF finti (categorie diverse, 1 pagina, zero PII)

  Le credenziali NON sono mai hardcoded: vengono chieste in modo sicuro
  (Read-Host -AsSecureString) oppure passate via env ACCANTO_DEMO_EMAIL /
  ACCANTO_DEMO_PASSWORD (utile in CI). Dopo il seed, cambia la password
  dall'app/account e il dato resta.

  Endpoint reali (verificati su backend/src/Accanto.Api/Controllers).
  N.B.: i controller backend espongono le rotte sotto /api, ma l'edge Caddy
  riscrive ogni richiesta aggiungendo il prefisso (deploy/Caddyfile:
  `rewrite @notHealth /api{uri}`). Il client chiama quindi SENZA /api:
    POST /auth/register|login           (AuthController)
    POST /care-circles                  (CareCirclesController)
    POST /care-circles/{id}/timeline    (TimelineController)
    POST /care-circles/{id}/doctor-questions (DoctorQuestionsController)
    POST /care-circles/{id}/documents   (DocumentsController, multipart)

.PARAMETER BaseUrl
  API base (default staging).

.PARAMETER CircleName
  Nome del cerchio: deve combaciare con `tapOn` in .maestro/screenshots.yaml.

.PARAMETER PostgresContainer
  Opzionale. Nome del container Docker Postgres (es. 'accanto-db'). Se passato
  (o env ACCANTO_PG_CONTAINER), lo script estende la deadline 2FA dell'account
  demo di 365 giorni (UPDATE users ... TwoFactorRequiredFromUtc) cosi' non
  scatta il 403 del RequireTwoFactorForOwnersMiddleware. Utile per account
  reviewer Apple (2FA DEVE restare spenta ma l'Owner verrebbe bloccato dopo 7gg).

.EXAMPLE
  pwsh scripts/seed-demo-account.ps1
  (chiede email + password in modo sicuro)

.EXAMPLE
  pwsh scripts/seed-demo-account.ps1 -BaseUrl https://api.accanto.care -PostgresContainer accanto-db
  (seed + estensione deadline 2FA via docker exec psql)

.EXAMPLE
  $env:ACCANTO_DEMO_EMAIL='demo@accanto.care'; $env:ACCANTO_DEMO_PASSWORD='***'
  pwsh scripts/seed-demo-account.ps1 -BaseUrl https://api.staging.accanto.care
#>
[CmdletBinding()]
param(
  [string]$BaseUrl = 'https://api.staging.accanto.care',
  [string]$CircleName = 'Famiglia Rossi',

  # Estensione deadline 2FA (opzionale). Se assenti, legge env omonime.
  [string]$PostgresContainer,
  [string]$PostgresDb = 'accanto',
  [string]$PostgresUser = 'postgres',
  [int]$TwoFactorGraceDays = 365
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Credenziali sicure (mai nel file)
# ---------------------------------------------------------------------------
$Email = $env:ACCANTO_DEMO_EMAIL
if (-not $Email) { $Email = Read-Host 'Email account demo' }

$PasswordPlain = $env:ACCANTO_DEMO_PASSWORD
if (-not $PasswordPlain) {
  $sec = Read-Host 'Password account demo' -AsSecureString
  $PasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}

$DisplayName = 'Anna'

# ---------------------------------------------------------------------------
# Helper HTTP
# ---------------------------------------------------------------------------
$script:Token = $null

function Invoke-Api {
  param(
    [Parameter(Mandatory)] [string]$Method,
    [Parameter(Mandatory)] [string]$Path,
    [object]$Body,
    [string]$ContentType = 'application/json'
  )
  $uri = "$BaseUrl$Path"
  $headers = @{}
  if ($script:Token) { $headers['Authorization'] = "Bearer $Token" }

  $params = @{
    Method      = $Method
    Uri         = $uri
    Headers     = $headers
    ErrorAction = 'Stop'
  }
  if ($null -ne $Body) {
    if ($ContentType -eq 'application/json') {
      $params['Body'] = ($Body | ConvertTo-Json -Depth 6)
      $params['ContentType'] = 'application/json'
    }
  }
  return Invoke-RestMethod @params
}

# ---------------------------------------------------------------------------
# 1. Auth: prova login; se fallisce registra l'account
# ---------------------------------------------------------------------------
Write-Host '==> Auth' -ForegroundColor Cyan
$loginBody = @{ email = $Email; password = $PasswordPlain }
try {
  $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/login" `
    -Body ($loginBody | ConvertTo-Json) -ContentType 'application/json' -ErrorAction Stop
} catch {
  $login = $null
}

if ($null -eq $login) {
  Write-Host '  - login fallito: registro nuovo account'
  $regBody = @{ email = $Email; displayName = $DisplayName; password = $PasswordPlain }
  $reg = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/register" `
    -Body ($regBody | ConvertTo-Json) -ContentType 'application/json' -ErrorAction Stop
  $script:Token = $reg.accessToken
  Write-Host '  - account registrato' -ForegroundColor Green
} elseif ($login.requiresTwoFactor) {
  throw "L'account demo ha 2FA attiva: per il seed serve un account SENZA 2FA (lo script non gestisce il challenge). Disattivala temporaneamente o usa un altro account."
} else {
  $script:Token = $login.auth.accessToken
  Write-Host '  - login ok' -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 2. Care circle: riusa se esiste gia', altrimenti crea
# ---------------------------------------------------------------------------
Write-Host '==> Care circle' -ForegroundColor Cyan
$mine = Invoke-Api -Method Get -Path '/care-circles'
$circle = $mine | Where-Object { $_.name -eq $CircleName -and $_.status -ne 'Archived' } | Select-Object -First 1

if ($circle) {
  Write-Host "  - riuso cerchio esistente ($($circle.id))" -ForegroundColor DarkGray
} else {
  $circle = Invoke-Api -Method Post -Path '/care-circles' -Body @{
    name        = $CircleName
    description = 'Assistenza alla nonna Maria: terapie, visite e appunti condivisi tra fratelli.'
  }
  Write-Host "  - cerchio creato ($($circle.id))" -ForegroundColor Green
}
$CircleId = $circle.id

# ---------------------------------------------------------------------------
# 3. Timeline (4 voci, date recenti, visibilita' Circle)
# ---------------------------------------------------------------------------
Write-Host '==> Timeline' -ForegroundColor Cyan
$now = [DateTimeOffset]::UtcNow
$entries = @(
  @{ daysAgo = 6; type = 'Symptom';      title = 'Stanchezza pomeridiana';   content = 'Stanchezza nel pomeriggio, migliorata dopo il riposo.' },
  @{ daysAgo = 4; type = 'Medication';   title = 'Cambio dosaggio';          content = 'Cambio dosaggio antidolorifico su indicazione del medico.' },
  @{ daysAgo = 2; type = 'Appointment';  title = 'Visita di controllo';      content = 'Visita di controllo - Dott.ssa Bianchi, ore 10:30.' },
  @{ daysAgo = 0; type = 'PersonalNote'; title = 'Buon umore';               content = 'Oggi la nonna era di buon umore, passeggiata in giardino.' }
)
foreach ($e in $entries) {
  Invoke-Api -Method Post -Path "/care-circles/$CircleId/timeline" -Body @{
    occurredAt = $now.AddDays(-$e.daysAgo).ToString('o')
    type       = $e.type
    title      = $e.title
    content    = $e.content
    tags       = @('demo')
    visibility = 'Circle'
  } | Out-Null
  Write-Host "  - $($e.type): $($e.title)" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 4. Domande per il medico (3 stati diversi)
# ---------------------------------------------------------------------------
Write-Host '==> Domande medico' -ForegroundColor Cyan
$questions = @(
  @{ q = 'Possiamo ridurre il dosaggio serale?';                 cat = 'Therapy' },
  @{ q = "Il dolore notturno e' legato alla nuova terapia?";     cat = 'Pain' },
  @{ q = "Serve un integratore per l'appetito?";                 cat = 'Nutrition' }
)
$created = @()
foreach ($q in $questions) {
  $created += Invoke-Api -Method Post -Path "/care-circles/$CircleId/doctor-questions" -Body @{
    question = $q.q
    category = $q.cat
  }
  Write-Host "  - $($q.cat): $($q.q)" -ForegroundColor Green
}
# Porta la 2a ad Asked e la 3a ad Answered per varieta' visiva
$statusMap = @('Asked', 'Answered')
for ($i = 1; $i -le 2; $i++) {
  $cur = $created[$i]
  Invoke-Api -Method Put -Path "/care-circles/$CircleId/doctor-questions/$($cur.id)" -Body @{
    question    = $cur.question
    category    = $cur.category
    status      = $statusMap[$i - 1]
    answerNotes = $(if ($statusMap[$i - 1] -eq 'Answered') { 'Il medico consiglia rivalutazione al prossimo controllo.' } else { $null })
  } | Out-Null
}

# ---------------------------------------------------------------------------
# 5. Documenti (5 PDF finti, 1 pagina, multipart upload)
# ---------------------------------------------------------------------------
Write-Host '==> Documenti' -ForegroundColor Cyan
Add-Type -AssemblyName System.Drawing

$TmpPdfDir = Join-Path $env:TEMP "accanto-demo-docs"
New-Item -ItemType Directory -Force $TmpPdfDir | Out-Null

$docs = @(
  @{ cat = 'Report';       file = 'Referto cardiologia 2026-07.pdf' },
  @{ cat = 'Prescription'; file = 'Ricetta terapia luglio.pdf' },
  @{ cat = 'BloodTest';    file = 'Esami del sangue 15-07.pdf' },
  @{ cat = 'Imaging';      file = 'RX torace.pdf' },
  @{ cat = 'Other';        file = 'Dieta consigliata.pdf' }
)

# PDF minimo valido con 1 pagina bianca (nessuna dipendenza esterna)
function New-DummyPdf([string]$Path, [string]$Label) {
  $content = @"
%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]>>endobj
trailer<</Root 1 0 R>>
%%EOF
"@
  [System.IO.File]::WriteAllText($Path, $content)
}

foreach ($d in $docs) {
  $pdfPath = Join-Path $TmpPdfDir $d.file
  New-DummyPdf -Path $pdfPath -Label $d.file

  $uri = "$BaseUrl/care-circles/$CircleId/documents"
  $headers = @{ Authorization = "Bearer $Token" }

  # multipart/form-data via HttpClient (PS5.1 compatibile)
  $handler = New-Object System.Net.Http.HttpClientHandler
  $client  = New-Object System.Net.Http.HttpClient($handler)
  try {
    $client.DefaultRequestHeaders.Authorization =
      New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
    $form = New-Object System.Net.Http.MultipartFormDataContent
    $bytes = [System.IO.File]::ReadAllBytes($pdfPath)
    # PS5.1: New-Object con singolo argomento costruttore risolve male
    # l'overload -> usare la sintassi con array di argomenti esplicito.
    $fileContent = New-Object System.Net.Http.ByteArrayContent @(,$bytes)
    $fileContent.Headers.ContentType =
      New-Object System.Net.Http.Headers.MediaTypeHeaderValue 'application/pdf'
    $form.Add($fileContent, 'File', $d.file)
    $catContent = New-Object System.Net.Http.StringContent @($d.cat)
    $form.Add($catContent, 'Category')

    $resp = $client.PostAsync($uri, $form).Result
    if (-not $resp.IsSuccessStatusCode) {
      $errBody = $resp.Content.ReadAsStringAsync().Result
      Write-Warning "  ! upload $($d.file) fallito ($($resp.StatusCode)): $errBody"
    } else {
      Write-Host "  - $($d.cat): $($d.file)" -ForegroundColor Green
    }
  } finally {
    $client.Dispose(); $handler.Dispose()
  }
}

Remove-Item -Recurse -Force $TmpPdfDir -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------
# 6. (Opzionale) Estendi la deadline 2FA dell'account demo
# ---------------------------------------------------------------------------
# Il RequireTwoFactorForOwnersMiddleware blocca con 403 gli Owner senza 2FA
# oltre la grace (default 7gg). L'account reviewer Apple DEVE restare senza
# 2FA (il reviewer non puo' produrre un TOTP), quindi posticipiamo la
# deadline. Tabella/colonne verificate su UserConfiguration + migration
# AddTwoFactorRequiredFromUtc: tabella `users` (lowercase), colonne PascalCase
# quoted ("Email", "TwoFactorRequiredFromUtc").
$pgContainer = if ($PostgresContainer) { $PostgresContainer } else { $env:ACCANTO_PG_CONTAINER }

$escapedEmail = $Email -replace "'", "''"
$extendSql = @"
UPDATE users
SET ""TwoFactorRequiredFromUtc"" = NOW() AT TIME ZONE 'UTC' + INTERVAL '$TwoFactorGraceDays days'
WHERE ""Email"" = '$escapedEmail';
"@

Write-Host '==> Estensione deadline 2FA' -ForegroundColor Cyan
if ($pgContainer) {
  $pgDb   = if ($env:ACCANTO_PG_DB)   { $env:ACCANTO_PG_DB }   else { $PostgresDb }
  $pgUser = if ($env:ACCANTO_PG_USER) { $env:ACCANTO_PG_USER } else { $PostgresUser }
  if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Warning '  ! docker non nel PATH: salto estensione automatica. SQL manuale piu sotto.'
    $pgContainer = $null
  } else {
    # SQL via STDIN (pattern di scripts/db/restore-drill.ps1): evita il
    # mangling dei doppi apici fatto da PowerShell passando argomenti a docker exec.
    $out = $extendSql | docker exec -i $pgContainer psql -U $pgUser -d $pgDb -tA 2>&1
    if ($LASTEXITCODE -eq 0) {
      Write-Host "  - deadline posticipata di $TwoFactorGraceDays giorni per $Email" -ForegroundColor Green
    } else {
      Write-Warning "  ! UPDATE fallita: $out"
    }
  }
}
if (-not $pgContainer) {
  Write-Host '  - nessun -PostgresContainer: lancia questo SQL a mano sul DB:' -ForegroundColor DarkGray
  Write-Host $extendSql -ForegroundColor Yellow
}

Write-Host "`nSeed completato." -ForegroundColor Cyan
Write-Host "  Account : $Email  (displayName '$DisplayName')"
Write-Host "  Cerchio : $CircleName ($CircleId)"
Write-Host 'Ora puoi lanciare il flow Maestro o il workflow store-screenshots.' -ForegroundColor DarkGray
