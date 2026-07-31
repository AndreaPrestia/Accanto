# TASK-008 — Admin Frontend

## Goal

Create a separate admin frontend for the Admin Control Plane.

## Files to create

```text
admin/accanto-admin-web/**
```

## Files allowed to modify

```text
admin/accanto-admin-web/**
```

## Files forbidden to modify

```text
frontend/**
backend/src/Accanto.Domain/**
backend/src/Accanto.Api/**
```

## Implementation steps

1. Create Vite React TypeScript app under `admin/accanto-admin-web`.
2. Add Tailwind CSS.
3. Add routing.
4. Add API client.
5. Add auth state.
6. Add login page.
7. Add dashboard page.
8. Add users list.
9. Add user detail.
10. Add audit logs page.
11. Add operations page.
12. Add system page.
13. Add reason modals for mutating actions.

## Tests

```text
[ ] npm install
[ ] npm run build
[ ] frontend tests if configured
```

## Acceptance criteria

```text
[ ] Admin frontend separate
[ ] Login page works
[ ] Dashboard exists
[ ] Users list metadata only
[ ] User detail metadata only
[ ] Mutating actions require reason modal
[ ] No forbidden fields displayed
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
