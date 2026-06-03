# Probe upload hardening per Accanto.
#
# Verifica che il documento upload rifiuti:
#   1. file con content-type spoofed (PNG bytes ma Content-Type=application/pdf)
#   2. file con content-type non in allowlist (application/x-msdownload)
#   3. file con magic bytes "binari" ma dichiarato text/plain
# e accetti:
#   4. un PDF reale dichiarato application/pdf
#   5. un testo ASCII reale dichiarato text/plain
#
# Uso (stack attivo su http://localhost:8080):
#   pwsh scripts/security/upload-probe.ps1
#
# Exit 0 = tutte le aspettative rispettate.
# Exit 1 = almeno una probe ha FAIL.

param([string]$BaseUrl = "http://localhost:8080/api")

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

$results = [System.Collections.Generic.List[object]]::new()

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
        $code = [int]$resp.StatusCode
        $body = ""
        try {
            $s = $resp.GetResponseStream(); $sr = New-Object IO.StreamReader($s)
            $body = $sr.ReadToEnd()
        } catch {}
        return [pscustomobject]@{ StatusCode = $code; Content = $body }
    }
}

function Upload-File {
    param(
        [string]$Url, [hashtable]$Headers,
        [byte[]]$Bytes, [string]$FileName, [string]$DeclaredContentType,
        [string]$Category = 'Other'
    )
    $boundary = "----accantoprobe$([Guid]::NewGuid().ToString('n'))"
    $LF = "`r`n"
    $enc = [Text.Encoding]::UTF8

    $pre = "--$boundary$LF" +
           "Content-Disposition: form-data; name=`"File`"; filename=`"$FileName`"$LF" +
           "Content-Type: $DeclaredContentType$LF$LF"
    $sep = "$LF--$boundary$LF" +
           "Content-Disposition: form-data; name=`"Category`"$LF$LF$Category$LF" +
           "--$boundary--$LF"

    $body = New-Object System.IO.MemoryStream
    $b1 = $enc.GetBytes($pre); $body.Write($b1, 0, $b1.Length)
    $body.Write($Bytes, 0, $Bytes.Length)
    $b2 = $enc.GetBytes($sep); $body.Write($b2, 0, $b2.Length)
    $bodyBytes = $body.ToArray()

    $H = $Headers.Clone()
    $H['Content-Type'] = "multipart/form-data; boundary=$boundary"

    try {
        return Invoke-WebRequest -Uri $Url -Method Post -Headers $H -Body $bodyBytes -ErrorAction Stop -UseBasicParsing
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $code = [int]$resp.StatusCode
        $b = ""
        try { $s = $resp.GetResponseStream(); $sr = New-Object IO.StreamReader($s); $b = $sr.ReadToEnd() } catch {}
        return [pscustomobject]@{ StatusCode = $code; Content = $b }
    }
}

# Setup: utente + cerchio.
$email = "upload-probe+$([Guid]::NewGuid().ToString('n'))@accanto.local"
$reg = Invoke-Api -Method Post -Path "/auth/register" -Body @{
    email = $email; displayName = "up"; password = "Probe-Pass-12345!"
}
if ($reg.StatusCode -ne 200) { throw "register fallita: $($reg.StatusCode)" }
$auth = $reg.Content | ConvertFrom-Json
$H = @{ Authorization = "Bearer $($auth.accessToken)" }

$circle = (Invoke-Api -Method Post -Path "/care-circles" -Headers $H -Body @{ name='up'; description='up' }).Content | ConvertFrom-Json
$uploadUrl = "$BaseUrl/care-circles/$($circle.id)/documents"

$pngHead = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x01,0x02,0x03)
$pdfHead = [byte[]]@(0x25,0x50,0x44,0x46,0x2D,0x31,0x2E,0x37,0x0A,0x25,0x62) + ([Text.Encoding]::ASCII.GetBytes("ody`n"))
$ascii   = [Text.Encoding]::ASCII.GetBytes("Hello world`n")

function Probe {
    param($Label, $Expected, $Status)
    $hit = ($Expected -contains $Status)
    $results.Add([pscustomobject]@{
        Label    = $Label
        Expected = ($Expected -join '/')
        Got      = $Status
        Result   = if ($hit) { 'PASS' } else { 'FAIL' }
    })
}

Write-Host "==> Probe upload hardening" -ForegroundColor Cyan

# 1. PNG bytes declared as application/pdf  -> 422 atteso
$r = Upload-File -Url $uploadUrl -Headers $H -Bytes $pngHead -FileName 'fake.pdf' -DeclaredContentType 'application/pdf'
Probe -Label 'spoof: PNG bytes as application/pdf' -Expected @(422,400) -Status $r.StatusCode

# 2. PDF bytes declared as application/x-msdownload  -> 422 (content-type non in allow-list)
$r = Upload-File -Url $uploadUrl -Headers $H -Bytes $pdfHead -FileName 'malware.exe' -DeclaredContentType 'application/x-msdownload'
Probe -Label 'allowlist: application/x-msdownload rifiutato' -Expected @(422,400) -Status $r.StatusCode

# 3. PNG bytes declared as text/plain  -> 422 (magic non-testuali)
$r = Upload-File -Url $uploadUrl -Headers $H -Bytes $pngHead -FileName 'bad.txt' -DeclaredContentType 'text/plain'
Probe -Label 'spoof: PNG bytes as text/plain' -Expected @(422,400) -Status $r.StatusCode

# 4. PDF reale -> 201 atteso
$r = Upload-File -Url $uploadUrl -Headers $H -Bytes $pdfHead -FileName 'ok.pdf' -DeclaredContentType 'application/pdf'
Probe -Label 'happy: PDF reale accettato' -Expected @(201,200) -Status $r.StatusCode

# 5. Testo ASCII reale -> 201 atteso
$r = Upload-File -Url $uploadUrl -Headers $H -Bytes $ascii -FileName 'ok.txt' -DeclaredContentType 'text/plain'
Probe -Label 'happy: text/plain reale accettato' -Expected @(201,200) -Status $r.StatusCode

Write-Host ""
$results | Format-Table Label, Expected, Got, Result -AutoSize

$fails = @($results | Where-Object Result -eq 'FAIL')
if ($fails.Count -gt 0) {
    Write-Host "UPLOAD HARDENING FAIL ($($fails.Count) probe)" -ForegroundColor Red
    exit 1
}
Write-Host "Upload hardening OK: spoof/allowlist rifiutati, happy path accettati." -ForegroundColor Green
exit 0
