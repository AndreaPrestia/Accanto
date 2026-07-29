# ADR-0002 — Admin DB Separation

## Status

Accepted

## Context

Admin identities, admin sessions, audit logs and admin operations have a different lifecycle and risk profile than public user data.

Mixing admin and public data makes it easier to accidentally grant excess privileges or query sensitive content.

## Decision

Create a separate database:

```text
AccantoAdminDb
```

The public app keeps its own database:

```text
AccantoDb
```

`AccantoAdminDb` stores only:

- AdminUsers;
- AdminRoles;
- AdminUserRoles;
- AdminSessions;
- AdminAuditLogs;
- AdminOperations.

## Consequences

Positive:

- clean data boundary;
- easier backups and retention policies;
- easier privilege separation;
- easier security review.

Negative:

- extra database;
- extra migrations;
- docker-compose changes;
- deployment complexity.

## Alternatives considered

### Store admins in public Users table

Rejected.

### Store audit logs in public app DB

Rejected for v0.1. Audit logs are part of the control plane.

### Use one database with two schemas

Possible later, but less strong than a separate DB.

## Privacy impact

Positive. Admin DB cannot accidentally contain caregiving content if designed correctly.

## Security impact

Positive. Admin API can use a DB principal that does not have access to public app content.
