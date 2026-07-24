#!/usr/bin/env bash
# Hard-delete di un care circle DEMO (e di tutti i dati collegati) da Postgres.
#
# ⚠️  SOLO PER CERCHI DEMO/TEST. Per un cerchio reale usa l'API
#     DELETE /care-circles/{id} (soft-archive, dati conservati per compliance).
#
# Uso (sul server, nella cartella con docker-compose.yml):
#   ./scripts/db/delete-circle.sh --name "Famiglia Rossi"           # dry-run (conteggi)
#   ./scripts/db/delete-circle.sh --name "Famiglia Rossi" --apply   # cancella davvero
#   ./scripts/db/delete-circle.sh --id <uuid> --apply
#
# Opzioni:
#   --name <nome>   Nome esatto del cerchio (default: cerca per --id)
#   --id <uuid>     Id del cerchio
#   --apply         Esegue il DELETE (senza: solo dry-run con conteggi)
#   -f <compose>    Path al docker-compose.yml (default: ./docker-compose.yml)
#
# Auth: nessuna password richiesta — dentro il container Postgres l'utente
# owner e' trusted via local (pattern gia' usato da restore-drill.ps1).
#
# Nota: il DELETE sul DB NON rimuove i file cifrati in storage/YYYY/MM/ ne'
# le repliche S3 (accanto-docs). Lo script elenca i StoragePath PRIMA del
# delete cosi' puoi pulirli a mano se serve. Per i cerchi demo i file sono
# finti: gli orfani sono accettabili.

set -euo pipefail

NAME=""
ID=""
APPLY=0
COMPOSE="./docker-compose.yml"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --name)  NAME="$2"; shift 2 ;;
    --id)    ID="$2"; shift 2 ;;
    --apply) APPLY=1; shift ;;
    -f)      COMPOSE="$2"; shift 2 ;;
    -h|--help)
      sed -n '2,20p' "$0"; exit 0 ;;
    *) echo "Argomento sconosciuto: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$NAME" && -z "$ID" ]]; then
  echo "Errore: specifica --name o --id" >&2
  exit 2
fi

# Escape apici singoli per sicurezza SQL
esc() { printf '%s' "$1" | sed "s/'/''/g"; }

if [[ -n "$ID" ]]; then
  WHERE="\"Id\" = '$(esc "$ID")'::uuid"
else
  WHERE="\"Name\" = '$(esc "$NAME")'"
fi

# ---------------------------------------------------------------------------
# 1. Risolvi il cerchio
# ---------------------------------------------------------------------------
CIRCLE_JSON=$(docker compose -f "$COMPOSE" exec -T db \
  bash -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA' <<SQL
SELECT "Id" || ' | ' || "Name" || ' | ' || "Status"
FROM care_circles WHERE $WHERE;
SQL
)

if [[ -z "$CIRCLE_JSON" ]]; then
  echo "Nessun cerchio trovato con: $WHERE" >&2
  exit 1
fi

N_MATCHES=$(printf '%s\n' "$CIRCLE_JSON" | grep -c .)
if [[ "$N_MATCHES" -gt 1 ]]; then
  echo "Piu' di un cerchio corrisponde ($N_MATCHES) — usa --id:" >&2
  printf '%s\n' "$CIRCLE_JSON" >&2
  exit 1
fi

CIRCLE_ID=$(printf '%s' "$CIRCLE_JSON" | cut -d'|' -f1 | tr -d ' ')
echo "Cerchio: $CIRCLE_JSON"
echo "Id     : $CIRCLE_ID"

# ---------------------------------------------------------------------------
# 2. Conteggi + path documenti (sempre, anche in dry-run)
# ---------------------------------------------------------------------------
docker compose -f "$COMPOSE" exec -T db \
  bash -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
\pset footer off
SELECT 'timeline_entries'    AS tabella, COUNT(*) FROM timeline_entries    WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'doctor_questions',           COUNT(*) FROM doctor_questions    WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'shared_updates',             COUNT(*) FROM shared_updates      WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'medical_documents',          COUNT(*) FROM medical_documents   WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'care_circle_invites',        COUNT(*) FROM care_circle_invites WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'ai_interactions',            COUNT(*) FROM ai_interactions     WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'audit_log_entries',          COUNT(*) FROM audit_log_entries   WHERE "CareCircleId" = '$CIRCLE_ID'
UNION ALL SELECT 'care_circle_members',        COUNT(*) FROM care_circle_members WHERE "CareCircleId" = '$CIRCLE_ID'
ORDER BY 1;

SELECT '--- StoragePath documenti (file da pulire a mano se serve) ---' AS info;
SELECT "StoragePath" FROM medical_documents WHERE "CareCircleId" = '$CIRCLE_ID';
SQL

# ---------------------------------------------------------------------------
# 3. Delete (solo con --apply)
# ---------------------------------------------------------------------------
if [[ "$APPLY" -ne 1 ]]; then
  echo ""
  echo "DRY-RUN: nessuna modifica. Rilancia con --apply per cancellare."
  exit 0
fi

echo ""
read -r -p "Confermi l'hard delete di '$CIRCLE_JSON'? [s/N] " ans
if [[ "${ans,,}" != "s" && "${ans,,}" != "y" ]]; then
  echo "Annullato."
  exit 0
fi

docker compose -f "$COMPOSE" exec -T db \
  bash -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
BEGIN;
CREATE TEMP TABLE _c AS SELECT '$CIRCLE_ID'::uuid AS "Id";

DELETE FROM document_sync_outbox o USING medical_documents d, _c
  WHERE o."DocumentId" = d."Id" AND d."CareCircleId" = _c."Id";
DELETE FROM timeline_entries    WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM doctor_questions    WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM shared_updates      WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM medical_documents   WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM care_circle_invites WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM ai_interactions     WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM audit_log_entries   WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM care_circle_members WHERE "CareCircleId" IN (SELECT "Id" FROM _c);
DELETE FROM care_circles        WHERE "Id"           IN (SELECT "Id" FROM _c);

DROP TABLE _c;
COMMIT;
SQL

echo ""
echo "Hard delete completato per '$CIRCLE_JSON'."
echo "Ricorda: eventuali file in storage/YYYY/MM/ e repliche S3 restano orfani (ok per demo)."
