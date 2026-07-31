# ADR-0001 — Admin Control Plane

## Status

Accepted

## Context

Accanto may contain highly sensitive user-created caregiving content. A traditional admin panel could easily become too powerful and allow operators to read private material.

The project needs administrative capabilities, but only for platform operations.

## Decision

Implement the admin system as a separate **Admin Control Plane**.

This means:

- separate Admin API;
- separate Admin Frontend;
- separate Admin Domain;
- separate Admin Application layer;
- separate Admin Infrastructure;
- separate Admin DB.

Admins are not public users with extra flags.

## Consequences

Positive:

- clearer security boundary;
- easier auditability;
- less risk of accidental content access;
- better future SaaS/hosted posture.

Negative:

- more projects;
- more configuration;
- more deployment complexity;
- service-to-service communication required.

## Alternatives considered

### Add `/admin` routes to public PWA

Rejected. Too easy to mix public and admin concerns.

### Add `User.IsAdmin`

Rejected. Admins are a different identity class.

### Single API with admin controllers

Rejected as default. It may be acceptable only for internal endpoints, but the preferred design is a separate Admin API.

## Privacy impact

Positive. The control plane boundary reinforces the rule that admins manage accounts, not content.

## Security impact

Positive. Separate auth, DB and API reduce blast radius.
