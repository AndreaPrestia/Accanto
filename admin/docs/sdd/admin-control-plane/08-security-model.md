# 08 — Security model

## Core principle

Admin security must be stricter than public user security because admin actions can affect accounts.

## Authentication separation

Admin auth uses separate settings:

```text
AdminJwt__Issuer
AdminJwt__Audience
AdminJwt__SigningKey
```

Public app auth uses separate settings:

```text
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
```

Internal service-to-service auth uses separate settings:

```text
InternalAdmin__Issuer
InternalAdmin__Audience
InternalAdmin__SigningKey
```

## JWT rules

- Admin endpoints accept only Admin JWT.
- Admin API rejects public app JWT.
- Public app endpoints reject Admin JWT unless explicitly internal and intended.
- Internal endpoints accept only service-to-service token.
- Signing keys must be different.

## Password hashing

Admin passwords must be hashed using a secure password hasher.

Acceptable:

- ASP.NET Core `PasswordHasher<T>`;
- Argon2id if already available;
- BCrypt if already available.

Do not store plaintext passwords.

## Refresh tokens

- Store refresh token hash only.
- Rotate refresh token on refresh.
- Revoke old refresh token.
- Expire refresh tokens.
- Allow logout to revoke refresh token.

## Rate limiting

Required:

- admin login endpoint;
- refresh endpoint optional;
- internal endpoints optional but recommended.

## CORS

Admin CORS separate:

```text
AdminCors__AllowedOrigins=https://admin.accanto.care
```

Development can allow localhost.

## Admin creation

No public registration for admin.

Development seed is allowed only if:

- no admin user exists;
- env seed values are present;
- password is not logged;
- production behavior is documented.

Suggested env:

```text
AdminSeed__Email
AdminSeed__Password
AdminSeed__DisplayName
```

## Authorization

Roles:

```text
Owner
Operator
SecurityAuditor
```

Rules:

- Owner can do all admin operations.
- Operator can manage user account operations.
- SecurityAuditor can view audit/logs but cannot mutate users.

## Mutating actions

Every mutating action requires:

- authenticated admin;
- authorized role;
- non-empty reason;
- audit log;
- operation record;
- service-to-service call if affecting public app.

## Audit log

Audit log entries must include:

- AdminUserId;
- Action;
- TargetType;
- TargetId;
- Reason;
- IpAddress;
- UserAgent;
- CreatedAt.

Audit log must not include:

- request body;
- response body;
- user content;
- original filenames;
- timeline content;
- doctor questions;
- shared updates.

## Logging

Production logging must avoid:

- body logging;
- document names;
- document content;
- timeline content;
- doctor questions;
- shared updates;
- auth tokens;
- refresh tokens;
- passwords;
- full stacktrace in responses.

## Technical logs

Admin technical logs must be filtered and non-sensitive.

If logs cannot be guaranteed non-sensitive, do not expose them in Admin v0.1.

## Internal endpoint security

Internal endpoints:

- must not be reachable by browser clients;
- must require service-to-service auth;
- must validate issuer/audience;
- must expose only metadata or specific commands;
- must never return forbidden content.

## No impersonation

Impersonation is explicitly forbidden.

No:

- login as user;
- view as user;
- support session inside user account;
- token minting for user;
- bypass membership.

## Deletion safety

Admin does not hard-delete data directly.

Admin starts deletion workflow.

The application domain owns:

- deletion;
- anonymization;
- retention;
- cascade rules;
- file deletion.

## Security tests

Required tests:

- admin endpoints reject unauthenticated requests;
- admin endpoints reject public JWT;
- public endpoints reject admin JWT where applicable;
- SecurityAuditor cannot mutate users;
- Operator cannot read restricted logs if policy says no;
- reason required;
- audit log written;
- refresh token hash stored;
- no forbidden fields in metadata response.
