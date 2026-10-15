# Istruzioni Copilot per Accanto

App web open source (AGPL-3.0) **mobile-first** per i **caregiver familiari**. Organizza diario,
documenti, domande per il medico e aggiornamenti attorno a un **Cerchio di cura** (`CareCircle`).
Non è un'app medica. Tono gentile, lingua di riferimento **italiano** (UI anche en/es).

> Mappa completa del repo + convenzioni estese: vedi [`AGENTS.md`](../AGENTS.md).

## Monorepo — aree

| Area | Path | Stack |
|---|---|---|
| Backend API | `backend/` | .NET 10 / ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 16 (Clean Architecture) |
| Admin control plane | `backend/src/Accanto.Admin.*` + `admin/` | .NET 10 API + Vite/React — DB **dedicato** `accanto_admin`, zero contenuti utente |
| Frontend SPA/PWA | `frontend/` | Vite + React 18 + TS + Tailwind + PWA |
| Sito vetrina | `web/` | Astro statico trilingua it/en/es |
| Mobile | `mobile/` | React Native + Expo (managed) + EAS + NativeWind |
| Codice condiviso | `packages/shared/` (`@accanto/shared`) | TS: tipi, i18n locales, costanti — usato da frontend **e** mobile |
| Deploy | `deploy/`, `docker-compose*.yml` | Caddy v2 (unico servizio pubblico in prod) + Docker Compose |

Backend layering: `Accanto.Domain` (entità/enum, zero dipendenze) → `Accanto.Application`
(servizi/DTO/validator/contratti) → `Accanto.Infrastructure` (EF, PG, JWT, storage cifrato) →
`Accanto.Api` (controller/middleware). Test in `backend/tests/Accanto.Tests` (xUnit + EF InMemory).

## Comandi

```sh
# Backend
cd backend && dotnet build && dotnet test
dotnet run --project src/Accanto.Api                      # dev, migrazioni auto all'avvio
dotnet ef migrations add <Nome> --project src/Accanto.Infrastructure --startup-project src/Accanto.Api

# Frontend / shared / mobile / web
cd frontend && npm run dev            # build: npm run build ; e2e: npm run e2e (serve stack up)
npm run build:shared                  # dalla root (workspaces)
cd mobile && npm run typecheck && npm run test
cd web && npm run build

# Docker (prod: SEMPRE entrambi i file)
docker compose up --build             # dev: frontend :5173, web :4321, api :8080 (/swagger)
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## Regole critiche (NON derogabili)

- **Ogni query utente DEVE filtrare per `care_circle_id`.** Mai saltarlo.
- Ruoli solo per care-circle: `Owner` < `Caregiver` < `Viewer` (per ordinale, più alto = meno privilegi).
  Autorizzazione centralizzata in `CareCircleAuthorization`. Nessun ruolo platform-admin nel dominio utente.
- Validazione backend: **FluentValidation** + filtro MVC → **`422`** (NON 400). Errori tipizzati mappati
  in middleware: `NotFoundException`→404, `ForbiddenException`→403, `ConflictException`→409.
- Entità: enum stringa `HasConversion<string>()`; `Tags` = `text[]` Postgres.
- Auth: JWT bearer (HS256) + PBKDF2. Niente cookie.
- Documenti: cifrati on-disk (AES-256-GCM, `LocalFileStorage` + `IFieldProtector`), replica S3 via outbox
  (`document_sync_outbox` + `DocumentSyncWorker`). Dettagli: `docs/document-storage.md`.
- **i18n**: stringhe in `packages/shared/src/i18n/locales/{it,en,es}.json`. Non hardcodare label enum:
  usare `t(\`namespace.${enumValue}\`)`. Lingua AI risolta da `Accept-Language` (`AiPromptBuilder.ResolveLanguage`,
  precedenza it→en→es→it — non cambiarla).
- API pubblica **senza** prefisso `/api` nel path (Caddy fa il rewrite): es. `https://api.accanto.care/auth/login`.

## Processo

- Commit **Conventional** (`feat`/`fix`/`docs`/`refactor`/`test`/`chore`/`ci`/`perf`). Una PR = un cambiamento.
- Branch da `main` (`feat/...`, `fix/...`). Tocchi il backend → aggiorna i test in `backend/tests/Accanto.Tests/`.
- **Mai committare segreti** (`.env` gitignorato) né dati reali di assistiti; usare dati inventati.
- Vietati: telemetria/tracker, SaaS esterni non opzionali, cambi di licenza ≠ AGPL-3.0, invio dati medici
  a terzi senza consenso esplicito.
- Vulnerabilità → non aprire Issue pubbliche, vedi `SECURITY.md`. CI: 3 gate audit ereditati da `main`
  (un `main` rosso rende rosse le PR → sistemare `main` prima).
