# TASK-009 — Docker Compose

## Goal

Add admin services and configuration without breaking existing services.

## Files to create

```text
Dockerfiles if needed for admin API/frontend.
```

## Files allowed to modify

```text
docker-compose.yml
.env.example
backend/src/Accanto.Admin.Api/Dockerfile
admin/accanto-admin-web/Dockerfile
```

## Files forbidden to modify

```text
Existing service definitions except additive environment/configuration changes
```

## Implementation steps

1. Add `postgres-admin`.
2. Add `accanto-admin-api`.
3. Add `accanto-admin-web`.
4. Add admin DB env vars.
5. Add Admin JWT env vars.
6. Add internal admin service-to-service env vars.
7. Add Admin CORS env vars.
8. Preserve existing services.
9. Validate compose config.

## Tests

```text
[ ] docker compose config
[ ] docker compose up where feasible
```

## Acceptance criteria

```text
[ ] postgres-admin added
[ ] admin API service added
[ ] admin frontend service added
[ ] env example updated
[ ] existing services preserved
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
