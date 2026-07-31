# 07 — Data model

## Database

Admin data lives in:

```text
AccantoAdminDb
```

Connection string:

```text
ConnectionStrings__AdminDatabase
```

The admin database is separate from the public app database.

## Entities

### AdminUser

```csharp
public sealed class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool MfaEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<AdminUserRole> Roles { get; set; } = new List<AdminUserRole>();
    public ICollection<AdminSession> Sessions { get; set; } = new List<AdminSession>();
}
```

Constraints:

- Email required;
- Email unique;
- PasswordHash required;
- DisplayName required;
- CreatedAt required.

### AdminRole

```csharp
public sealed class AdminRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}
```

Seed roles:

```text
Owner
Operator
SecurityAuditor
```

### AdminUserRole

```csharp
public sealed class AdminUserRole
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid AdminRoleId { get; set; }

    public AdminUser AdminUser { get; set; } = default!;
    public AdminRole AdminRole { get; set; } = default!;
}
```

Unique index:

```text
AdminUserId + AdminRoleId
```

### AdminSession

```csharp
public sealed class AdminSession
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string RefreshTokenHash { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public AdminUser AdminUser { get; set; } = default!;
}
```

Rules:

- store only refresh token hash;
- never store raw refresh token;
- revoke sets RevokedAt.

### AdminAuditLog

```csharp
public sealed class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string Action { get; set; } = default!;
    public string TargetType { get; set; } = default!;
    public string? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AdminUser AdminUser { get; set; } = default!;
}
```

Rules:

- no sensitive payloads;
- no request body;
- no response body;
- no user content;
- reason length bounded.

### AdminOperation

```csharp
public sealed class AdminOperation
{
    public Guid Id { get; set; }
    public Guid RequestedByAdminUserId { get; set; }
    public AdminOperationType OperationType { get; set; }
    public Guid? TargetUserId { get; set; }
    public AdminOperationStatus Status { get; set; }
    public string Reason { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public AdminUser RequestedByAdminUser { get; set; } = default!;
}
```

Enums:

```csharp
public enum AdminOperationType
{
    DisableUser = 1,
    EnableUser = 2,
    RevokeUserSessions = 3,
    StartUserDeletion = 4
}

public enum AdminOperationStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
```

## Indexes

```text
AdminUsers.Email unique
AdminUserRoles.AdminUserId + AdminRoleId unique
AdminSessions.AdminUserId
AdminSessions.RefreshTokenHash
AdminSessions.ExpiresAt
AdminAuditLogs.AdminUserId
AdminAuditLogs.Action
AdminAuditLogs.TargetType
AdminAuditLogs.TargetId
AdminAuditLogs.CreatedAt
AdminOperations.TargetUserId
AdminOperations.Status
AdminOperations.OperationType
AdminOperations.CreatedAt
```

## Migrations

Migration context:

```text
AccantoAdminDbContext
```

Commands:

```bash
dotnet ef migrations add InitialAdminCreate \
  --project backend/src/Accanto.Admin.Infrastructure \
  --startup-project backend/src/Accanto.Admin.Api \
  --context AccantoAdminDbContext

dotnet ef database update \
  --project backend/src/Accanto.Admin.Infrastructure \
  --startup-project backend/src/Accanto.Admin.Api \
  --context AccantoAdminDbContext
```

Adapt paths if repository differs.

## Forbidden persistence

Do not persist in Admin DB:

- care circle names;
- timeline titles;
- timeline contents;
- document original filenames;
- document storage paths;
- doctor questions;
- shared updates;
- file contents;
- raw refresh tokens;
- request bodies.
