# TASK-004 — Admin Auth

## Goal

Implement Admin API authentication, separate JWT settings, refresh tokens and admin seed.

## Files to create

```text
backend/src/Accanto.Admin.Api/**
backend/src/Accanto.Admin.Application/**
```

## Files allowed to modify

```text
backend/src/Accanto.Admin.Api/**
backend/src/Accanto.Admin.Application/**
backend/src/Accanto.Admin.Infrastructure/**
backend/src/Accanto.Admin.Domain/**
.env.example
```

## Files forbidden to modify

```text
backend/src/Accanto.Api/** except solution/reference configuration if unavoidable
backend/src/Accanto.Domain/**
frontend/**
admin/**
```

## Implementation steps

1. Create `Accanto.Admin.Api`.
2. Create `Accanto.Admin.Application`.
3. Configure Admin JWT auth.
4. Implement login endpoint.
5. Implement refresh endpoint.
6. Implement logout endpoint.
7. Implement me endpoint.
8. Store refresh token hash.
9. Add dev admin seed.
10. Add rate limiting to login.

## Tests

```text
[ ] dotnet build
[ ] auth tests if test project exists
```

## Acceptance criteria

```text
[ ] Admin login works
[ ] Admin refresh works
[ ] Admin logout works
[ ] Admin me works
[ ] Admin JWT settings separate
[ ] Public JWT rejected by admin endpoints
[ ] Refresh token stored hashed
[ ] No public admin registration
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
