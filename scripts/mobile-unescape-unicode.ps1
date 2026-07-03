$ErrorActionPreference = 'Stop'

# Convert literal \uXXXX (and surrogate pairs \uXXXX\uYYYY) in JS/TS/TSX sources
# under mobile/src into their actual Unicode characters. Safe because:
#   - inside JS string literals ('...' / "..."): \u escapes were already
#     evaluated to the same char at parse time -> no behavior change.
#   - inside JSX text (between > and <) and JSX attribute values: the escape
#     was rendered LITERALLY -> this fixes the bug.

$root = Join-Path $PSScriptRoot '..\mobile\src'
$root = (Resolve-Path $root).Path
Write-Host "Scanning $root"

$files = Get-ChildItem -Path $root -Recurse -File -Include *.ts, *.tsx, *.js, *.jsx
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

$pairRx = [regex]'\\u(d[89ab][0-9a-f]{2})\\u(d[c-f][0-9a-f]{2})'
$singleRx = [regex]'\\u([0-9a-fA-F]{4})'

$changed = 0
foreach ($f in $files) {
    $orig = [System.IO.File]::ReadAllText($f.FullName)

    # Surrogate pairs first (emoji etc.)
    $stage1 = $pairRx.Replace($orig, {
        param($m)
        $hi = [Convert]::ToInt32($m.Groups[1].Value, 16)
        $lo = [Convert]::ToInt32($m.Groups[2].Value, 16)
        $cp = (($hi - 0xD800) * 0x400) + ($lo - 0xDC00) + 0x10000
        [Char]::ConvertFromUtf32($cp)
    })

    # Solo BMP escapes
    $final = $singleRx.Replace($stage1, {
        param($m)
        $cp = [Convert]::ToInt32($m.Groups[1].Value, 16)
        [Char]::ConvertFromUtf32($cp)
    })

    if ($final -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $final, $utf8NoBom)
        Write-Host "  fixed: $($f.FullName.Substring($root.Length + 1))"
        $changed++
    }
}

Write-Host "Done. $changed file(s) updated."
