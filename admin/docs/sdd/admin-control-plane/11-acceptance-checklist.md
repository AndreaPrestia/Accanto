# 11 — Acceptance checklist

## Build and runtime

```text
[ ] Existing backend builds
[ ] Existing frontend builds
[ ] Admin API builds
[ ] Admin frontend builds
[ ] Tests pass
[ ] docker-compose config is valid
[ ] docker-compose starts existing services
[ ] docker-compose starts postgres-admin
[ ] docker-compose starts accanto-admin-api
[ ] docker-compose starts accanto-admin-web
```

## Separation

```text
[ ] Admin API is separate
[ ] Admin frontend is separate
[ ] Admin DB is separate
[ ] Admin users are not in public Users table
[ ] No User.IsAdmin exists
[ ] Public app auth is not reused for admin
[ ] Admin JWT settings are separate
[ ] Internal service-to-service auth settings are separate
```

## Admin auth

```text
[ ] Admin can login
[ ] Admin can logout
[ ] Admin can refresh token
[ ] Admin can call /me
[ ] Invalid credentials fail
[ ] Inactive admin cannot login
[ ] Admin endpoints reject public JWT
[ ] Admin endpoints reject unauthenticated requests
```

## User metadata

```text
[ ] Admin can list users
[ ] Admin can view user detail
[ ] User list shows only metadata
[ ] User detail shows only metadata
[ ] No care circle names are returned
[ ] No timeline title/content is returned
[ ] No document original filename is returned
[ ] No document storage path is returned
[ ] No doctor question is returned
[ ] No shared update content is returned
```

## User operations

```text
[ ] Owner can disable user
[ ] Operator can disable user
[ ] SecurityAuditor cannot disable user
[ ] Owner can enable user
[ ] Operator can enable user
[ ] Owner can revoke sessions
[ ] Operator can revoke sessions
[ ] Owner can start deletion request
[ ] Operator can start deletion request
[ ] All mutating actions require reason
[ ] Hard delete is not performed directly by Admin API
```

## Audit and operations

```text
[ ] Disable user writes audit log
[ ] Enable user writes audit log
[ ] Revoke sessions writes audit log
[ ] Start deletion writes audit log
[ ] Audit log does not contain sensitive payloads
[ ] Operations page shows operation status
[ ] Failed operations are visible
```

## Privacy

```text
[ ] Admin cannot read timeline
[ ] Admin cannot read documents
[ ] Admin cannot download documents
[ ] Admin cannot read doctor questions
[ ] Admin cannot read shared updates
[ ] Admin cannot read private notes
[ ] Admin cannot see original filenames
[ ] Admin cannot see care circle names
[ ] Admin cannot impersonate users
```

## Frontend

```text
[ ] Admin login page exists
[ ] Dashboard exists
[ ] Users page exists
[ ] User detail page exists
[ ] Audit logs page exists
[ ] Operations page exists
[ ] System page exists
[ ] Mutating actions use reason modal
[ ] UI does not display forbidden fields
```

## Documentation

```text
[ ] docs/admin-system.md exists
[ ] README or docs mention admin setup
[ ] Admin DB setup documented
[ ] First admin creation documented
[ ] Privacy boundary documented
[ ] Internal endpoints documented
[ ] Audit log documented
[ ] Forbidden admin capabilities documented
```

## Final decision

```text
[ ] Accepted
[ ] Accepted with follow-ups
[ ] Rejected
```

## Follow-ups

```text
- 
```
