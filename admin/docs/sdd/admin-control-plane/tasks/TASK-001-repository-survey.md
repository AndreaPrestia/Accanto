# TASK-001 — Repository survey

## Goal

Inspect the repository before implementing the Admin Control Plane.

## Files to create

```text
docs/sdd/admin-control-plane/00-context.md
```

## Files allowed to modify

```text
docs/sdd/admin-control-plane/00-context.md
docs/sdd/admin-control-plane/tasks/TASK-001-repository-survey.md
```

## Files forbidden to modify

```text
backend/**
frontend/**
admin/**
docker-compose.yml
.env.example
```

## Implementation steps

1. Inspect backend project structure.
2. Inspect frontend structure.
3. Identify current auth model.
4. Identify current User model.
5. Identify current DbContext.
6. Identify docker-compose.
7. Identify env configuration.
8. Identify tests.
9. Identify logging and health checks.
10. Update `00-context.md`.

## Tests

No code tests required.

## Acceptance criteria

```text
[x] Backend structure documented
[x] Frontend structure documented
[x] Auth model documented
[x] User model documented
[x] DbContext documented
[x] Docker setup documented
[x] Tests documented
[x] Integration risks documented
[x] No production code modified
```

## Privacy checks

```text
[x] No code changes
[x] No new admin access
```

## Rollback notes

Revert documentation changes only.

---

## Survey outcome (2026-07-27)

Survey completata. I risultati dettagliati sono in `../00-context.md` sezione
"Required repository survey". Sintesi esecutiva:

- **Backend:** 5 progetti .NET 10 (`Accanto.Api/.Application/.Domain/.Infrastructure/.Cli`),
  solution `backend/Accanto.slnx` (formato `.slnx`). Nessun progetto `Accanto.Admin.*`
  presente. Spazio libero per la struttura target.
- **Frontend:** SPA React 19 + Vite 8 + PWA in `frontend/`. Nessuna route admin in
  `App.tsx`. Admin-web sarà app separata (`admin/accanto-admin-web`).
- **Auth pubblica:** JWT Bearer HS256 (multi-key) + refresh token rotanti persistiti
  (hash), 2FA TOTP, lockout, rate limit per-IP. Admin userà signing material separato.
- **User model:** nessun `IsAdmin`, ruoli solo per-care-circle. Già presenti lockout,
  revoke sessioni, GDPR erasure tombstone → base per le "minimal account operations".
- **DB:** un solo Postgres `accanto` (19 tabelle, dual-role migrator/runtime, cifratura
  campi sensibili AES-256-GCM, audit append-only). `AccantoAdminDb` sarà DB separato.
- **Docker:** compose base/override/prod + Caddy edge (network `edge`). Da aggiungere
  `admin-api`, `admin-web`, DB admin, host `admin.<dominio>`.
- **Test/Logging/Health:** xUnit+WebApplicationFactory (factory riusabile), Serilog
  (+Seq opt-in), `/health` `/health/live` `/health/ready`. Pattern replicabili per admin.
- **Integration risks:** 11 rischi documentati in `00-context.md` (solution `.slnx`,
  segregazione DB, no accesso diretto al DB pubblico, JWT separation, cifratura campi,
  build context monorepo, Caddy/ACME, CORS separato, append-only audit, no SW admin,
  no hard delete/impersonation).

Nessuna riga di codice di produzione modificata. Solo i due file di documentazione
(`00-context.md` e questo task) sono stati aggiornati.
