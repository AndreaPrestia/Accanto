# Final verification report — Admin Control Plane

## Date

2026-07-30

## Agent

GitHub Copilot (MoonshotAI: Kimi K3)

## Summary

Implementazione completa del control plane Admin secondo il pacchetto SDD
(`admin/docs/sdd/admin-control-plane/`), task TASK-001 → TASK-011 sul branch
`feat/admin-control-plane` (10 commit). Sistema composto da Admin API, Admin
Frontend, Admin DB e autenticazione admin separati, con operazioni tecniche minime
sugli account, audit log append-only e **zero accesso ai contenuti utente**.

Tutti i vincoli non negoziabili sono rispettati e coperti da test automatici.

## Build results

| Check | Result | Notes |
|---|---:|---|
| Existing backend builds | ✅ | `dotnet build Accanto.slnx` — 0 warning / 0 error (include i 4 progetti Admin) |
| Existing frontend builds | ✅ | `frontend/` `npm run build` OK |
| Admin API builds | ✅ | `Accanto.Admin.Api` compila nella solution |
| Admin frontend builds | ✅ | `admin/accanto-admin-web` `npm run build` OK (dist ~306 kB) |
| Backend tests pass | ✅ | 254/254 (217 `Accanto.Tests` + 37 `Accanto.Admin.Tests`) |
| Frontend tests pass | ⚪ | N/A — nessun test runner configurato per l'admin-web (build OK) |

## Separation results

| Check | Result | Notes |
|---|---:|---|
| Admin API separate | ✅ | `Accanto.Admin.Api` progetto/servizio `accanto-admin-api` dedicato |
| Admin frontend separate | ✅ | `admin/accanto-admin-web`, nessuna route nella PWA pubblica |
| Admin DB separate | ✅ | servizio `postgres-admin`, DB `accanto_admin`, volume `admin-db-data` |
| Admin users not in public Users table | ✅ | entità `AdminUser` in `AccantoAdminDb`, tabella `admin_users` |
| No User.IsAdmin | ✅ | grep: solo commenti che ne attestano l'assenza; nessun campo aggiunto |
| Admin JWT separate | ✅ | sezione `AdminJwt__*` (issuer/audience/chiave distinti da `Jwt__*`) |
| Internal service-to-service auth separate | ✅ | `InternalAdmin__*` / `InternalApp__*`, scheme `InternalAdminScheme` dedicato |

## Privacy boundary results

| Forbidden data | Exposed? | Evidence |
|---|---:|---|
| Care circle names | ❌ No | DTO interni/admin senza `Name`/`Description`; test reflection + serializzazione |
| Timeline title/content | ❌ No | DTO senza `Title`/`Content`/`Tags`; test con seed sensibili non leak |
| Document original filenames | ❌ No | DTO senza `OriginalFileName`; test serializzazione |
| Document storage paths | ❌ No | DTO senza `StoragePath`; test serializzazione |
| Doctor questions | ❌ No | DTO senza `Question`/`AnswerNotes` |
| Shared updates | ❌ No | DTO senza `Content` |
| File content | ❌ No | nessun endpoint di download admin; Admin API senza `IFieldProtector`/master key |

Test di riferimento: `InternalUserMetadataTests` (reflection guard + whitelist
proprietà + serializzazione senza valori sensibili), `AdminAuditQueryServiceTests`
(esclusione payload). L'Admin API non possiede `Encryption__MasterKey` pubblica.

## Security results

| Check | Result | Notes |
|---|---:|---|
| Admin endpoints reject public JWT | ✅ | issuer/audience/chiave distinti; test `InternalAdminEndpointAuthTests` |
| Internal endpoints reject public JWT | ✅ | scheme `InternalAdminScheme`; test 401 per public/admin-frontend/anonymous |
| Reason required for mutating actions | ✅ | `AdminValidationException` (422) su reason vuota; test theory su tutte le op |
| Audit log written for mutating actions | ✅ | `Admin.Login/Logout`, `User.Disable/Enable/RevokeSessions/StartDeletion`; test |
| Refresh tokens stored hashed | ✅ | SHA-256 hex, mai raw; test `Refresh_token_is_stored_hashed_not_raw` |
| SecurityAuditor cannot mutate users | ✅ | `AdminForbiddenException` (403); test theory su tutte le op mutative |

## Files changed

```text
150 files changed, +14113 / -1 (branch feat/admin-control-plane vs main)

backend/src/Accanto.Admin.Domain/          (nuovo)
backend/src/Accanto.Admin.Application/     (nuovo)
backend/src/Accanto.Admin.Infrastructure/  (nuovo, incl. migration InitialAdminCreate)
backend/src/Accanto.Admin.Api/             (nuovo, incl. Dockerfile)
backend/tests/Accanto.Admin.Tests/         (nuovo, 37 test)
admin/accanto-admin-web/                   (nuovo frontend React, incl. Dockerfile/nginx)
docs/admin-system.md                       (nuovo)
admin/docs/sdd/admin-control-plane/        (pacchetto SDD + survey)

Modifiche additive a file esistenti:
- backend/Accanto.slnx                     (registra i progetti Admin + test)
- backend/src/Accanto.Api/Program.cs       (scheme JWT InternalAdminScheme, opt-in)
- backend/src/Accanto.Api/Controllers/InternalAdminUsersController.cs (nuovo)
- backend/src/Accanto.Api/Configuration/InternalAdminOptions.cs (nuovo)
- backend/src/Accanto.Application/Internal/* (metadata + account service)
- backend/src/Accanto.Domain/Entities/User.cs (IsDisabled/DisabledAt/DisabledReason)
- backend/src/Accanto.Infrastructure/.../UserConfiguration.cs + migration AddUserIsDisabled
- backend/src/Accanto.Application/Auth/AuthService.cs (guard login disabilitato)
- docker-compose.yml                       (servizi postgres-admin, accanto-admin-api, accanto-admin-web; env InternalAdmin__*)
- .env.example                             (sezione Admin)
- README.md                                (sezione Admin Control Plane)
```

## Failed checks

```text
Nessuno. Tutti i check automatici verdi.
Note (non fallimenti):
- Frontend admin: nessun test runner unitario configurato (solo build). 
- Build immagini Docker non eseguita in locale (Docker Desktop non avviato);
  validazione limitata a `docker compose config` (EXIT=0).
- Endpoint GET /api/admin/system/technical-logs non implementato: per scelta
  privacy (08-security-model), in v0.1 i log tecnici non sono esposti.
```

## Risks remaining

```text
- Deploy/verifica runtime end-to-end (docker compose up + flusso completo
  login admin → operazione → audit) NON eseguito in questa sessione: richiede
  Docker attivo. Da coprire con la Manual QA del test plan.
- Seed admin in produzione richiede procedura manuale documentata (runbook
  operativo), non automatizzata per scelta di sicurezza.
- docker-compose.prod.yml non include ancora i servizi admin né l'host Caddy
  admin.<dominio>: da aggiungere in fase di deploy produzione (attenzione al
  rate limit ACME sul nuovo host).
- Storage aggregato "total storage used" nella dashboard admin non mostrato
  (metrica non invasiva rinviata, v0.1).
```

## Follow-up recommendations

```text
1. Eseguire la Manual QA del test plan (docker compose up, seed, login, disable/
   enable/revoke, verifica audit/operations/health) su un ambiente con Docker.
2. Aggiungere servizi admin + host admin.<dominio> a docker-compose.prod.yml e
   al Caddyfile prima del deploy produzione (attendere DNS propagato per ACME).
3. Definire la procedura operativa di creazione primo admin in produzione.
4. Valutare test E2E (Playwright) separati per l'admin-web.
5. Valutare una metrica aggregata non invasiva per "total storage" in dashboard.
6. Push del branch feat/admin-control-plane (attualmente commit TASK-005..011
   solo locali) e apertura PR per review.
```

## Final decision

```text
[x] Accepted with follow-ups
[ ] Accepted
[ ] Rejected
```

Motivazione: tutti i vincoli non negoziabili e i check automatici (build, test,
separazione, privacy boundary, security) sono verdi. I follow-up riguardano la
verifica runtime end-to-end e la configurazione di deploy produzione, non il
codice del control plane.
