# TASK-002 — Admin Domain

## Goal

Create the admin domain model without touching the public app domain.

## Files to create

```text
backend/src/Accanto.Admin.Domain/**
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Domain/**
backend/Accanto.sln or equivalent solution file
```

## Files forbidden to modify

```text
backend/src/Accanto.Domain/**
frontend/**
admin/**
docker-compose.yml
```

## Implementation steps

1. Create `Accanto.Admin.Domain`.
2. Add AdminUser.
3. Add AdminRole.
4. Add AdminUserRole.
5. Add AdminSession.
6. Add AdminAuditLog.
7. Add AdminOperation.
8. Add AdminOperationType enum.
9. Add AdminOperationStatus enum.
10. Add role constants if useful.

## Tests

```text
[ ] dotnet build
```

## Acceptance criteria

```text
[ ] Admin domain project exists
[ ] Admin entities exist
[ ] Admin enums exist
[ ] Public domain untouched
[ ] No User.IsAdmin added
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
