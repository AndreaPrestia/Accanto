# 04 — Architecture

## Overview

Il sistema Admin viene implementato come control plane separato.

```text
+----------------------+
| accanto-admin-web    |
| Admin Frontend       |
+----------+-----------+
           |
           | Admin JWT
           v
+----------------------+
| Accanto.Admin.Api    |
| Admin API            |
+----------+-----------+
           |
           | EF Core
           v
+----------------------+
| AccantoAdminDb       |
| Admin DB             |
+----------------------+
```

Per operazioni su utenti pubblici:

```text
+----------------------+
| Accanto.Admin.Api    |
+----------+-----------+
           |
           | service-to-service auth
           | metadata + commands only
           v
+----------------------+
| Accanto.Api          |
| Internal endpoints   |
+----------+-----------+
           |
           | EF Core
           v
+----------------------+
| AccantoDb            |
| Public App DB        |
+----------------------+
```

## Components

### Accanto.Admin.Api

Responsabilità:

- autenticazione admin;
- autorizzazione admin;
- gestione sessioni admin;
- lettura metadata utenti tramite internal app API;
- invio comandi tecnici alla internal app API;
- audit log;
- operation tracking;
- technical health;
- CORS admin.

Non responsabilità:

- leggere dati utente sensibili;
- leggere documenti;
- accedere a timeline;
- impersonare utenti.

### Accanto.Admin.Application

Responsabilità:

- use case admin;
- DTO admin;
- request validation;
- permission checks;
- orchestration delle chiamate interne;
- audit service contracts.

### Accanto.Admin.Domain

Responsabilità:

- AdminUser;
- AdminRole;
- AdminUserRole;
- AdminSession;
- AdminAuditLog;
- AdminOperation;
- admin enums.

### Accanto.Admin.Infrastructure

Responsabilità:

- AccantoAdminDbContext;
- EF configurations;
- migrations;
- admin password hashing;
- admin JWT service;
- refresh token hashing;
- internal app API client;
- technical log provider.

### accanto-admin-web

Responsabilità:

- login admin;
- dashboard;
- users metadata;
- user operations;
- audit logs;
- operations;
- system page.

Non responsabilità:

- route pubbliche;
- PWA caregiver;
- contenuti utente.

## Deployment separation

Produzione suggerita:

```text
accanto.care           -> PWA pubblica
api.accanto.care       -> API pubblica
admin.accanto.care     -> frontend admin
admin-api.accanto.care -> Admin API
```

## Database separation

```text
AccantoDb
- users
- care circles
- timeline
- documents
- questions
- shared updates

AccantoAdminDb
- admin users
- admin roles
- admin sessions
- admin audit logs
- admin operations
```

## Auth separation

```text
Public JWT:
- issuer: Accanto
- audience: AccantoApp

Admin JWT:
- issuer: Accanto.Admin
- audience: Accanto.Admin.Web

Internal admin JWT:
- issuer: Accanto.Admin.Api
- audience: Accanto.Internal
```

I JWT pubblici non devono funzionare sugli endpoint admin.

## Internal app API

Gli endpoint interni devono essere minimizzati:

```http
GET  /internal/admin/users
GET  /internal/admin/users/{userId}
POST /internal/admin/users/{userId}/disable
POST /internal/admin/users/{userId}/enable
POST /internal/admin/users/{userId}/revoke-sessions
POST /internal/admin/users/{userId}/deletion-requests
GET  /internal/admin/system/health
```

## Development mode

In development è accettabile:

- creare seed admin da env;
- esporre Swagger admin;
- usare localhost CORS;
- usare docker-compose con due Postgres.

## Production mode

In production:

- niente seed admin automatico non protetto;
- niente Swagger pubblico;
- CORS solo admin domain;
- rate limiting login admin;
- secret forti;
- audit log abilitato;
- no stacktrace in response;
- no body logging.

## Failure handling

Se l'Admin API non riesce a chiamare la Internal App API:

- registrare AdminOperation failed;
- scrivere audit log tecnico senza payload sensibili;
- restituire errore chiaro ma non dettagli interni;
- non ritentare operazioni mutative all'infinito senza idempotency key.

## Idempotency

Per operazioni mutative future è consigliabile:

- operation id;
- idempotency key;
- stato Pending/Completed/Failed.

Per v0.1 può bastare `AdminOperation`.
