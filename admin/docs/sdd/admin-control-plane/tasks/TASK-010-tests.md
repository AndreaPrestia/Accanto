# TASK-010 — Tests

## Goal

Add backend/frontend tests focused on security and privacy boundary.

## Files to create

```text
Test files under existing or new test projects.
```

## Files allowed to modify

```text
backend/tests/**
backend/src/** test-visible changes only
admin/accanto-admin-web/** tests only
```

## Files forbidden to modify

```text
Production logic except bug fixes required by tests
```

## Implementation steps

1. Add admin auth tests.
2. Add admin authorization tests.
3. Add privacy boundary response tests.
4. Add reason-required tests.
5. Add audit log tests.
6. Add refresh token hash test.
7. Add frontend basic tests if project supports it.
8. Run full test suite.

## Tests

```text
[ ] dotnet test
[ ] npm test if configured
[ ] npm run build
```

## Acceptance criteria

```text
[ ] Admin auth tested
[ ] Admin authorization tested
[ ] Privacy boundary tested
[ ] Audit log tested
[ ] Existing tests still pass
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
