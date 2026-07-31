# TASK-012 — Admin password reset + password-less seed

## Goal

Introdurre un flusso di **reset password admin** self-contained e cambiare il
**seed** in modo che provisioni gli admin **senza password** (solo email,
display name, ruoli). L'admin imposta la propria password tramite il flusso di
reset. Il seed viene abilitato **anche in produzione** (non più solo Development).

## Decisioni (concordate)

- **Seed senza password**: `AdminUser` creato con `PasswordHash` vuoto e
  `IsActive = true`. Login impossibile finché il reset non imposta un hash.
- **Multipli admin da config**: `AdminSeed__Admins` = **JSON array**
  `[{ "email": "...", "displayName": "...", "role": "Owner|Operator|SecurityAuditor" }]`.
  `displayName` opzionale (fallback: prefisso email), `role` opzionale (default `Owner`).
- **Seed in produzione**: rimosso il gate `IsDevelopment()`. Idempotente: i ruoli
  canonici vengono garantiti sempre; ogni admin in `Admins` viene creato solo se
  non esiste già un admin con quella email.
- **Reset link base**: nuova sezione `AdminAuth__PasswordReset__PublicUrl`
  (URL dell'admin-web, es. `https://admin.accanto.care`), fallback al primo origin
  di `AdminCors__AllowedOrigins`. `ResetPath` default `/reset-password`,
  `TokenLifetimeMinutes` default 60.
- **Email admin**: infrastruttura SMTP **copiata** dentro i progetti admin
  (nessun riferimento a `Accanto.Infrastructure` pubblica). Config `AdminEmail__*`.

## Vincoli non negoziabili (invariati)

- Nessun `User.IsAdmin`; admin restano fuori dalla tabella `users` pubblica.
- DB admin separato: il token di reset admin vive in `AccantoAdminDb`, MAI nel DB pubblico.
- Nessun contenuto utente coinvolto (solo email/displayName admin).
- Il token di reset è salvato **solo come hash SHA-256** (mai il valore raw).
- Il reset **revoca tutte le sessioni admin** dell'utente.
- Anti-enumerazione: `forgot-password` risponde sempre 204, esista o no l'email.
- Ogni evento rilevante scrive **audit log admin** (`Admin.PasswordResetRequested`,
  `Admin.PasswordResetCompleted`).
- Nessuna registrazione pubblica admin (il reset non crea account: agisce solo su
  admin già seedati).

## Files to create

```text
backend/src/Accanto.Admin.Domain/Entities/AdminPasswordResetToken.cs
backend/src/Accanto.Admin.Infrastructure/Persistence/Configurations/AdminPasswordResetTokenConfiguration.cs
backend/src/Accanto.Admin.Infrastructure/Migrations/<ts>_AddAdminPasswordResetTokens.cs (+ Designer)
backend/src/Accanto.Admin.Infrastructure/Email/AdminEmailOptions.cs
backend/src/Accanto.Admin.Infrastructure/Email/AdminEmailSender.cs
backend/src/Accanto.Admin.Application/Email/IAdminEmailSender.cs
backend/src/Accanto.Admin.Application/Auth/AdminPasswordResetOptions.cs
backend/src/Accanto.Admin.Application/Auth/AdminPasswordResetService.cs
backend/src/Accanto.Admin.Application/Auth/IAdminPasswordResetService.cs
admin/accanto-admin-web/src/pages/ForgotPasswordPage.tsx
admin/accanto-admin-web/src/pages/ResetPasswordPage.tsx
backend/tests/Accanto.Admin.Tests/AdminPasswordResetServiceTests.cs
backend/tests/Accanto.Admin.Tests/AdminSeedTests.cs
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Domain/**
backend/src/Accanto.Admin.Application/**       (DbContext contract, DI, DTO)
backend/src/Accanto.Admin.Infrastructure/**    (DbContext, DI, email, packages)
backend/src/Accanto.Admin.Api/**               (Program.cs DI, AdminAuthController, AdminSeed)
admin/accanto-admin-web/**                      (routes, api client, pages)
.env.example
docker-compose.yml                              (env AdminEmail__*, AdminAuth__PasswordReset__*)
docs/admin-system.md
backend/tests/Accanto.Admin.Tests/**
```

## Files forbidden to modify

```text
backend/src/Accanto.Domain/**
backend/src/Accanto.Api/**                      (la app pubblica non c'entra col reset admin)
frontend/**
```

## Implementation steps

### Domain + DB

1. `AdminPasswordResetToken` (Id, AdminUserId, TokenHash, CreatedAt, ExpiresAt,
   UsedAt, IpAddress, UserAgent) + relazione `AdminUser`.
2. EF configuration: tabella `admin_password_reset_tokens`, indice su `TokenHash`,
   FK `AdminUserId` cascade.
3. Aggiungere `DbSet<AdminPasswordResetToken>` a `AccantoAdminDbContext` e a
   `IAccantoAdminDbContext`.
4. Migration `AddAdminPasswordResetTokens` (design-time factory già presente).

### Email (copiata, self-contained)

5. `IAdminEmailSender` (Application) — `IsConfigured` + `SendAsync`.
6. `AdminEmailOptions` + `AdminEmailSender` (Infrastructure, MailKit). Aggiungere
   `MailKit`/`MimeKit` alle package reference di `Accanto.Admin.Infrastructure`.
7. Registrazione DI in `AddAccantoAdminInfrastructure` (sezione `AdminEmail`).

### Application

8. `AdminPasswordResetOptions` (PublicUrl, ResetPath, TokenLifetimeMinutes).
9. `AdminPasswordResetService`:
   - `RequestResetAsync(email, client)` → anti-enumerazione, genera token URL-safe,
     salva hash, invia email con link `{PublicUrl}{ResetPath}?token=...`, audit.
   - `ResetAsync(token, newPassword, client)` → valida token (non usato/non scaduto),
     `IAdminPasswordHasher.Hash`, marca `UsedAt`, revoca tutte le sessioni admin, audit.
   - Vincolo password: min 8 char (validazione leggera coerente con la public app).
10. Registrare il service in `AddAccantoAdminApplication`.
11. Aggiungere gli eventi audit `Admin.PasswordResetRequested` /
    `Admin.PasswordResetCompleted` (stringhe, coerenti con lo stile esistente).

### API

12. `AdminAuthController`:
    - `POST /api/admin/auth/forgot-password` `[AllowAnonymous]` `[EnableRateLimiting("admin-auth-login")]`
      → sempre 204.
    - `POST /api/admin/auth/reset-password` `[AllowAnonymous]` `[EnableRateLimiting("admin-auth-login")]`
      → 204 su successo, 403 token invalido/scaduto.
13. `Program.cs`: `Configure<AdminPasswordResetOptions>` (bind `AdminAuth:PasswordReset`,
    fallback PublicUrl al primo `AdminCors:AllowedOrigins`).

### Seed (password-less, prod-enabled)

14. `AdminSeed.EnsureSeedAsync`:
    - garantisce sempre i ruoli canonici;
    - legge `AdminSeed:Admins` (JSON array); per compat retro, se assente ma
      `AdminSeed:Email` presente, tratta come lista di uno;
    - per ogni entry crea l'admin **solo se l'email non esiste**, con
      `PasswordHash = ""`, `IsActive = true`, ruolo risolto (default Owner);
    - NON logga password (non ce ne sono);
    - log riepilogo: quali admin creati e istruzione "usare forgot-password".
15. `Program.cs`: rimuovere il gate `IsDevelopment()` per il seed (resta comunque
    idempotente e no-op se gli admin esistono).

### Login guard

16. `AdminAuthService.LoginAsync`: se `PasswordHash` è vuoto/non impostato →
    fallire come credenziali non valide (un admin senza password non può loggare
    finché non completa il reset). `IAdminPasswordHasher.Verify` deve gestire hash
    vuoto ritornando false.

### Frontend admin-web

17. `ForgotPasswordPage` (email → POST forgot-password → messaggio neutro).
18. `ResetPasswordPage` (token da query string + nuova password + conferma →
    POST reset-password → redirect a /login).
19. Rotte in `App.tsx` (`/forgot-password`, `/reset-password`, pubbliche) e link
    "Password dimenticata?" nella `LoginPage`. Endpoint nel client
    (`api/endpoints.ts` o `AuthContext`).

### Config + docs

20. `.env.example`: `AdminEmail__*`, `AdminAuth__PasswordReset__*`,
    `AdminSeed__Admins`.
21. `docker-compose.yml` (servizio `accanto-admin-api`): env `AdminEmail__*`,
    `AdminAuth__PasswordReset__PublicUrl`, `AdminSeed__Admins`.
22. `docs/admin-system.md`: aggiornare "Creazione del primo admin" (seed senza
    password + reset), nuova sezione "Reset password admin", troubleshooting email.

## Tests

```text
[ ] dotnet build
[ ] dotnet test (nessuna regressione, 254 preesistenti verdi)
[ ] AdminPasswordResetService: request anti-enumerazione (email sconosciuta -> no throw, no token)
[ ] request crea token con hash (mai raw), scadenza, invia email se configurata
[ ] reset con token valido imposta hash + UsedAt + revoca sessioni + audit
[ ] reset con token usato/scaduto/garbage -> ForbiddenException
[ ] reset password troppo corta -> validation
[ ] seed: crea ruoli + admin da JSON array, password vuota, IsActive=true
[ ] seed: idempotente (email esistente non duplicata)
[ ] seed: default role Owner quando role assente/invalid
[ ] login con admin senza password -> Unauthorized
[ ] npm run build admin-web
```

## Acceptance criteria

```text
[x] Seed abilitato in prod (no gate IsDevelopment), password-less, idempotente
[x] Seed supporta piu' admin via AdminSeed__Admins (JSON)
[x] Flusso reset password admin funziona end-to-end (forgot -> email -> reset)
[x] Token reset salvato solo come hash; reset revoca tutte le sessioni
[x] Admin senza password non puo' loggare finche' non completa il reset
[x] Email admin self-contained (nessun ref ad Accanto.Infrastructure pubblica)
[x] Nessuna registrazione pubblica admin introdotta
[x] Pagine admin-web forgot/reset presenti
[x] Docs aggiornate
```

> Verificato 2026-07-31: build backend 0/0, 263/263 test verdi (217 pubblici +
> 46 admin), npm run build admin-web OK, docker compose config EXIT=0.
> Runtime end-to-end (invio email reale) da coprire in Manual QA con SMTP.

## Privacy checks

```text
[ ] Nessun contenuto utente coinvolto (solo email/displayName admin)
[ ] Token reset in AccantoAdminDb, mai nel DB pubblico
[ ] Nessun timeline/document/care circle/question/shared update esposto
[ ] Password mai loggata; token raw mai persistito
[ ] Nessuna impersonation introdotta
```

## Security notes

- Rate limit su forgot/reset (bucket `admin-auth-login`, 5/min/IP).
- Anti-enumerazione su forgot-password (sempre 204).
- Reset revoca sessioni: chi avesse la vecchia password/token e' fuori.
- Il seed prod NON introduce credenziali long-lived in env (solo email/displayName).
- `AdminEmail__*` opzionale: senza SMTP il sender e' no-op; in tal caso il primo
  reset admin puo' essere completato via injection del token (documentare), come
  gia' avviene per la app pubblica.

## Rollback notes

Revert dei file creati/modificati da questo task. La migration
`AddAdminPasswordResetTokens` va rimossa con `dotnet ef migrations remove`
(o applicata la down) se gia' generata.
