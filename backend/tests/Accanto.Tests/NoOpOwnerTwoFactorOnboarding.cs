using Accanto.Application.Auth.TwoFactor;

namespace Accanto.Tests;

public sealed class NoOpOwnerTwoFactorOnboarding : IOwnerTwoFactorOnboarding
{
    public List<(Guid UserId, string CircleName)> Calls { get; } = new();

    public Task OnPromotedToOwnerAsync(Guid userId, string circleName, CancellationToken cancellationToken = default)
    {
        Calls.Add((userId, circleName));
        return Task.CompletedTask;
    }
}
