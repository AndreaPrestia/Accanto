# ADR-0004 — Service-to-Service Boundary

## Status

Accepted

## Context

The Admin API must perform account operations on users in the public app. Direct database access to the public app could accidentally expose content or bypass business rules.

## Decision

The Admin API communicates with the public app through internal service-to-service endpoints that expose only:

- minimized metadata;
- specific account commands.

Internal endpoints require dedicated service-to-service auth.

## Consequences

Positive:

- public app retains ownership of user data and domain rules;
- admin API does not need broad DB access;
- easier to prevent content leakage;
- more explicit command boundary.

Negative:

- more endpoints;
- more auth configuration;
- network failure handling required;
- potential eventual consistency.

## Alternatives considered

### Admin API reads AccantoDb directly

Rejected as default. Acceptable only via carefully restricted views if absolutely necessary, but not preferred.

### Shared repository/service access

Rejected. Risks tight coupling.

### Event-driven projections only

Strong privacy option, but more complex for v0.1.

## Privacy impact

Positive. Internal DTOs can be constrained to metadata only.

## Security impact

Positive. Service-to-service token can be rotated and scoped separately.
