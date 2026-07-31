# Accanto Admin Control Plane

Documentazione per sviluppatori e operatori del sistema **Admin** di Accanto.

> **Principio guida:** gli admin gestiscono la piattaforma, **non** leggono la vita
> privata degli utenti. Il sistema Admin è progettato per non poter accedere ai
> contenuti sensibili (timeline, documenti, domande ai medici, aggiornamenti, nomi
> dei cerchi di cura, nomi originali dei file).

---

## Indice

1. [Cos'è](#cosè)
2. [Architettura](#architettura)
3. [Setup locale](#setup-locale)
4. [Admin DB](#admin-db)
5. [Creazione del primo admin](#creazione-del-primo-admin)
6. [Privacy boundary](#privacy-boundary)
7. [Audit log](#audit-log)
8. [Endpoint interni (service-to-service)](#endpoint-interni-service-to-service)
9. [Cosa l'admin NON può fare](#cosa-ladmin-non-può-fare)
10. [Troubleshooting](#troubleshooting)

---

## Cos'è

Il Control Plane Admin è un sistema **separato** dall'app pubblica, composto da:

| Componente | Progetto / servizio | Scopo |
|---|---|---|
| Admin API | `backend/src/Accanto.Admin.Api` | autenticazione admin, operazioni account, audit, health |
| Admin Application | `backend/src/Accanto.Admin.Application` | use case, DTO, validazione, orchestration |
| Admin Domain | `backend/src/Accanto.Admin.Domain` | entità admin (AdminUser, AdminRole, AdminSession, AdminAuditLog, AdminOperation) |
| Admin Infrastructure | `backend/src/Accanto.Admin.Infrastructure` | `AccantoAdminDbContext`, migration, JWT/password/audit, client service-to-service |
| Admin Frontend | `admin/accanto-admin-web` | SPA React separata (NON la PWA pubblica) |
| Admin DB | servizio `postgres-admin` | database dedicato `accanto_admin` |

Caratteristiche:

- autenticazione admin **separata** (JWT dedicato);
- DB admin **separato**;
- operazioni tecniche minime sugli account (disable/enable/revoke/avvio cancellazione);
- audit log admin append-only;
- **zero accesso** ai contenuti utente.

---

## Architettura

```text
+----------------------+        Admin JWT (AdminJwt__*)         +----------------------+
| accanto-admin-web    | -------------------------------------> | Accanto.Admin.Api    |
| (SPA React separata) |                                        |  (control plane)     |
+----------------------+                                        +----------+-----------+
                                                                           |
                                                                           | EF Core
                                                                           v
                                                                  +------------------+
                                                                  | AccantoAdminDb   |
                                                                  | (postgres-admin) |
                                                                  +------------------+

Per le operazioni sugli utenti pubblici (metadata + comandi), l'Admin API NON
accede mai al DB pubblico: passa da endpoint interni service-to-service.

+----------------------+   service-to-service JWT (InternalAdmin__*)  +----------------------+
| Accanto.Admin.Api    | -------------------------------------------> | Accanto.Api          |
|  InternalAppClient   |    GET /internal/admin/users[/{id}]          |  /internal/admin/*   |
|                      |    POST disable/enable/revoke/deletion       |  (app-owned commands)|
+----------------------+                                              +----------+-----------+
                                                                             | EF Core
                                                                             v
                                                                    +------------------+
                                                                    | AccantoDb        |
                                                                    | (public app DB)  |
                                                                    +------------------+
```

### Tre contesti di autenticazione distinti

| Contesto | Sezione config | Chi usa | Validato da |
|---|---|---|---|
| JWT pubblico | `Jwt__*` | utenti della PWA | `Accanto.Api` |
| JWT admin | `AdminJwt__*` | admin (frontend admin) | `Accanto.Admin.Api` |
| Service-to-service | `InternalAdmin__*` (public) / `InternalApp__*` (admin) | Admin API → public API | `Accanto.Api` `/internal/admin/*` |

Issuer, audience e **signing key sono diversi** in tutti e tre i casi. Conseguenza:

- un JWT pubblico **non** è accettato dagli endpoint admin;
- un JWT admin-frontend **non** è accettato dagli endpoint interni;
- un token interno non è valido né per la PWA né per l'admin-frontend.

---

## Setup locale

### Con Docker (consigliato)

```bash
# Copia e valorizza le variabili admin in .env (vedi .env.example, sezione Admin).
docker compose up -d postgres-admin accanto-admin-api accanto-admin-web
```

Servizi admin aggiunti (in modo additivo, senza toccare quelli pubblici):

| Servizio | Porta host | Note |
|---|---|---|
| `postgres-admin` | 5433 | DB admin dedicato |
| `accanto-admin-api` | 8082 | Admin API |
| `accanto-admin-web` | 5174 | Admin frontend |

L'Admin API esegue `MigrateAsync()` all'avvio e, in **Development**, il seed dei
ruoli + primo admin (vedi sotto).

### Senza Docker

Backend (Admin API):

```bash
cd backend/src/Accanto.Admin.Api
# Imposta ConnectionStrings__AdminDatabase, AdminJwt__Key, InternalApp__*.
dotnet run
```

Frontend (admin-web):

```bash
cd admin/accanto-admin-web
npm install
# VITE_ADMIN_API_BASE_URL deve puntare all'Admin API (default /admin-api in compose).
$env:VITE_ADMIN_API_BASE_URL="http://localhost:8082/admin-api"   # PowerShell
npm run dev
```

### Variabili d'ambiente principali

Tutte documentate in `.env.example` (sezione *Admin Control Plane*):

```text
ConnectionStrings__AdminDatabase=Host=postgres-admin;Port=5432;Database=accanto_admin;Username=accanto_admin;Password=...
AdminJwt__Issuer=accanto-admin
AdminJwt__Audience=accanto-admin
AdminJwt__Key=<almeno 32 char, DISTINTA da Jwt__Key>
AdminCors__AllowedOrigins=http://localhost:5174
InternalApp__BaseUrl=http://backend:8080
InternalApp__SigningKey=<DEVE matchare InternalAdmin__SigningKey del backend pubblico>
InternalAdmin__SigningKey=<lato backend pubblico, stessa chiave di InternalApp__SigningKey>
```

> ⚠️ **Mai** riusare `Jwt__Key` (pubblico) per `AdminJwt__Key` o
> `InternalApp__SigningKey`. Tre chiavi diverse.

---

## Admin DB

`AccantoAdminDb` è un database **fisicamente separato** (servizio `postgres-admin`,
proprio volume, proprie credenziali). Contiene solo tabelle admin:

| Tabella | Contenuto |
|---|---|
| `admin_users` | account admin (email, display name, hash password, flag MFA/attivo) |
| `admin_roles` | ruoli: `Owner`, `Operator`, `SecurityAuditor` |
| `admin_user_roles` | associazione admin ↔ ruoli |
| `admin_sessions` | refresh token **hash** (mai raw), scadenza, revoca |
| `admin_audit_logs` | audit append-only delle azioni admin |
| `admin_operations` | tracking del ciclo di vita delle operazioni richieste |

Migration: `InitialAdminCreate` (in `Accanto.Admin.Infrastructure/Migrations`),
applicata all'avvio. Le migration si generano con:

```bash
dotnet ef migrations add <Nome> \
  --project backend/src/Accanto.Admin.Infrastructure \
  --context AccantoAdminDbContext
```

**Non contiene** e non deve mai contenere copie di contenuti utente (nomi cerchi,
titoli/contenuti timeline, nomi file, path, domande, aggiornamenti, body request).

---

## Creazione del primo admin

Non esiste registrazione pubblica per gli admin. Gli admin vengono **seedati senza
password** (email + display name + ruolo); ciascuno imposta poi la propria password
tramite il flusso di reset. Il seed è **idempotente** e gira in **tutti gli ambienti**
(Development e Production): non introduce credenziali long-lived in configurazione.

### Seed (email + display name + ruolo, senza password)

Config `AdminSeed:Admins` come **JSON array** (env `AdminSeed__Admins`):

```json
"AdminSeed": {
  "Admins": "[{\"email\":\"admin@accanto.care\",\"displayName\":\"Admin\",\"role\":\"Owner\"}]"
}
```

- `role` opzionale (default `Owner`); valori: `Owner`, `Operator`, `SecurityAuditor`.
- `displayName` opzionale (fallback: prefisso dell'email).
- All'avvio l'Admin API garantisce i ruoli canonici e crea gli admin elencati **solo
  se l'email non esiste già**. Gli account nascono con `PasswordHash` vuoto e
  `IsActive = true`: **non possono loggare** finché non impostano la password.

### Impostare la password (primo accesso)

L'admin usa il flusso di reset (vedi sotto): dalla pagina di login dell'admin-web →
"Password dimenticata / primo accesso?" → inserisce l'email → riceve il link → imposta
la password.

### Bootstrap senza email (SMTP non configurato)

Se `AdminEmail__*` non è configurato, il sender è no-op e il link non viene inviato.
In tal caso il primo reset si completa via **injection del token** (stesso pattern
della app pubblica): generare un token URL-safe, calcolarne l'hash SHA-256,
`INSERT` in `admin_password_reset_tokens` per l'admin, poi `POST /api/admin/auth/reset-password`
con `{token, newPassword}`.

---

## Reset password admin

Flusso self-contained nel control plane (nessun riuso del flusso pubblico).

```text
POST /api/admin/auth/forgot-password  { "email": "..." }      → 204 (sempre, anti-enumerazione)
POST /api/admin/auth/reset-password   { "token": "...", "newPassword": "..." }  → 204 / 403
```

Regole:

- **Anti-enumerazione**: `forgot-password` risponde sempre 204, esista o no l'email;
  nessun token emesso per admin inattivi.
- Il token è salvato **solo come hash SHA-256** in `admin_password_reset_tokens`
  (mai il valore raw); il raw esiste solo nel link email. Scadenza default 60 min,
  monouso (`UsedAt`).
- Il reset imposta l'hash PBKDF2 e **revoca tutte le sessioni admin** dell'utente.
- Entrambi gli endpoint sono anonimi e **rate-limited** (bucket `admin-auth-login`).
- Eventi audit: `Admin.PasswordResetRequested`, `Admin.PasswordResetCompleted`.

Config:

```text
AdminAuth__PasswordReset__PublicUrl=https://admin.accanto.care   # base URL admin-web (fallback: primo AdminCors origin)
AdminAuth__PasswordReset__ResetPath=/reset-password
AdminAuth__PasswordReset__TokenLifetimeMinutes=60
AdminEmail__SmtpHost=...   AdminEmail__FromAddress=...           # vuoto = sender no-op
```

---

## Privacy boundary

L'admin accede **solo a metadata e aggregati**, mai a contenuti.

| Dato | Admin può vedere? |
|---|---|
| UserId, email, display name, CreatedAt, LastLoginAt | ✅ |
| IsDisabled, AccountStatus, DisabledAt/Reason | ✅ |
| Conteggi: care circle, documenti, timeline entries | ✅ |
| Storage usato (somma byte) | ✅ |
| `CareCircle.Name` / `Description` | ❌ |
| `TimelineEntry.Title` / `Content` / `Tags` | ❌ |
| `MedicalDocument.OriginalFileName` / `StoragePath` / `Notes` | ❌ |
| Contenuto dei file | ❌ |
| `DoctorQuestion.Question` / `AnswerNotes` | ❌ |
| `SharedUpdate.Content` | ❌ |

Regole implementate a più livelli:

- **DTO rule** — i DTO interni/admin non hanno proprietà vietate (verificato da
  test di reflection + serializzazione).
- **API rule** — non esistono endpoint admin per leggere contenuti.
- **UI rule** — il frontend admin non richiede né renderizza campi vietati.
- **DB rule** — il DB admin non copia contenuti sensibili.
- **Encryption** — l'Admin API non possiede la `Encryption__MasterKey` pubblica né
  `IFieldProtector`: non può decifrare i campi cifrati nemmeno per errore.

---

## Audit log

Ogni **azione mutativa** admin richiede una `reason` obbligatoria e scrive una
entry in `admin_audit_logs`:

| Azione | `action` |
|---|---|
| Login / Logout | `Admin.Login` / `Admin.Logout` |
| Disable / Enable account | `User.Disable` / `User.Enable` |
| Revoca sessioni | `User.RevokeSessions` |
| Avvio cancellazione | `User.StartDeletion` |

Ogni entry contiene: `AdminUserId`, `Action`, `TargetType`, `TargetId` (opaco),
`Reason`, `IpAddress`, `UserAgent`, `CreatedAt`.

L'audit è **append-only a livello DB**: all'avvio l'Admin API revoca `UPDATE` e
`DELETE` su `admin_audit_logs` per il ruolo runtime. Non contiene mai body
request/response né contenuti utente.

Lettura: `GET /api/admin/audit-logs` (filtri per admin/action/targetType/targetId/
intervallo date, paginazione). Le operazioni sono tracciate anche in
`admin_operations` (`GET /api/admin/operations[/{id}]`).

---

## Endpoint interni (service-to-service)

L'app pubblica espone endpoint interni **non destinati ai browser**, protetti dallo
scheme `InternalAdminScheme`:

```text
GET  /internal/admin/users              (lista metadata, filtri q/disabled, paginata)
GET  /internal/admin/users/{userId}     (dettaglio metadata)
POST /internal/admin/users/{userId}/disable
POST /internal/admin/users/{userId}/enable
POST /internal/admin/users/{userId}/revoke-sessions
POST /internal/admin/users/{userId}/deletion-requests
```

Regole:

- richiedono il token service-to-service (`InternalAdmin__*`);
- rifiutano JWT pubblici e JWT admin-frontend;
- ritornano **solo metadata** o eseguono **comandi account** app-owned;
- non raggiungibili dai client browser (scheme dedicato, non registrato nella
  pipeline di default).

L'Admin API li invoca tramite `InternalAppClient` con token mintato per chiamata.

---

## Cosa l'admin NON può fare

Vincoli non negoziabili, enforced nel codice e nei test:

- ❌ **Leggere contenuti utente** — timeline, documenti, domande ai medici,
  aggiornamenti, note, nomi dei cerchi, nomi originali dei file, path di storage.
- ❌ **Impersonation** — nessun "login as user", "view as user", token minting per
  utenti, bypass di membership.
- ❌ **Hard delete diretto** — l'admin *avvia* la cancellazione; il dominio pubblico
  esegue l'erasure GDPR (tombstone: PII azzerati, documenti rimossi anche da S3,
  audit preservato). L'admin non cancella direttamente dati.
- ❌ **Scaricare documenti** dall'admin.
- ❌ **Registrarsi da solo** — nessuna registrazione pubblica admin.
- ❌ **Usare il JWT degli utenti** — né viceversa.
- ❌ **`User.IsAdmin`** — non esiste alcun flag admin sulla tabella utenti pubblica;
  gli admin non vivono nella tabella `users`.

Ruoli:

| Ruolo | Può |
|---|---|
| `Owner` | tutto |
| `Operator` | operazioni account (disable/enable/revoke/deletion) |
| `SecurityAuditor` | **sola lettura** di metadata/audit/operations; nessuna mutazione |

---

## Troubleshooting

**Login admin fallisce con 401 "Credenziali non valide".**
Verifica email/password. Controlla che l'admin esista e `IsActive=true`. Se l'admin
è stato appena seedato, ha `PasswordHash` vuoto e **non può loggare**: deve prima
impostare la password via `forgot-password`. Nei log del seed cerca
`AdminSeed: creato admin ...`.

**L'email di reset admin non arriva.**
`AdminEmail__*` è opzionale: se `SmtpHost`/`FromAddress` sono vuoti il sender è
no-op (nessuna email). Configura SMTP oppure completa il primo reset via injection
del token (vedi "Bootstrap senza email"). `forgot-password` risponde comunque 204
per anti-enumerazione, anche quando l'email non viene inviata.

**Gli endpoint `/internal/admin/*` rispondono 401 anche con un token.**
La chiave `InternalApp__SigningKey` (Admin API) deve **matchare**
`InternalAdmin__SigningKey` (backend pubblico). Devono coincidere anche issuer e
audience (`accanto-internal-admin`). Se `InternalAdmin__SigningKey` è vuota nel
backend pubblico, lo scheme non accetta alcun token (opt-in).

**L'admin-web non raggiunge l'Admin API.**
Controlla `VITE_ADMIN_API_BASE_URL` (build-time). In compose è `/admin-api` (proxy
nginx interno verso `accanto-admin-api`). Fuori dal compose punta all'URL completo
(es. `http://localhost:8082/admin-api`). Verifica anche `AdminCors__AllowedOrigins`.

**L'Admin API non si avvia / migrazioni falliscono.**
Verifica `ConnectionStrings__AdminDatabase` e che `postgres-admin` sia healthy
(`docker compose ps`). Le migration girano all'avvio; guarda i log
(`docker compose logs accanto-admin-api`).

**Un utente disabilitato riesce ancora ad accedere.**
Il disable revoca le sessioni attive e blocca nuovi login (guard in `AuthService`).
Se un access token è ancora valido per pochi minuti, scadrà da solo (il refresh è
revocato). Per blocco immediato usa anche *Revoke sessions*.

**Token admin scadono troppo presto / troppo tardi.**
`AdminJwt__ExpiryMinutes` (default 60) e `AdminJwt__RefreshTokenExpiryDays`
(default 7). Il refresh ruota a ogni uso.

**Swagger in produzione.**
Non esporre Swagger in produzione. La UI è pensata per sviluppo; in prod va
limitata/disabilitata (vedi security QA del test plan).

---

## Riferimenti

- Specifiche SDD complete: `admin/docs/sdd/admin-control-plane/`
- ADR: `admin/docs/sdd/admin-control-plane/adr/` (separazione DB, no-content-access,
  service-to-service boundary)
- Variabili d'ambiente: `.env.example`
