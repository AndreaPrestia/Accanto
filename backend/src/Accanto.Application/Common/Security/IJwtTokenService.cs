using Accanto.Domain.Entities;

namespace Accanto.Application.Common.Security;

public interface IJwtTokenService
{
    AccessToken Issue(User user);

    /// <summary>Token JWT firmato di breve durata per il flusso di challenge 2FA.</summary>
    TwoFactorChallengeToken IssueTwoFactorChallenge(Guid userId, TimeSpan lifetime);

    /// <summary>Convalida un token 2FA emesso da <see cref="IssueTwoFactorChallenge"/>; ritorna lo userId se valido.</summary>
    Guid? ValidateTwoFactorChallenge(string token);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed record TwoFactorChallengeToken(string Token, DateTimeOffset ExpiresAt);
