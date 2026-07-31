# 11 — Acceptance checklist

> Stato verifica: 2026-07-30. Dettagli in `final-verification-report.md`.
> Legenda: [x] verificato · [~] parziale / da verificare a runtime · [ ] non fatto.

## Build and runtime

```text
[x] Existing backend builds
[x] Existing frontend builds
[x] Admin API builds
[x] Admin frontend builds
[x] Tests pass (254/254)
[x] docker-compose config is valid (EXIT=0)
[~] docker-compose starts existing services (config valida; avvio runtime non eseguito in sessione)
[~] docker-compose starts postgres-admin (come sopra)
[~] docker-compose starts accanto-admin-api (come sopra)
[~] docker-compose starts accanto-admin-web (come sopra)
```

## Separation

```text
[x] Admin API is separate
[x] Admin frontend is separate
[x] Admin DB is separate
[x] Admin users are not in public Users table
[x] No User.IsAdmin exists
[x] Public app auth is not reused for admin
[x] Admin JWT settings are separate
[x] Internal service-to-service auth settings are separate
```

## Admin auth

```text
[x] Admin can login
[x] Admin can logout
[x] Admin can refresh token
[x] Admin can call /me
[x] Invalid credentials fail
[x] Inactive admin cannot login
[x] Admin endpoints reject public JWT
[x] Admin endpoints reject unauthenticated requests
```

## User metadata

```text
[x] Admin can list users
[x] Admin can view user detail
[x] User list shows only metadata
[x] User detail shows only metadata
[x] No care circle names are returned
[x] No timeline title/content is returned
[x] No document original filename is returned
[x] No document storage path is returned
[x] No doctor question is returned
[x] No shared update content is returned
```

## User operations

```text
[x] Owner can disable user
[x] Operator can disable user
[x] SecurityAuditor cannot disable user
[x] Owner can enable user
[x] Operator can enable user
[x] Owner can revoke sessions
[x] Operator can revoke sessions
[x] Owner can start deletion request
[x] Operator can start deletion request
[x] All mutating actions require reason
[x] Hard delete is not performed directly by Admin API
```

## Audit and operations

```text
[x] Disable user writes audit log
[x] Enable user writes audit log
[x] Revoke sessions writes audit log
[x] Start deletion writes audit log
[x] Audit log does not contain sensitive payloads
[x] Operations page shows operation status
[x] Failed operations are visible
```

## Privacy

```text
[x] Admin cannot read timeline
[x] Admin cannot read documents
[x] Admin cannot download documents
[x] Admin cannot read doctor questions
[x] Admin cannot read shared updates
[x] Admin cannot read private notes
[x] Admin cannot see original filenames
[x] Admin cannot see care circle names
[x] Admin cannot impersonate users
```

## Frontend

```text
[x] Admin login page exists
[x] Dashboard exists
[x] Users page exists
[x] User detail page exists
[x] Audit logs page exists
[x] Operations page exists
[x] System page exists
[x] Mutating actions use reason modal
[x] UI does not display forbidden fields
```

## Documentation

```text
[x] docs/admin-system.md exists
[x] README or docs mention admin setup
[x] Admin DB setup documented
[x] First admin creation documented
[x] Privacy boundary documented
[x] Internal endpoints documented
[x] Audit log documented
[x] Forbidden admin capabilities documented
```

## Final decision

```text
[ ] Accepted
[x] Accepted with follow-ups
[ ] Rejected
```

## Follow-ups

```text
- Manual QA end-to-end (docker compose up, seed, login, disable/enable/revoke,
  verifica audit/operations/health) su ambiente con Docker attivo.
- Aggiungere servizi admin + host admin.<dominio> a docker-compose.prod.yml e
  Caddyfile prima del deploy produzione (attenzione rate limit ACME).
- Procedura operativa documentata per creazione primo admin in produzione.
- (Opzionale) test E2E Playwright per admin-web; metrica "total storage" dashboard.
- Push branch feat/admin-control-plane (TASK-005..011 solo locali) + PR review.
```
