using Accanto.Admin.Domain.Entities;

namespace Accanto.Admin.Application.Common.Security;

public sealed record AdminAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Emette access token JWT per gli admin, firmati con la chiave admin dedicata.
/// I claim includono i ruoli admin; issuer/audience sono quelli admin (mai quelli pubblici).
/// </summary>
public interface IAdminJwtTokenService
{
    AdminAccessToken Issue(AdminUser admin, IEnumerable<string> roles);
}

/// <summary>Hash delle password admin (PBKDF2). Mai plaintext.</summary>
public interface IAdminPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
