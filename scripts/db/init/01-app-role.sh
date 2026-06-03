#!/bin/sh
# Bootstrap del ruolo runtime "accanto_app" (DML only) separato dal ruolo
# proprietario/migratore "accanto" (DDL).
#
# Eseguito automaticamente da `postgres:16-alpine` su PRIMA inizializzazione
# del volume `db-data` (cartella /docker-entrypoint-initdb.d). Se il volume
# esiste gia' lo script NON viene rieseguito; per applicarlo a un dev DB
# pre-esistente: `docker compose down -v` e ricreare lo stack.
#
# Variabili attese (gia' iniettate da docker-compose.yml):
#   POSTGRES_DB              database target (default: accanto)
#   POSTGRES_USER            ruolo proprietario/migratore (default: accanto)
#   POSTGRES_APP_PASSWORD    password ruolo runtime accanto_app
#
# Strategia privileges:
#   - accanto_app NON puo' CREATE/ALTER/DROP table (DDL). Solo DML.
#   - GRANT esplicito su tabelle/sequenze gia' esistenti al momento dell'init.
#   - ALTER DEFAULT PRIVILEGES per le future tabelle create dal migrator
#     (`accanto`), cosi' i futuri `dotnet ef database update` non richiedono
#     rerun dello script.

set -eu

if [ -z "${POSTGRES_APP_PASSWORD:-}" ]; then
    echo "[init/01-app-role] POSTGRES_APP_PASSWORD non impostata: salto la creazione del ruolo runtime"
    echo "[init/01-app-role] (l'app continuera' a usare POSTGRES_USER privilegiato)"
    exit 0
fi

DB="${POSTGRES_DB:-accanto}"
OWNER="${POSTGRES_USER:-accanto}"

echo "[init/01-app-role] creazione ruolo accanto_app + grants DML su database $DB"

# psql `-v app_password=...` + `:'app_password'` produce un literal SQL
# correttamente quoted lato client, evita problemi di escape in here-doc.
psql \
    -v ON_ERROR_STOP=1 \
    -v app_password="$POSTGRES_APP_PASSWORD" \
    -v owner="$OWNER" \
    -v dbname="$DB" \
    --username "$OWNER" \
    --dbname   "$DB" \
    <<'SQL'
-- Le variabili psql `:'var'` vengono sostituite client-side prima del send.
-- Funzionano in plain SQL ma NON dentro DO/dollar-quoted blocks; usiamo
-- quindi \gexec con select condizionali per gestire create-vs-update.
SELECT format('CREATE ROLE accanto_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'accanto_app')
\gexec

SELECT format('ALTER ROLE accanto_app WITH LOGIN PASSWORD %L', :'app_password')
WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'accanto_app')
\gexec

GRANT CONNECT ON DATABASE :"dbname" TO accanto_app;
GRANT USAGE  ON SCHEMA public TO accanto_app;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA public TO accanto_app;
GRANT USAGE, SELECT, UPDATE
    ON ALL SEQUENCES IN SCHEMA public TO accanto_app;

ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO accanto_app;
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" IN SCHEMA public
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO accanto_app;

-- Niente CREATE sullo schema: blocca CREATE TABLE/INDEX dal runtime.
REVOKE CREATE ON SCHEMA public FROM accanto_app;
-- Su Postgres 15+ il role PUBLIC non ha CREATE su public di default,
-- ma lo esplicitiamo per chiarezza/regressione su immagini base diverse.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL

echo "[init/01-app-role] OK"
