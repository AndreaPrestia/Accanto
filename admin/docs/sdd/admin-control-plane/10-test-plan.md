# 10 — Test plan

## Goals

Testare soprattutto:

- separazione auth;
- separazione dati;
- privacy boundary;
- audit log;
- operazioni admin;
- assenza di regressioni.

## Backend tests

### Auth tests

```text
[ ] Admin login succeeds with valid credentials
[ ] Admin login fails with invalid password
[ ] Admin login fails for inactive admin
[ ] Admin refresh succeeds with valid refresh token
[ ] Admin refresh fails with revoked refresh token
[ ] Admin refresh token is stored hashed
[ ] Admin logout revokes refresh token
[ ] Admin endpoints reject unauthenticated requests
[ ] Admin endpoints reject public app JWT
```

### Authorization tests

```text
[ ] Owner can disable user
[ ] Operator can disable user
[ ] SecurityAuditor cannot disable user
[ ] SecurityAuditor can read audit logs
[ ] Operator cannot access technical logs if policy forbids it
```

### Reason/audit tests

```text
[ ] Disable user requires reason
[ ] Enable user requires reason
[ ] Revoke sessions requires reason
[ ] Start deletion requires reason
[ ] Disable user writes AdminAuditLog
[ ] Enable user writes AdminAuditLog
[ ] Revoke sessions writes AdminAuditLog
[ ] Start deletion writes AdminAuditLog
```

### Operation tests

```text
[ ] Disable user creates AdminOperation
[ ] Enable user creates AdminOperation
[ ] Revoke sessions creates AdminOperation
[ ] Start deletion creates Pending/Completed operation according to implementation
[ ] Failed internal call marks operation Failed
```

### Privacy boundary tests

Admin user list response must not contain:

```text
[ ] CareCircle.Name
[ ] CareCircle.Description
[ ] TimelineEntry.Title
[ ] TimelineEntry.Content
[ ] TimelineEntry.Tags
[ ] MedicalDocument.OriginalFileName
[ ] MedicalDocument.StoragePath
[ ] MedicalDocument.Notes
[ ] DoctorQuestion.Question
[ ] DoctorQuestion.AnswerNotes
[ ] SharedUpdate.Content
```

Admin user detail response must not contain the same fields.

### Internal endpoint tests

```text
[ ] Internal endpoints reject missing service-to-service token
[ ] Internal endpoints reject public JWT
[ ] Internal endpoints reject admin frontend JWT
[ ] Internal endpoints accept valid service-to-service token
[ ] Internal user metadata returns only allowed fields
```

### Regression tests

```text
[ ] Existing user registration still works
[ ] Existing user login still works
[ ] Existing care circle features still work
[ ] Existing timeline features still work
[ ] Existing documents features still work
```

## Frontend tests

Minimum:

```text
[ ] Admin login page renders
[ ] Dashboard renders after auth
[ ] User list renders metadata only
[ ] User detail renders metadata only
[ ] Disable modal requires reason
[ ] Enable modal requires reason
[ ] Revoke sessions modal requires reason
[ ] Start deletion modal requires reason
[ ] Logout clears session
```

## Manual QA

```text
[ ] Run docker-compose up
[ ] Create/seed admin
[ ] Login admin
[ ] View dashboard
[ ] View users
[ ] Open user detail
[ ] Confirm no sensitive content is visible
[ ] Disable user
[ ] Verify user cannot login/access app
[ ] Enable user
[ ] Verify user can access again
[ ] Revoke sessions
[ ] Verify audit log
[ ] Check operations page
[ ] Check health page
```

## Security QA

```text
[ ] Try public JWT against admin endpoint
[ ] Try admin JWT against internal endpoint
[ ] Try no token against internal endpoint
[ ] Inspect network response for forbidden fields
[ ] Inspect logs for sensitive payloads
[ ] Confirm Swagger is not exposed in production mode
```
