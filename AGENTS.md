# AGENTS.md — Guida per agenti AI su Accanto

> Questo file è la mappa del repository pensata per gli agenti (GitHub Copilot, Claude, Cursor, Codex, ecc.).
> Per Copilot in VS Code, un riepilogo compatto vive in `.github/copilot-instructions.md`.
> Per approfondimenti umani: `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `docs/`.

## Cos'è Accanto

App web open source (AGPL-3.0) **mobile-first** per i **caregiver familiari**: organizza diario,
documenti, domande per il medico e aggiornamenti attorno a un **Cerchio di cura** (`CareCircle`).
Non è un'app medica. Tono gentile, in **italiano** (lingua di riferimento; UI anche en/es).
Niente telemetria, niente tracker, niente SaaS esterni obbligatori, nessuna AI mandatoria.

## Monorepo — mappa delle aree

| Area                 | Path                              | Stack                                             | Note |
|----------------------|-----------------------------------|---------------------------------------------------|------|
| **Backend API**      | `backend/`                        | .NET 10 / ASP.NET Core, EF Core 10 + Npgsql, PG16 | Clean Architecture, monolite modulare |
| **Admin Control Plane** | `backend/src/Accanto.Admin.*` + `admin/` | .NET 10 API + Vite/React admin web        | DB **dedicato** `accanto_admin`, zero contenuti utente |
| **Frontend SPA/PWA** | `frontend/`                       | Vite + React 18 + TS + Tailwind + PWA             | Chiama l'API cross-origin, JWT bearer |
| **Sito vetrina**     | `web/`                            | Astro (statico, zero JS di default) + Tailwind    | Trilingua it/en/es, servito sull'apex |
| **Mobile**           | `mobile/`                         | React Native + Expo (managed) + EAS + NativeWind  | Feature parity con la PWA |
| **Codice condiviso** | `packages/shared/`                | TypeScript (`@accanto/shared`)                    | Tipi, i18n locales, costanti, dati statici — usato da frontend **e** mobile |
| **Deploy**           | `deploy/`, `docker-compose*.yml`  | Caddy v2 (TLS auto), Docker Compose               | Caddy è l'unico servizio pubblico in prod |
| **Script**           | `scripts/`, `mobile/scripts/`     | PowerShell, Bash                                  | CI, DB, security, smoke, store-screenshots |

### Struttura backend (Clean Architecture)

```
backend/src/
  Accanto.Domain/           # entità ed enum, nessuna dipendenza
  Accanto.Application/      # servizi, DTO, validator, contratti (IAccantoDbContext, IFileStorage, …)
  Accanto.Infrastructure/   # EF Core, PostgreSQL, JWT, PBKDF2, storage locale cifrato, S3 replica
  Accanto.Api/              # ASP.NET Core Web API, controller REST, middleware
  Accanto.Cli/              # tool CLI (es. `accanto smoke-s3`)
  Accanto.Admin.Domain/     # admin control plane (separato dal dominio utente)
  Accanto.Admin.Application/
  Accanto.Admin.Infrastructure/
  Accanto.Admin.Api/
backend/tests/
  Accanto.Tests/            # xUnit + EF InMemory + WebApplicationFactory
  Accanto.Admin.Tests/
```

## Comandi chiave

### Backend (.NET 10)

```sh
cd backend
dotnet build                 # tutti i progetti (soluzione Accanto.slnx)
dotnet test                  # xUnit, tutta la suite
dotnet run --project src/Accanto.Api   # dev (applica migrazioni in auto all'avvio)

# Nuova migrazione EF
dotnet ef migrations add <Nome> --project src/Accanto.Infrastructure --startup-project src/Accanto.Api
```

### Frontend (Vite/React)

```sh
cd frontend
npm install
npm run dev          # dev server
npm run build
npm run e2e          # Playwright (lo stack deve girare: docker compose up -d)
```

### Shared / Mobile / Web

```sh
# dalla root del monorepo (npm workspaces)
npm run build:shared
npm run typecheck:shared

cd mobile && npm run typecheck && npm run test   # jest-expo
cd web && npm run build                          # Astro statico
```

### Docker (stack completo)

```sh
docker compose up --build       # dev: frontend :5173, web :4321, api :8080 (swagger /swagger)
# prod: usa SEMPRE entrambi i file
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## Convenzioni e regole critiche (NON derogabili)

### Dominio / multi-tenancy
- **Ogni query utente DEVE filtrare per `care_circle_id`.** Mai saltare il filtro.
- Ruoli solo a livello di care-circle: `Owner` (Coordinatore) < `Caregiver` < `Viewer` (In ascolto),
  per ordinale (più alto = meno privilegi). Autorizzazione centralizzata in `CareCircleAuthorization`
  (appartenenza + ruolo minimo). **Nessun** ruolo platform-admin nel dominio utente.
- L'admin control plane è un sistema **separato** (DB `accanto_admin` dedicato) e non accede ai
  contenuti utente — vedi ADR in `admin/docs/sdd/admin-control-plane/adr/`.

### Backend
- Entità: enum stringa via `HasConversion<string>()`; `Tags` = `text[]` Postgres nativo.
- Validazione: **FluentValidation** + filtro MVC che restituisce **`422 Unprocessable Entity`** (NON 400).
- Errori tipizzati → HTTP status nel middleware: `NotFoundException`→404, `ForbiddenException`→403,
  `ConflictException`→409, `AppValidationException`→422.
- Auth: JWT bearer (HS256) + PBKDF2 (HMACSHA256, 100k iter). Niente cookie → nessun problema SameSite.
- Storage documenti: cifrato on-disk (AES-256-GCM via `LocalFileStorage` + `IFieldProtector`),
  replica S3 best-effort via outbox (`document_sync_outbox` + `DocumentSyncWorker`). Mai su servizi terzi
  non opzionali. Dettagli: `docs/document-storage.md`.
- Lingua AI: risolta da `Accept-Language` in `AiPromptBuilder.ResolveLanguage` (policy "italo-tollerante":
  it se presente, poi en, poi es, default it). I test in `AiPromptBuilderTests.cs` codificano questa
  precedenza — **non** cambiarla in priority-order.

### Frontend / Mobile / Shared
- **Tutte le stringhe i18n vivono in `packages/shared/src/i18n/locales/{it,en,es}.json`** (namespace per
  schermata). Non hardcodare label di enum: usare `t(\`namespace.${enumValue}\`)`.
- Frontend e mobile condividono tipi/i18n/costanti via `@accanto/shared`.
- La SPA chiama l'API **cross-origin** in prod (`app.*` → `api.*`); l'API pubblica **non** usa il prefisso
  `/api` nel path (Caddy fa il rewrite). Es.: `https://api.accanto.care/auth/login`.

### Processo / contributi
- Commit **Conventional**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `perf`.
  Es.: `feat(timeline): filtro per range data`.
- Branch da `main`, nome breve (`feat/invite-link`, `fix/timeline-date-tz`). Una PR = un cambiamento.
- Se tocchi il backend, aggiungi/aggiorna i test in `backend/tests/Accanto.Tests/`.
- **Mai committare segreti** (chiavi, password, dump DB reali). `.env` è gitignorato.
- Nei bug report, **dati inventati**: mai contenuti reali di diario/documenti/nomi di persone assistite.
- Non accettato: telemetria/analytics/tracker, dipendenze SaaS esterne non opzionali, cambi di licenza
  diversi da AGPL-3.0, feature "smart" che inviano dati medici a terzi senza consenso esplicito.

### Sicurezza
- Vulnerabilità → **non** aprire Issue pubbliche. Vedi `SECURITY.md`.
- CI ha 3 gate audit su PR (npm audit web/frontend, .NET vulnerable packages) ereditati da `main`:
  un `main` rosso rende rosse tutte le PR → sistemare prima `main`.

## Dove guardare per approfondire

- Panoramica prodotto + stack + deploy: `README.md`
- Come contribuire / PR / commit: `CONTRIBUTING.md`
- Sicurezza e disclosure: `SECURITY.md`
- Storage documenti (pipeline cifratura + replica S3): `docs/document-storage.md`
- Admin control plane (SDD completo, data model, security, ADR): `admin/docs/sdd/admin-control-plane/`
- Audit di sicurezza: `docs/security-audit.md`
- Store / pubblicazione mobile: `mobile/README.md`, `mobile/store/`
- Sito vetrina: `web/README.md`
