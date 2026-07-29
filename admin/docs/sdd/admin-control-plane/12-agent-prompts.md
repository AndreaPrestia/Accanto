# 12 — Agent prompts for Claude/Kimi

Usare questi prompt in sequenza.

Non saltare direttamente all'implementazione completa.

---

## Prompt 0 — Repository survey

```text
You are working on the Accanto repository.

Do not implement code yet.

First, inspect the repository and complete the repository survey for the Admin Control Plane.

Read:

docs/sdd/admin-control-plane/00-context.md
docs/sdd/admin-control-plane/tasks/TASK-001-repository-survey.md

Update both files with actual repository details:
- backend structure
- frontend structure
- auth model
- user model
- DbContext
- docker-compose
- env files
- tests
- logging
- health checks
- integration risks

Do not change production code in this step.
```

---

## Prompt 1 — Validate SDD docs

```text
Read all SDD documentation under:

docs/sdd/admin-control-plane/

Check for inconsistencies, missing requirements or ambiguous instructions.

Do not implement production code.

Produce a short review section inside:

docs/sdd/admin-control-plane/00-context.md

Do not weaken these constraints:
- Admin DB separate
- Admin API separate
- Admin frontend separate
- no content access
- no User.IsAdmin
- no impersonation
```

---

## Prompt 2 — Implement TASK-002 Admin Domain

```text
Implement only TASK-002-admin-domain.

Read:

docs/sdd/admin-control-plane/tasks/TASK-002-admin-domain.md

Create the Accanto.Admin.Domain project and implement the admin domain entities and enums.

Do not modify the public app domain.

Do not implement database, API, frontend or auth yet.

After implementation, run build/tests if available and update the task file with completion notes.
```

---

## Prompt 3 — Implement TASK-003 Admin DB

```text
Implement only TASK-003-admin-db.

Read:

docs/sdd/admin-control-plane/tasks/TASK-003-admin-db.md

Create Accanto.Admin.Infrastructure, AccantoAdminDbContext, EF configurations and initial migration for the admin database.

Use a separate connection string:
ConnectionStrings__AdminDatabase

Do not reuse the public app DbContext.

Do not modify the public app database schema.

Run build/tests and update the task file with completion notes.
```

---

## Prompt 4 — Implement TASK-004 Admin Auth

```text
Implement only TASK-004-admin-auth.

Read:

docs/sdd/admin-control-plane/tasks/TASK-004-admin-auth.md

Create the Accanto.Admin.Api and Accanto.Admin.Application projects if not already created.

Implement:
- POST /api/admin/auth/login
- POST /api/admin/auth/refresh
- POST /api/admin/auth/logout
- GET /api/admin/auth/me

Use admin-specific JWT settings:
- AdminJwt__Issuer
- AdminJwt__Audience
- AdminJwt__SigningKey

Do not accept public app JWTs for admin endpoints.

Do not add User.IsAdmin.

Do not add admin routes to the public PWA.

Run build/tests and update the task file with completion notes.
```

---

## Prompt 5 — Implement TASK-005 Internal User Metadata

```text
Implement only TASK-005-internal-user-metadata.

Read:

docs/sdd/admin-control-plane/tasks/TASK-005-internal-user-metadata.md

Add internal admin metadata endpoints to the public app or internal API layer.

These endpoints must be protected by service-to-service authentication.

They must return only minimized metadata:
- UserId
- Email
- DisplayName
- CreatedAt
- LastLoginAt
- IsDisabled
- AccountStatus
- CareCircleCount
- DocumentsCount
- StorageUsedBytes
- TimelineEntryCount

They must not return:
- CareCircle.Name
- Timeline title/content
- Document original filename
- Document storage path
- Doctor questions
- Shared updates

Keep changes to the public app minimal and isolated.

Run tests and update the task file with completion notes.
```

---

## Prompt 6 — Implement TASK-006 Admin User Operations

```text
Implement only TASK-006-admin-user-operations.

Read:

docs/sdd/admin-control-plane/tasks/TASK-006-admin-user-operations.md

Implement admin user operations:
- disable user
- enable user
- revoke user sessions
- start user deletion request

Every mutating action must require a non-empty reason.

The Admin API must call internal app endpoints using service-to-service auth.

Do not implement hard delete directly from the admin panel.
Do not expose user content.
Do not implement impersonation.

Run tests and update the task file with completion notes.
```

---

## Prompt 7 — Implement TASK-007 Admin Audit Log

```text
Implement only TASK-007-admin-audit-log.

Read:

docs/sdd/admin-control-plane/tasks/TASK-007-admin-audit-log.md

Implement admin audit logging and operation tracking.

Every mutating admin action must write an audit log entry.

Audit logs must not include sensitive user content or request/response payloads.

Implement endpoints:
- GET /api/admin/audit-logs
- GET /api/admin/operations
- GET /api/admin/operations/{operationId}

Run tests and update the task file with completion notes.
```

---

## Prompt 8 — Implement TASK-008 Admin Frontend

```text
Implement only TASK-008-admin-frontend.

Read:

docs/sdd/admin-control-plane/tasks/TASK-008-admin-frontend.md

Create a separate React + TypeScript + Vite admin frontend under:

admin/accanto-admin-web

Do not add admin routes to the public PWA.

Implement:
- login
- dashboard
- users list
- user detail
- audit logs
- operations
- system page

Do not display user content.
Do not display care circle names.
Do not display original filenames.
Do not implement impersonation.

Every mutating user action must open a confirmation modal requiring a reason.

Run frontend build and update the task file with completion notes.
```

---

## Prompt 9 — Implement TASK-009 Docker Compose

```text
Implement only TASK-009-docker-compose.

Read:

docs/sdd/admin-control-plane/tasks/TASK-009-docker-compose.md

Update docker-compose and .env.example to include:
- postgres-admin
- accanto-admin-api
- accanto-admin-web
- admin JWT settings
- admin DB connection string
- internal admin service-to-service settings

Do not remove or break existing services.

Run docker compose config if available and update the task file with completion notes.
```

---

## Prompt 10 — Implement TASK-010 Tests

```text
Implement only TASK-010-tests.

Read:

docs/sdd/admin-control-plane/tasks/TASK-010-tests.md

Add backend tests for the Admin Control Plane.

Focus especially on security and privacy boundaries.

Tests must verify that admin responses do not expose:
- care circle names
- timeline title/content
- document original filenames
- document storage paths
- doctor questions
- shared updates

Run tests and update the task file with completion notes.
```

---

## Prompt 11 — Implement TASK-011 Docs

```text
Implement only TASK-011-docs.

Read:

docs/sdd/admin-control-plane/tasks/TASK-011-docs.md

Create or update documentation for the Admin Control Plane.

Create:

docs/admin-system.md

Explain:
- why the admin system is separate
- how to run it locally
- how to configure Admin DB
- how to create the first admin
- what the admin can do
- what the admin cannot do
- privacy boundary
- audit logs
- internal endpoints
- troubleshooting

Run final build/tests if possible and update the task file with completion notes.
```

---

## Prompt 12 — Final verification

```text
Perform a final SDD verification for the Accanto Admin Control Plane.

Do not add new features.

Check:

1. The existing public app still builds.
2. The existing frontend still builds.
3. The Admin API builds.
4. The Admin frontend builds.
5. Tests pass.
6. Admin DB is separate.
7. Admin users are not stored in the public Users table.
8. No User.IsAdmin or equivalent public-user admin flag exists.
9. Admin endpoints use admin-specific auth.
10. Admin endpoints reject public app JWTs.
11. Admin responses do not expose user content.
12. Admin cannot read timeline entries.
13. Admin cannot read documents.
14. Admin cannot download files.
15. Admin cannot see original filenames.
16. Admin cannot see care circle names.
17. Admin cannot impersonate users.
18. Mutating actions require reason.
19. Mutating actions write audit logs.
20. Documentation is complete.

Produce a final report at:

docs/sdd/admin-control-plane/final-verification-report.md

Include:
- passed checks
- failed checks
- files changed
- risks remaining
- follow-up recommendations

Do not hide failures.
```
