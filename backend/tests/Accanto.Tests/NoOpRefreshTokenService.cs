using Accanto.Application.Auth;

namespace Accanto.Tests;

public class NoOpRefreshTokenService : IRefreshTokenService
{
    public Task<IssuedRefreshToken> IssueAsync(Guid userId, ClientInfo? client, CancellationToken cancellationToken = default)
        => Task.FromResult(new IssuedRefreshToken(Guid.NewGuid(), "noop", DateTimeOffset.UtcNow.AddDays(30)));

    public Task<(IssuedRefreshToken Token, Guid UserId)> RotateAsync(string rawToken, ClientInfo? client, CancellationToken cancellationToken = default)
        => Task.FromResult((new IssuedRefreshToken(Guid.NewGuid(), "noop", DateTimeOffset.UtcNow.AddDays(30)), Guid.Empty));

    public Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RevokeByIdAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ActiveSessionDto>> ListActiveAsync(Guid userId, string? currentRawToken, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ActiveSessionDto>>(Array.Empty<ActiveSessionDto>());
}
