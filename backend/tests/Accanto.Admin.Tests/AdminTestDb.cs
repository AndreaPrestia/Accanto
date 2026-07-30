using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Users;
using Accanto.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Tests;

internal static class AdminTestDb
{
    public static AccantoAdminDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<AccantoAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        return new AccantoAdminDbContext(opts);
    }
}

internal sealed class FakeInternalAppClient : IInternalAppClient
{
    public List<(string Op, Guid UserId, string? Reason)> Calls { get; } = new();
    public bool ThrowOnCommand { get; set; }

    public Task<AdminUserListResponse> ListUsersAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new AdminUserListResponse(Array.Empty<AdminUserMetadataDto>(), page, pageSize, 0));

    public Task<AdminUserMetadataDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<AdminUserMetadataDto?>(null);

    public Task DisableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default) => Record("Disable", userId, reason);
    public Task EnableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default) => Record("Enable", userId, reason);
    public Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default) => Record("Revoke", userId, null);
    public Task StartUserDeletionAsync(Guid userId, string reason, CancellationToken cancellationToken = default) => Record("Delete", userId, reason);

    private Task Record(string op, Guid userId, string? reason)
    {
        if (ThrowOnCommand) throw new InvalidOperationException("internal app failure");
        Calls.Add((op, userId, reason));
        return Task.CompletedTask;
    }
}

internal sealed class NoOpAdminAuditLog : IAdminAuditLog
{
    public List<(Guid AdminUserId, string Action, string TargetType, string? TargetId, string? Reason)> Calls { get; } = new();

    public Task WriteAsync(Guid adminUserId, string action, string targetType, string? targetId = null,
        string? reason = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((adminUserId, action, targetType, targetId, reason));
        return Task.CompletedTask;
    }
}
