# ADR-0003 — No Content Access

## Status

Accepted

## Context

Accanto users may store information about serious illness, documents, family updates and personal notes. This is sensitive even when it is not formally classified as medical device data.

Admin access to content would create trust, privacy and reputational risks.

## Decision

Admins cannot read user-created caregiving content.

Forbidden:

- timeline title/content;
- document original filename;
- document content;
- document storage path;
- doctor question text;
- answer notes;
- shared updates;
- care circle names;
- private notes.

Allowed:

- metadata and aggregate counts needed for account operations.

## Consequences

Positive:

- strong trust boundary;
- reduced data exposure;
- lower support risk;
- clearer product ethics.

Negative:

- support/debug may be harder;
- some issues require user-provided screenshots or exports;
- future support access grants need careful design.

## Alternatives considered

### Full super-admin access

Rejected.

### Support access by default

Rejected.

### Support access only with explicit user grant

Potential future feature, not v0.1.

## Privacy impact

Strongly positive.

## Security impact

Strongly positive. There are fewer sensitive paths to secure.
