# 05 — Admin API Spec

## Base URL

```text
/admin-api
```

oppure, se il progetto preferisce:

```text
/api/admin
```

Preferenza per controller:

```text
/api/admin/auth
/api/admin/users
/api/admin/audit-logs
/api/admin/operations
/api/admin/system
```

## Authentication

Tutti gli endpoint, tranne login e refresh dove appropriato, richiedono Admin JWT.

JWT utenti pubblici devono essere rifiutati.

## Error format

Usare `ProblemDetails` o formato coerente:

```json
{
  "error": "ValidationError",
  "message": "Reason is required."
}
```

## Auth endpoints

### POST /api/admin/auth/login

Request:

```json
{
  "email": "admin@example.com",
  "password": "password"
}
```

Response:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2026-07-27T10:00:00Z",
  "adminUser": {
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "admin@example.com",
    "displayName": "Admin",
    "roles": ["Owner"]
  }
}
```

Audit:

```text
Admin.Login
```

### POST /api/admin/auth/refresh

Request:

```json
{
  "refreshToken": "..."
}
```

Response:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2026-07-27T10:00:00Z"
}
```

### POST /api/admin/auth/logout

Request:

```json
{
  "refreshToken": "..."
}
```

Response:

```json
{
  "ok": true
}
```

Audit:

```text
Admin.Logout
```

### GET /api/admin/auth/me

Response:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "email": "admin@example.com",
  "displayName": "Admin",
  "roles": ["Owner"]
}
```

## Users endpoints

### GET /api/admin/users

Query params:

```text
q
status
page
pageSize
sort
```

Response:

```json
{
  "items": [
    {
      "userId": "00000000-0000-0000-0000-000000000000",
      "email": "user@example.com",
      "displayName": "User",
      "createdAt": "2026-07-27T10:00:00Z",
      "lastLoginAt": "2026-07-27T10:00:00Z",
      "isDisabled": false,
      "accountStatus": "Active",
      "careCircleCount": 2,
      "documentsCount": 5,
      "storageUsedBytes": 123456,
      "timelineEntryCount": 10
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

Must not contain:

```text
CareCircle.Name
TimelineEntry.Title
TimelineEntry.Content
MedicalDocument.OriginalFileName
MedicalDocument.StoragePath
DoctorQuestion.Question
SharedUpdate.Content
```

### GET /api/admin/users/{userId}

Response:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "user@example.com",
  "displayName": "User",
  "createdAt": "2026-07-27T10:00:00Z",
  "lastLoginAt": "2026-07-27T10:00:00Z",
  "isDisabled": false,
  "accountStatus": "Active",
  "careCircleCount": 2,
  "documentsCount": 5,
  "storageUsedBytes": 123456,
  "timelineEntryCount": 10,
  "disabledAt": null,
  "disabledReason": null
}
```

### POST /api/admin/users/{userId}/disable

Request:

```json
{
  "reason": "Requested by user via support email."
}
```

Response:

```json
{
  "operationId": "00000000-0000-0000-0000-000000000000",
  "status": "Completed"
}
```

Rules:

- `reason` required;
- admin role: Owner or Operator;
- writes audit log;
- calls internal app command.

Audit:

```text
User.Disable
```

### POST /api/admin/users/{userId}/enable

Same structure.

Audit:

```text
User.Enable
```

### POST /api/admin/users/{userId}/revoke-sessions

Same structure.

Audit:

```text
User.RevokeSessions
```

### POST /api/admin/users/{userId}/deletion-requests

Request:

```json
{
  "reason": "User requested deletion."
}
```

Response:

```json
{
  "operationId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending"
}
```

Rules:

- must not hard-delete immediately from admin API;
- starts app-owned workflow;
- audit required.

Audit:

```text
User.StartDeletion
```

## Audit endpoints

### GET /api/admin/audit-logs

Query params:

```text
adminUserId
action
targetType
targetId
from
to
page
pageSize
```

Response:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "adminUserId": "00000000-0000-0000-0000-000000000000",
      "adminEmail": "admin@example.com",
      "action": "User.Disable",
      "targetType": "User",
      "targetId": "00000000-0000-0000-0000-000000000000",
      "reason": "Requested by user.",
      "ipAddress": "127.0.0.1",
      "userAgent": "Mozilla/5.0",
      "createdAt": "2026-07-27T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

## Operations endpoints

### GET /api/admin/operations

Response:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "operationType": "DisableUser",
      "targetUserId": "00000000-0000-0000-0000-000000000000",
      "status": "Completed",
      "reason": "Requested by user.",
      "createdAt": "2026-07-27T10:00:00Z",
      "completedAt": "2026-07-27T10:00:01Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

### GET /api/admin/operations/{operationId}

Returns one operation.

## System endpoints

### GET /api/admin/system/health

Response:

```json
{
  "adminApi": "Healthy",
  "adminDb": "Healthy",
  "publicApiInternal": "Healthy",
  "checkedAt": "2026-07-27T10:00:00Z"
}
```

### GET /api/admin/system/technical-logs

Restricted to Owner/SecurityAuditor.

Must not return:

- request body;
- response body;
- user content;
- original filenames;
- timeline content;
- doctor questions;
- shared updates.

## Required authorization matrix

| Endpoint | Owner | Operator | SecurityAuditor |
|---|---:|---:|---:|
| GET /auth/me | Yes | Yes | Yes |
| GET /users | Yes | Yes | Read-only |
| GET /users/{id} | Yes | Yes | Read-only |
| POST disable | Yes | Yes | No |
| POST enable | Yes | Yes | No |
| POST revoke-sessions | Yes | Yes | No |
| POST deletion-requests | Yes | Yes | No |
| GET audit-logs | Yes | No/Maybe | Yes |
| GET operations | Yes | Yes | Yes |
| GET technical-logs | Yes | No | Yes |
