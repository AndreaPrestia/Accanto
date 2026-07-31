# TASK-006 — Admin User Operations

## Goal

Implement disable, enable, revoke sessions and start deletion request through Admin API and internal commands.

## Files to create

```text
No specific new project required; add to existing admin/internal projects.
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Api/**
backend/src/Accanto.Admin.Application/**
backend/src/Accanto.Admin.Infrastructure/**
backend/src/Accanto.Api/** internal endpoints only
backend/src/Accanto.Application/** internal services only
```

## Files forbidden to modify

```text
frontend/**
admin/**
backend/src/Accanto.Domain/** except minimal account status if required
```

## Implementation steps

1. Add Admin API endpoints for user operations.
2. Add reason validation.
3. Add internal app endpoints/commands.
4. Implement disable user.
5. Implement enable user.
6. Implement revoke sessions.
7. Implement start deletion request.
8. Ensure no hard delete directly in admin.
9. Create operation records.

## Tests

```text
[ ] dotnet build
[ ] authorization tests
[ ] reason validation tests
[ ] operation tests
```

## Acceptance criteria

```text
[ ] Disable works
[ ] Enable works
[ ] Revoke sessions works
[ ] Start deletion request works
[ ] Reason required
[ ] SecurityAuditor cannot mutate
[ ] No hard delete from admin
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
