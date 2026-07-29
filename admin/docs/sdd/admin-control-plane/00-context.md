# 00 — Current repository context

## Status

Questo documento è la base di partenza per il sistema **Accanto Admin Control Plane**.

Va completato dal coding agent dopo la repository survey iniziale.  
La parte sottostante contiene il contesto atteso e i vincoli già decisi.

## Product context

Accanto è una PWA open source per familiari e caregiver che stanno affrontando una malattia grave di una persona cara.

L'app pubblica consente di organizzare:

- appunti;
- timeline;
- documenti;
- domande da fare ai medici;
- aggiornamenti per familiari;
- informazioni pratiche.

Questi dati sono altamente sensibili.

## Admin goal

Il sistema Admin deve permettere solo operazioni tecniche minime:

- login admin;
- gestione account utenti tramite metadata;
- disabilitazione/riabilitazione utenti;
- revoca sessioni;
- avvio richiesta cancellazione dati;
- audit log admin;
- log tecnici non sensibili;
- health/status.

## Expected backend structure

La struttura desiderata è:

```text
backend/
  src/
    Accanto.Api/
    Accanto.Application/
    Accanto.Domain/
    Accanto.Infrastructure/

    Accanto.Admin.Api/
    Accanto.Admin.Application/
    Accanto.Admin.Domain/
    Accanto.Admin.Infrastructure/
```

Se il repository reale usa nomi diversi, adattare senza rompere i progetti esistenti.

## Expected frontend structure

La struttura desiderata è:

```text
frontend/
  accanto-web/

admin/
  accanto-admin-web/
```

Il frontend admin non deve essere aggiunto come route interna della PWA pubblica.

## Expected databases

```text
AccantoDb
- dati applicativi pubblici e sensibili degli utenti

AccantoAdminDb
- admin users
- admin roles
- admin sessions
- admin audit logs
- admin operations
```

## Required repository survey

Il coding agent deve completare questa sezione prima del codice.

> **Survey eseguita il 2026-07-27 (TASK-001).** Tutti i percorsi sono relativi alla repo root.

### Backend structure found

Monorepo .NET sotto `backend/`, solution `backend/Accanto.slnx` (formato `.slnx`). Layered architecture (Clean/Onion semplificata), 5 progetti in `backend/src/` + 1 di test:

```text
backend/
  Accanto.slnx
  Dockerfile                      # multi-stage, runtime aspnet:10.0-noble-chiseled (uid 1654)
  src/
    Accanto.Api/                  # ASP.NET Core 10, controller-based + minimal API health. Program.cs, Controllers/, Middleware/, Configuration/, Common/
    Accanto.Application/          # use case / servizi / DTO / validators (FluentValidation). AddAccantoApplication()
    Accanto.Domain/               # Entities/, Enums/ — POCO plain, nessuna logica di persistenza
    Accanto.Infrastructure/       # EF Core, Persistence/ (DbContext+Configurations+Migrations), Security/, Storage/, Audit/, Email/, Push/, Ai/, Export/, Authorization/. AddAccantoInfrastructure(configuration)
    Accanto.Cli/                  # tool console (VAPID gen, erasure admin manuale, smoke-s3)
  tests/
    Accanto.Tests/                # xUnit + WebApplicationFactory + EF InMemory
```

Punti chiave per l'integrazione Admin:

- La solution usa il formato `.slnx` (nuovo): aggiungere i 4 progetti `Accanto.Admin.*` richiede un edit del file `.slnx` (XML), non `.sln`.
- Dependency injection centralizzata: `AddAccantoApplication()` e `AddAccantoInfrastructure(configuration)`. I progetti Admin dovranno avere extension method analoghi (`AddAccantoAdminApplication()`, `AddAccantoAdminInfrastructure()`).
- `Program.cs` della Api pubblica esegue `Database.MigrateAsync()` **all'avvio** con una connection string privilegiata separata (`ConnectionStrings:PostgresMigrator`), poi REVOKE append-only sulle tabelle di audit. Lo stesso pattern è previsto per `AccantoAdminDbContext`.
- Nessun progetto `Accanto.Admin.*` esiste ancora. La struttura target attesa (`Accanto.Admin.Api/.Application/.Domain/.Infrastructure`) è libera e non collide con nulla di esistente.

### Frontend structure found

SPA React 19 + TypeScript + Vite 8 sotto `frontend/`, PWA con Service Worker (`vite-plugin-pwa`, injectManifest, `src/sw.ts`). Routing `react-router-dom` v7.

```text
frontend/
  Dockerfile             # monorepo context=repo root (serve packages/shared), nginx-unprivileged (uid 101) :8080
  vite.config.ts         # plugin react + VitePWA
  src/
    App.tsx              # <Routes> — TUTTE le route pubbliche/PWA (login, register, dashboard, care-circles/*, account, ...)
    api/client.ts        # axios instance, baseURL=VITE_API_BASE_URL (default /api), interceptor Bearer + auto-refresh su 401
    auth/AuthContext.tsx # AuthProvider (React context), token in localStorage chiavi accanto.token/refreshToken/user
    auth/RequireAuth.tsx # route guard
    pages/               # 19 pagine pubbliche
    components/, hooks/, i18n/, lib/, data/, types.ts
```

Punti chiave per l'integrazione Admin:

- **Nessuna route admin esiste** in `App.tsx` — coerente col vincolo "no admin routes inside the public PWA". Il frontend admin sarà un'app React separata (`admin/accanto-admin-web`).
- Il frontend è un **npm workspace** della repo root (`@accanto/shared` in `packages/shared`). Il Dockerfile richiede `context: .` (repo root). Anche l'admin-web dovrà seguire lo stesso pattern monorepo se vuole riusare `@accanto/shared`.
- Auth frontend: token JWT conservato in `localStorage` (chiavi `accanto.*`). L'admin-web dovrà usare chiavi di storage **diverse** (es. `accanto.admin.*`) e un proprio `client.ts` con baseURL verso l'Admin API, per non mescolare i due token.
- Esiste anche `web/` (sito vetrina Astro, statico) e `mobile/` (Expo/React Native): **non vanno toccati**.

### Current auth model

Autenticazione **JWT Bearer stateless + refresh token rotanti**, utenti pubblici.

- **Emissione** (`Accanto.Application/Auth/AuthService.cs`): login con email+password → `IJwtTokenService` emette access token; `IRefreshTokenService` emette refresh token persistito (hash) in tabella `refresh_tokens`.
- **JWT** (`Program.cs` + `JwtOptions.ResolveSigningMaterial`): HS256 (`HmacSha256`, whitelist algoritmi esplicita), `ValidateIssuer/Audience/Lifetime/SigningKey`, issuer+audience = `accanto`, chiave simmetrica da config. **Multi-key** supportato: `Jwt__Keys__<kid>` + `Jwt__ActiveKeyId` con `IssuerSigningKeyResolver` (rotazione zero-downtime, grace per token senza `kid`). ClockSkew 1 min.
- **Refresh rotation** (`RefreshTokenService.cs`): token monouso, rotazione a ogni `/auth/refresh`; rilevamento riuso → revoca di **tutte** le sessioni dell'utente. Persistiti come hash (SHA-256), con `UserAgent`/`IpAddress`, scadenza `Jwt:RefreshTokenExpiryDays`.
- **2FA TOTP** (`Auth/TwoFactor/`): segreto cifrato a riposo via `IFieldProtector`, challenge JWT temporaneo tra password-ok e TOTP-ok, recovery codes hash. Middleware `RequireTwoFactorForOwnersMiddleware` forza 2FA per ruolo Owner (dopo `UseAuthentication`).
- **Lockout**: contatore `FailedLoginAttempts` + `LockoutEndsAt` sul `User` dopo N tentativi (config `Lockout:*`, default 5/15min).
- **Password reset**: token monouso salvato come hash SHA-256 in `password_reset_tokens`, link costruito su `Auth:PasswordReset:PublicUrl`.
- **Rate limiting**: `AddRateLimiter` con policy named (`auth-login`, `auth-register`, `auth-sensitive`, `invite-create`, `ai`, `csp-report`) partizionate per IP o user-id.
- Frontend: `AuthContext` + axios interceptor che allega `Authorization: Bearer <token>` e fa refresh automatico su 401.

**Implicazione Admin:** gli admin NON devono riusare questo modello/JWT. Serve un `AdminJwt` separato (issuer/audience/chiave diversi) e una propria tabella `admin_users` in `AccantoAdminDb`. Il vincolo "non riusare JWT pubblici" è rispettabile solo emettendo token admin da `Accanto.Admin.Api` con signing material dedicato.

### Current user model

`backend/src/Accanto.Domain/Entities/User.cs` — **nessun campo admin/role piattaforma** (conforme al vincolo "no User.IsAdmin").

```csharp
public class User {
  Guid Id;
  string Email;                  // plaintext lowercase, NON cifrata
  string DisplayName;
  string PasswordHash;
  string? Language;
  DateTimeOffset CreatedAt;
  // Lockout
  int FailedLoginAttempts; DateTimeOffset? LockoutEndsAt; DateTimeOffset? LastFailedLoginAt;
  // 2FA TOTP (segreti cifrati via IFieldProtector)
  bool TwoFactorEnabled; string? TwoFactorSecret; string? TwoFactorPendingSecret;
  string? TwoFactorRecoveryCodesJson;           // JSON array di hash SHA-256
  DateTimeOffset? TwoFactorRequiredFromUtc;     // enforcement Owner
  // GDPR erasure tombstone
  bool IsErased; DateTimeOffset? ErasedAt; string? ErasureReason;
}
```

- L'unico concetto di "ruolo" è **per care-circle** (`CareCircleMember.Role` = Owner/Caregiver/Viewer), non piattaforma. Nessun ruolo globale.
- Operazioni utente già possibili lato dominio: lockout (campi presenti), revoca sessioni (`RefreshTokenService.RevokeAllForUserAsync`), GDPR erasure (`UserErasureService`, tombstone `IsErased`/`ErasureReason`). **Queste sono esattamente le "minimal account operations" richieste dall'Admin** — l'Admin API le invocherà tramite internal endpoints, non leggendo direttamente il DB pubblico.
- Vincoli Admin rispettati dal modello attuale: niente `IsAdmin`, admin non nella tabella `users`.

### Current database model

Un solo DB applicativo: **`accanto`** su Postgres 16. DbContext: `Accanto.Infrastructure/Persistence/AccantoDbContext.cs` (implementa `IAccantoDbContext` in Application).

**19 DbSet / tabelle:** `users`, `care_circles`, `care_circle_members`, `care_circle_invites`, `timeline_entries`, `medical_documents`, `doctor_questions`, `shared_updates`, `push_subscriptions`, `audit_log_entries`, `user_notification_preferences`, `device_push_tokens`, `refresh_tokens`, `password_reset_tokens`, `security_audit_log_entries`, `caregiver_check_ins`, `ai_interactions`, `document_sync_outbox`.

Caratteristiche rilevanti:

- **Cifratura a riposo campo-per-campo** via EF `ValueConverter` + `IFieldProtector` (AES-256-GCM): `CareCircle.Description`, `TimelineEntry.Title/Content`, `MedicalDocument.OriginalFileName/Notes`, `DoctorQuestion.Question/AnswerNotes`, `SharedUpdate.Content`, `AiInteraction.Input/Output`. → Questi sono esattamente i contenuti che l'Admin NON deve esporre (nomi care circle, nomi file originali, domande medici, aggiornamenti, timeline).
- **20 migrazioni EF** in `Persistence/Migrations/` (da `Init` 2026-05-21 a `AddDevicePushTokensAndPushPreferences` 2026-06-22). Migrazioni applicate all'avvio.
- **Dual-role Postgres:** runtime `accanto_app` (DML-only, no DDL) vs migrator `accanto` (owner, DDL). Creati da `scripts/db/init/01-app-role.sh` al primo init volume. Append-only DB-level su `audit_log_entries` + `security_audit_log_entries` (REVOKE UPDATE/DELETE per `accanto_app`).
- `IAccantoDbContext` espone tutti i DbSet sensibili → l'Admin non deve mai iniettare questo contratto.

**Implicazione Admin:** `AccantoAdminDb` sarà un DB fisico separato con proprio `AccantoAdminDbContext`, proprie migration e propri ruoli Postgres. Riusare il pattern dual-role + append-only per `admin_audit_log`.

### Current docker setup

Stack multi-servizio definito in `docker-compose.yml` (base) + `docker-compose.override.yml` (dev auto) + `docker-compose.prod.yml` (prod).

Servizi nel base:
- `db` — postgres:16-alpine, volume `db-data`, init script `scripts/db/init`, healthcheck `pg_isready`, porta host 5432 (rimossa in prod).
- `storage-init` — busybox one-shot, chown `1654:1654` su `./storage` (bind mount `/data/storage`).
- `backend` — build `backend/Dockerfile` (context `.`), uid 1654 chiseled, hardened (`no-new-privileges`, `cap_drop ALL`, `read_only`, tmpfs), porta host 8080, dipende da `db`(healthy)+`storage-init`.
- `frontend` — build `frontend/Dockerfile` (context `.` monorepo), nginx-unprivileged :8080→host 5173, hardened.
- `web` — sito Astro statico, :8080→host 4321.
- Profili opt-in: `ollama` (AI), `seq` (observability), `clamav` (av).

Prod (`docker-compose.prod.yml`): immagini GHCR, porte interne resettate, aggiunge `caddy` (build `deploy/caddy`, immagine `accanto-caddy:latest`) come unico entrypoint TLS 80/443, 3 host pubblici (apex vetrina, `app.` SPA, `api.` API). Network di default nominata **`edge`** per siti satellite.

**Implicazione Admin:** servono 2 nuovi servizi (`admin-api`, `admin-web`) + 1 nuovo DB (`admin-db` o database separato sullo stesso Postgres) + entrypoint Caddy dedicato (es. `admin.`). Vanno aggiunti sia a `docker-compose.yml` sia a `.prod.yml` con nuovo host pubblico e nuova sezione Caddyfile. Nessun servizio admin esiste oggi.

### Current tests

`backend/tests/Accanto.Tests/` — xUnit, ~40 file di test.

- `AccantoFactory.cs` — `WebApplicationFactory<Program>` con env `Testing`, sostituisce EF con `UseInMemoryDatabase`, config JWT/Encryption/RateLimit permissivi.
- `TestDb.cs` — helper per `AccantoDbContext` in-memory con `NullFieldProtector`.
- Helper NoOp: `NoOpAuditLog`, `NoOpSecurityAuditLog`, `NoOpRefreshTokenService`, `NoOpTwoFactorService`, `NoOpMalwareScanner`, `NoOpPushService`, ecc.
- Copertura: auth (lockout, 2FA, refresh, JWT signing), servizi (care circle, timeline, document, invite, export, erasure), endpoint (smoke, AI, CSP report), security (field protector, password hasher, rate limit).
- Frontend: Playwright E2E in `frontend/e2e/` (a11y, auth, 2FA, lockout, care-circle, invite, sessions, wellbeing, ...).

**Nota gotcha:** il custom `ValidationFilter` ritorna **422**, non 400 (i test devono asserire 422). FluentAssertions ≥7: `HaveCountGreaterThanOrEqualTo`.

**Implicazione Admin:** serve un nuovo progetto `Accanto.Admin.Tests` con factory analoga (`AccantoAdminFactory`) + E2E Playwright separati per l'admin-web. Pattern InMemory già collaudato e riusabile.

### Current logging

- **Serilog** configurato in `Program.cs` via `builder.Host.UseSerilog`. Minimum level Information; override Warning per `Microsoft.AspNetCore` e `Microsoft.EntityFrameworkCore`. Enrich: `Application=accanto-api`, `Environment`.
- Sink: console human-readable in dev, **JSON compatto** (`CompactJsonFormatter`) in prod. Sink **Seq** opt-in via `Logging:SeqUrl`/`Logging:SeqApiKey` (profilo `observability`).
- `app.UseSerilogRequestLogging` con enrichment `ClientIp`/`UserAgent` sulla summary line.
- `LogContextEnrichmentMiddleware` (dopo `UseAuthentication`) pusha `UserId`/`ClientIp`/`RequestId` nel LogContext → ereditati da tutti i log della request.
- Audit applicativo a due canali persistiti su DB: `AuditLogEntry` (eventi di dominio per care-circle, visibili agli utenti) e `SecurityAuditLogEntry` (eventi di sicurezza piattaforma), entrambi append-only a livello DB.

**Implicazione Admin:** l'Admin API userà Serilog con `Application=accanto-admin-api` e un **terzo canale** `admin_audit_log` nel DB admin separato. Ogni azione mutativa admin deve scrivere qui (vincolo).

### Current health checks

Minimal API in `Program.cs` (tutte `AllowAnonymous`):

- `GET /health` e `GET /health/live` → liveness, sempre 200 (processo su).
- `GET /health/ready` → readiness: `db.Database.CanConnectAsync()`, 200 `ok` o 503 `degraded` con payload `{status, version, uptimeSeconds, checks:{db}}`.
- `GET /api` → root info `{name:"accanto-api", version, docs, health}`.
- Dockerfile backend: `HEALTHCHECK` (liveness via /health). Compose `db` healthcheck `pg_isready`. Nessun healthcheck compose sul backend (immagine chiseled senza curl/wget).
- Caddy espone `/health*` senza prefisso `/api`.

**Implicazione Admin:** l'Admin API esporrà `/health/live` e `/health/ready` (probing `AccantoAdminDb` + reachability dell'internal app API), host `admin.` dedicato in Caddy.

### Integration risks

1. **Formato solution `.slnx`.** Aggiungere 4 progetti `Accanto.Admin.*` richiede edit manuale dell'XML `.slnx` (non standard `.sln`). Rischio basso ma da non dimenticare nella CI.
2. **Segregazione DBAdmin.** `AccantoAdminDb` deve essere un database fisico separato con propri utenti/ruoli Postgres. Se si riusa lo stesso cluster Postgres (servizio `db`), serve un nuovo database + nuova coppia di ruoli (`accanto_admin` migrator / `accanto_admin_app` DML-only) e un init script dedicato. Mai dare al runtime admin grant su `accanto` pubblico (e viceversa).
3. **Nessun accesso diretto al DB pubblico.** L'Admin non deve iniettare `IAccantoDbContext`/`AccantoDbContext` né referenziare `Accanto.Infrastructure`. Le operazioni su utenti pubblici devono passare per **internal endpoints** di `Accanto.Api` protetti da service-to-service auth (ADR-0004). Rischio: tentazione di leggere direttamente → violazione privacy boundary.
4. **JWT separation.** Riutilizzare `Jwt__Key`/issuer `accanto` per gli admin violerebbe il vincolo. Serve signing material indipendente (issuer/audience/chiave `accanto-admin`) così un token pubblico NON è valido sugli endpoint admin e viceversa. Attenzione a non condividere la config `Jwt` tra le due API.
5. **Field-encrypted content.** Molti campi sensibili sono cifrati a riposo via `IFieldProtector` con `Encryption__MasterKey` dell'app pubblica. L'Admin NON deve avere quella chiave né i converter → non può decifrare contenuti nemmeno per errore. Gli internal endpoints devono ritornare **solo metadata** (id, email, date, conteggi), mai campi cifrati decifrati.
6. **Monorepo frontend build context.** L'admin-web, se aggiunto come npm workspace, impatta il `frontend/Dockerfile` (context=repo root) e i workflow release (matrix `context: .`). Va creato un `admin/Dockerfile` con lo stesso vincolo di context root.
7. **Caddy edge + ACME.** Nuovo host pubblico `admin.<dominio>`: attendere DNS propagato prima del reload Caddy (rate limit ACME 5 fail/h/hostname). Rate-limit edge dedicato sulle route admin.
8. **CORS separato.** L'admin-web gira su origin diversa → la Admin API deve avere una propria allowlist CORS (`AdminCors__AllowedOrigins`), distinta da quella pubblica.
9. **Append-only audit.** Replicare il REVOKE UPDATE/DELETE per `admin_audit_log` sul ruolo runtime admin. Attenzione: lo script attuale di revoke è hardcoded su `security_audit_log_entries`/`audit_log_entries` nel `Program.cs` pubblico — quello admin va nel `Program.cs` di `Accanto.Admin.Api` con la sua tabella.
10. **Service Worker della PWA.** Non toccare la PWA pubblica; l'admin-web non deve registrare SW aggressivi che cachino risposte admin (rischio info leak su client condivisi).
11. **Nessun hard delete / impersonation.** Le operazioni disponibili oggi (erasure GDPR tombstone, revoke sessioni, lockout) sono coerenti: l'Admin le orchestra ma **non** implementa hard delete né login-as-user. Il GDPR `UserErasureService` esistente fa tombstone, non hard delete — allineato al vincolo.

## Hard constraints

- Non aggiungere `User.IsAdmin`.
- Non usare la tabella utenti pubblica per gli admin.
- Non usare lo stesso JWT degli utenti normali.
- Non esporre contenuti utente.
- Non implementare impersonificazione.
- Non implementare download documenti da admin.
