# TASK-003 — Admin DB

## Goal

Create Admin Infrastructure, Admin DbContext, EF configurations and initial migration using a separate database.

## Files to create

```text
backend/src/Accanto.Admin.Infrastructure/**
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Infrastructure/**
backend/src/Accanto.Admin.Api/** only if needed for design-time DbContext
backend/Accanto.sln or equivalent solution file
```

## Files forbidden to modify

```text
backend/src/Accanto.Infrastructure/** except shared abstractions if absolutely necessary
backend/src/Accanto.Domain/**
frontend/**
admin/**
```

## Implementation steps

1. Create `Accanto.Admin.Infrastructure`.
2. Add `AccantoAdminDbContext`.
3. Add EF configurations.
4. Configure table names.
5. Configure indexes.
6. Configure constraints.
7. Add design-time factory if needed.
8. Add initial migration.
9. Use `ConnectionStrings__AdminDatabase`.

## Tests

```text
[ ] dotnet build
[ ] dotnet ef migrations list --context AccantoAdminDbContext
```

## Acceptance criteria

```text
[ ] Admin DbContext exists
[ ] Admin migration exists
[ ] Admin connection string is separate
[ ] Public DbContext not reused
[ ] Public DB schema not modified
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
