using Accanto.Domain.Entities;

namespace Accanto.Application.Common.Security;

public interface IJwtTokenService
{
    AccessToken Issue(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
