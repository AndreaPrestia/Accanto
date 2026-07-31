# 09 — Implementation plan

## Strategy

Implementare il sistema Admin in modo incrementale.

Non implementare tutto in un unico passaggio.

Fasi:

1. Repository survey.
2. Admin domain.
3. Admin DB.
4. Admin auth.
5. Internal user metadata.
6. Admin user operations.
7. Audit log.
8. Admin frontend.
9. Docker Compose.
10. Tests.
11. Docs.
12. Final verification.

## Implementation order

### Step 1 — Repository survey

Output:

```text
docs/sdd/admin-control-plane/00-context.md
tasks/TASK-001-repository-survey.md
```

No production code changes.

### Step 2 — Admin domain

Create:

```text
Accanto.Admin.Domain
```

Entities:

- AdminUser;
- AdminRole;
- AdminUserRole;
- AdminSession;
- AdminAuditLog;
- AdminOperation.

### Step 3 — Admin DB

Create:

```text
Accanto.Admin.Infrastructure
AccantoAdminDbContext
EF configurations
Initial migration
```

### Step 4 — Admin API + Auth

Create:

```text
Accanto.Admin.Api
Accanto.Admin.Application
```

Implement:

- login;
- refresh;
- logout;
- me;
- seed admin development.

### Step 5 — Internal metadata endpoints

Add minimal isolated internal endpoints to public app:

```http
GET /internal/admin/users
GET /internal/admin/users/{userId}
```

These endpoints return metadata only.

### Step 6 — User operations

Add:

```http
POST /internal/admin/users/{userId}/disable
POST /internal/admin/users/{userId}/enable
POST /internal/admin/users/{userId}/revoke-sessions
POST /internal/admin/users/{userId}/deletion-requests
```

Admin API exposes matching public admin endpoints.

### Step 7 — Audit log and operations

Implement audit logging for every mutating action.

### Step 8 — Admin frontend

Create:

```text
admin/accanto-admin-web
```

Implement pages and modals.

### Step 9 — Docker Compose

Add:

- postgres-admin;
- accanto-admin-api;
- accanto-admin-web;
- env variables.

### Step 10 — Tests

Add privacy/security tests.

### Step 11 — Docs

Create:

```text
docs/admin-system.md
```

### Step 12 — Final verification

Create:

```text
docs/sdd/admin-control-plane/final-verification-report.md
```

## Modification policy

Allowed public app modifications:

- isolated internal admin endpoints;
- minimal technical fields if required, e.g. `IsDisabled`;
- session revocation hook if already supported;
- health endpoint if absent.

Forbidden public app modifications:

- `User.IsAdmin`;
- admin auth inside public auth;
- admin route inside public PWA;
- DTOs exposing content;
- document download from admin;
- impersonation;
- broad refactoring unrelated to admin.

## Build/test after each task

After each task:

```bash
dotnet build
dotnet test
npm run build
```

Adapt commands to repository.

## Stop conditions

Stop implementation if:

- the agent proposes `User.IsAdmin`;
- admin frontend is added into public PWA;
- admin API starts returning content;
- admin DB stores user content;
- impersonation is introduced;
- hard delete is implemented directly in admin API;
- existing app is rewritten unnecessarily.
