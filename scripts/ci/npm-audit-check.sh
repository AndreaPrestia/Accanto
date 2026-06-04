#!/usr/bin/env bash
# Esegue `npm audit --json --omit=dev` e fallisce se ci sono vulnerabilita'
# High o Critical NON listate nella allowlist passata come $1.
#
# Allowlist: file di testo (commenti con #), una GHSA per riga, opzionalmente
# seguita da " # nota umana che spiega perche' e' accettata e fino a quando".
#
# Uso: cd <project> && ../scripts/ci/npm-audit-check.sh ./.npm-audit-allow

set -euo pipefail

ALLOWFILE="${1:-./.npm-audit-allow}"

if [[ -f "$ALLOWFILE" ]]; then
    ALLOWED=$(grep -vE '^\s*(#|$)' "$ALLOWFILE" | awk '{print $1}' | sort -u || true)
else
    ALLOWED=""
fi

# `npm audit --json` exit code:
#   0 -> nessuna vuln
#   1 -> vuln presenti
# Vogliamo l'output sempre.
AUDIT_JSON=$(npm audit --json --omit=dev 2>/dev/null || true)

if [[ -z "$AUDIT_JSON" ]]; then
    echo "::error::npm audit non ha prodotto output JSON"
    exit 2
fi

# Estrai tutte le GHSA con severity high|critical via jq.
# Output: una riga per advisory: "<GHSA>  <severity>  <title>"
FINDINGS=$(echo "$AUDIT_JSON" | jq -r '
    .vulnerabilities
    | to_entries[].value.via[]
    | select(type == "object")
    | select(.severity == "high" or .severity == "critical")
    | "\(.url | sub(".*/"; ""))\t\(.severity)\t\(.title // "n/a")"
' | sort -u)

if [[ -z "$FINDINGS" ]]; then
    echo "OK: nessuna vulnerabilita' High/Critical nelle dipendenze runtime."
    exit 0
fi

echo "Vulnerabilita' High/Critical rilevate:"
echo "$FINDINGS" | column -t -s $'\t'
echo

UNEXPECTED=0
while IFS=$'\t' read -r ghsa sev title; do
    if echo "$ALLOWED" | grep -qxF "$ghsa"; then
        echo "  [allowed] $ghsa ($sev) — tollerata via $ALLOWFILE"
    else
        echo "::error::Vuln NON tollerata: $ghsa ($sev) — $title"
        UNEXPECTED=$((UNEXPECTED + 1))
    fi
done <<< "$FINDINGS"

if [[ $UNEXPECTED -gt 0 ]]; then
    echo
    echo "Per tollerare una nuova advisory aggiungerla a $ALLOWFILE con motivazione + scadenza."
    exit 1
fi

echo
echo "OK: tutte le High/Critical sono in allowlist tracciata."
exit 0
