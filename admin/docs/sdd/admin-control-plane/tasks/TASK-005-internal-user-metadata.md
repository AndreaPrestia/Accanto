# TASK-005 — Internal User Metadata

## Goal

Add minimal internal endpoints in the public app to expose allowed user metadata only.

## Files to create

```text
No new project required unless repository structure suggests an Internal API project.
```

## Files allowed to modify

```text
backend/src/Accanto.Api/** internal endpoint files only
backend/src/Accanto.Application/** internal metadata DTO/service only
backend/src/Accanto.Infrastructure/** queries only if needed
```

## Files forbidden to modify

```text
frontend/**
admin/**
backend/src/Accanto.Domain/** except minimal IsDisabled fields if explicitly required
```

## Implementation steps

1. Add service-to-service auth for internal admin endpoints.
2. Add `GET /internal/admin/users`.
3. Add `GET /internal/admin/users/{userId}`.
4. Return metadata only.
5. Ensure DTOs exclude forbidden fields.
6. Add tests for forbidden fields.

## Tests

```text
[ ] dotnet build
[ ] metadata endpoint tests
[ ] privacy boundary tests
```

## Acceptance criteria

```text
[ ] Internal endpoints protected
[ ] Public JWT rejected
[ ] Admin frontend JWT rejected
[ ] Service token accepted
[ ] Metadata returned
[ ] No forbidden content returned
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
