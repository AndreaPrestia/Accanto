# TASK-011 — Docs

## Goal

Create final user/developer documentation for the Admin Control Plane.

## Files to create

```text
docs/admin-system.md
```

## Files allowed to modify

```text
docs/admin-system.md
README.md if appropriate
.env.example if documentation comments are needed
```

## Files forbidden to modify

```text
Production code
```

## Implementation steps

1. Create `docs/admin-system.md`.
2. Explain architecture.
3. Explain local setup.
4. Explain Admin DB.
5. Explain first admin creation.
6. Explain privacy boundary.
7. Explain audit log.
8. Explain internal endpoints.
9. Explain what admin cannot do.
10. Add troubleshooting.

## Tests

```text
[ ] Documentation review
[ ] Links valid
```

## Acceptance criteria

```text
[ ] Admin docs exist
[ ] Setup documented
[ ] Privacy boundary documented
[ ] Forbidden capabilities documented
[ ] Troubleshooting documented
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
