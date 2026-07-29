# TASK-007 — Admin Audit Log

## Goal

Implement audit logs and operation read endpoints.

## Files to create

```text
No specific new project required; add to admin projects.
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Api/**
backend/src/Accanto.Admin.Application/**
backend/src/Accanto.Admin.Infrastructure/**
backend/src/Accanto.Admin.Domain/** if audit constants needed
```

## Files forbidden to modify

```text
backend/src/Accanto.Domain/**
frontend/**
admin/**
```

## Implementation steps

1. Implement audit write service.
2. Write audit logs for login/logout.
3. Write audit logs for user operations.
4. Add `GET /api/admin/audit-logs`.
5. Add `GET /api/admin/operations`.
6. Add `GET /api/admin/operations/{operationId}`.
7. Add access policy for audit/log endpoints.
8. Ensure no sensitive payloads.

## Tests

```text
[ ] dotnet build
[ ] audit tests
[ ] no sensitive payload tests
```

## Acceptance criteria

```text
[ ] Mutating actions write audit log
[ ] Audit list works
[ ] Operations list works
[ ] Operation detail works
[ ] Audit log excludes sensitive payloads
```

## Privacy checks

```text
[ ] No timeline title/content exposed
[ ] No document original filename exposed
[ ] No document storage path exposed
[ ] No care circle name exposed
[ ] No doctor question exposed
[ ] No shared update exposed
[ ] No impersonation introduced
```

## Rollback notes

Revert files created or modified by this task.
