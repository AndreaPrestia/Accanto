using Accanto.Application.Account;
using Accanto.Application.Auth;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Internal;
using Accanto.Domain.Entities;
using Accanto.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Tests;

public class InternalAdminAccountServiceTests
{
    private static InternalAdminAccountService CreateService(
        AccantoDbContext db,
        IRefreshTokenService? refresh = null,
        IUserErasureService? erasure = null,
        NoOpSecurityAuditLog? audit = null)
        => new(db, refresh ?? new NoOpRefreshTokenService(), erasure ?? new NoOpUserErasureService(), audit ?? new NoOpSecurityAuditLog(), TimeProvider.System);

    private static User SeedUser(AccantoDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "User",
            PasswordHash = "x",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task Disable_sets_flag_and_metadata()
    {
        using var db = TestDb.Create();
        var user = SeedUser(db);
        await db.SaveChangesAsync();
        var audit = new NoOpSecurityAuditLog();
        var svc = CreateService(db, audit: audit);

        await svc.DisableAsync(user.Id, "Requested via support");

        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.IsDisabled.Should().BeTrue();
        reloaded.DisabledAt.Should().NotBeNull();
        reloaded.DisabledReason.Should().Be("Requested via support");
        audit.Calls.Should().Contain(c => c.EventType == Domain.Enums.SecurityAuditEventType.AllSessionsRevoked);
    }

    [Fact]
    public async Task Enable_clears_flag()
    {
        using var db = TestDb.Create();
        var user = SeedUser(db);
        user.IsDisabled = true;
        user.DisabledAt = DateTimeOffset.UtcNow;
        user.DisabledReason = "x";
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        await svc.EnableAsync(user.Id, null);

        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.IsDisabled.Should().BeFalse();
        reloaded.DisabledAt.Should().BeNull();
        reloaded.DisabledReason.Should().BeNull();
    }

    [Fact]
    public async Task Disable_unknown_user_throws_not_found()
    {
        using var db = TestDb.Create();
        var svc = CreateService(db);
        await FluentActions.Invoking(() => svc.DisableAsync(Guid.NewGuid(), "r"))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RevokeSessions_calls_revoke_all()
    {
        using var db = TestDb.Create();
        var user = SeedUser(db);
        await db.SaveChangesAsync();
        var refresh = new TrackingRefreshTokenService();
        var svc = CreateService(db, refresh: refresh);

        await svc.RevokeSessionsAsync(user.Id);

        refresh.RevokedFor.Should().Contain(user.Id);
    }

    [Fact]
    public async Task StartDeletion_requires_reason()
    {
        using var db = TestDb.Create();
        var svc = CreateService(db);
        await FluentActions.Invoking(() => svc.StartDeletionAsync(Guid.NewGuid(), "  "))
            .Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task StartDeletion_delegates_to_erasure_not_hard_delete()
    {
        using var db = TestDb.Create();
        var user = SeedUser(db);
        await db.SaveChangesAsync();
        var erasure = new NoOpUserErasureService();
        var svc = CreateService(db, erasure: erasure);

        await svc.StartDeletionAsync(user.Id, "user requested");

        erasure.Erased.Should().ContainSingle()
            .Which.UserId.Should().Be(user.Id);
        erasure.Erased[0].Reason.Should().Contain("user requested");
    }

    private sealed class TrackingRefreshTokenService : IRefreshTokenService
    {
        public List<Guid> RevokedFor { get; } = new();
        public Task<IssuedRefreshToken> IssueAsync(Guid userId, ClientInfo? client, CancellationToken cancellationToken = default)
            => Task.FromResult(new IssuedRefreshToken(Guid.NewGuid(), "noop", DateTimeOffset.UtcNow.AddDays(30)));
        public Task<(IssuedRefreshToken Token, Guid UserId)> RotateAsync(string rawToken, ClientInfo? client, CancellationToken cancellationToken = default)
            => Task.FromResult((new IssuedRefreshToken(Guid.NewGuid(), "noop", DateTimeOffset.UtcNow.AddDays(30)), Guid.Empty));
        public Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeByIdAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokedFor.Add(userId);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ActiveSessionDto>> ListActiveAsync(Guid userId, string? currentRawToken, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActiveSessionDto>>(Array.Empty<ActiveSessionDto>());
    }

    private sealed class NoOpUserErasureService : IUserErasureService
    {
        public List<(Guid UserId, string Reason)> Erased { get; } = new();
        public Task EraseAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
        {
            Erased.Add((userId, reason));
            return Task.CompletedTask;
        }
    }
}
