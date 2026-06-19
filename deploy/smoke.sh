#!/usr/bin/env bash
#
# Smoke test post-deploy per Accanto.
#
# Verifica in pochi secondi che dopo un deploy:
#   1. il backend risponde (process + DB raggiungibile)
#   2. il sito vetrina risponde
#   3. (opzionale) la SPA risponde
#   4. (se ci sono credenziali) login + endpoint autenticato funzionano end-to-end
#
# Esce con codice 0 se tutti i check passano, 1 al primo errore.
#
# Uso:
#   deploy/smoke.sh <API_URL> [WEB_URL] [APP_URL]
#
# Esempio:
#   deploy/smoke.sh https://api.accanto.care https://accanto.care https://app.accanto.care
#
# Credenziali opzionali (necessarie per validare anche login + me):
#   export SMOKE_EMAIL='smoke@accanto.care'
#   export SMOKE_PASSWORD='...'
#
# L'utente smoke deve esistere e NON deve avere 2FA attivo.
# Crealo una volta sola con una POST a /auth/register e archivia le credenziali
# in un secret manager (1Password, Bitwarden, GitHub Actions secrets, ...).
#
# Dipendenze: bash 4+, curl, jq.

set -euo pipefail

API_URL="${1:-}"
WEB_URL="${2:-}"
APP_URL="${3:-}"

if [[ -z "$API_URL" ]]; then
  echo "usage: $0 <API_URL> [WEB_URL] [APP_URL]" >&2
  exit 2
fi

# Rimuove eventuale trailing slash.
API_URL="${API_URL%/}"
WEB_URL="${WEB_URL%/}"
APP_URL="${APP_URL%/}"

pass() { printf '  \033[32mOK\033[0m  %s\n' "$1"; }
fail() { printf '  \033[31mKO\033[0m  %s\n' "$1" >&2; exit 1; }

command -v curl >/dev/null || { echo "curl mancante" >&2; exit 2; }
command -v jq   >/dev/null || { echo "jq mancante"   >&2; exit 2; }

echo "▶ Smoke test contro API=$API_URL"

# ------------------------------------------------------------------
# 1. Health readiness (processo + DB)
# ------------------------------------------------------------------
ready_body="$(curl -fsS --max-time 10 "$API_URL/health/ready" || true)"
ready_status="$(echo "$ready_body" | jq -r '.status // empty')"
ready_db="$(echo "$ready_body"     | jq -r '.checks.db // empty')"

[[ "$ready_status" == "ok" ]] || fail "/health/ready non ha restituito status=ok (body: $ready_body)"
[[ "$ready_db"     == "ok" ]] || fail "/health/ready riporta DB non ok (body: $ready_body)"
pass "/health/ready → status=ok, db=ok"

# ------------------------------------------------------------------
# 2. Sito vetrina (opzionale, solo se passi WEB_URL)
# ------------------------------------------------------------------
# Nota: Caddy fa `redir / /it 302` (Astro statica multilingua), quindi
# usiamo -L per seguire il redirect e validare la pagina finale come
# farebbe un browser reale.
if [[ -n "$WEB_URL" ]]; then
  http_code="$(curl -fsSL -o /dev/null -w '%{http_code}' --max-time 10 "$WEB_URL/" || true)"
  [[ "$http_code" == "200" ]] || fail "sito vetrina $WEB_URL/ ha restituito HTTP $http_code"
  pass "sito vetrina $WEB_URL/ → 200"
fi

# ------------------------------------------------------------------
# 3. SPA (opzionale, solo se passi APP_URL)
# ------------------------------------------------------------------
if [[ -n "$APP_URL" ]]; then
  http_code="$(curl -fsSL -o /dev/null -w '%{http_code}' --max-time 10 "$APP_URL/" || true)"
  [[ "$http_code" == "200" ]] || fail "SPA $APP_URL/ ha restituito HTTP $http_code"
  pass "SPA $APP_URL/ → 200"
fi

# ------------------------------------------------------------------
# 4. Login + endpoint autenticato (solo se credenziali smoke fornite)
# ------------------------------------------------------------------
if [[ -n "${SMOKE_EMAIL:-}" && -n "${SMOKE_PASSWORD:-}" ]]; then
  login_payload="$(jq -nc --arg e "$SMOKE_EMAIL" --arg p "$SMOKE_PASSWORD" '{email:$e, password:$p}')"
  login_body="$(curl -fsS --max-time 10 -H 'Content-Type: application/json' \
    -d "$login_payload" "$API_URL/auth/login" || true)"
  token="$(echo "$login_body" | jq -r '.accessToken // empty')"
  [[ -n "$token" ]] || fail "login fallita per $SMOKE_EMAIL (body: $login_body)"
  pass "login $SMOKE_EMAIL → access token ricevuto"

  me_body="$(curl -fsS --max-time 10 -H "Authorization: Bearer $token" "$API_URL/auth/me" || true)"
  me_email="$(echo "$me_body" | jq -r '.email // empty')"
  [[ "$me_email" == "$SMOKE_EMAIL" ]] || fail "/auth/me email mismatch ($me_email != $SMOKE_EMAIL)"
  pass "/auth/me → $me_email"

  circles_code="$(curl -fsS -o /dev/null -w '%{http_code}' --max-time 10 \
    -H "Authorization: Bearer $token" "$API_URL/care-circles" || true)"
  [[ "$circles_code" == "200" ]] || fail "/care-circles ha restituito HTTP $circles_code"
  pass "/care-circles → 200"
else
  echo "  --  SMOKE_EMAIL/SMOKE_PASSWORD non impostati: skip dei check autenticati"
fi

echo "✔ Tutti i check passati."
