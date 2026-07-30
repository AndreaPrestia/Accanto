using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Domain.Entities;

namespace Accanto.Admin.Tests;

/// <summary>Password hasher deterministico per test (formato "hashed:{pw}").</summary>
internal sealed class FakeAdminPasswordHasher : IAdminPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string password, string hash) => hash == Hash(password);
}

/// <summary>JWT issuer finto: ritorna un token opaco, nessuna firma reale.</summary>
internal sealed class FakeAdminJwtTokenService : IAdminJwtTokenService
{
    public AdminAccessToken Issue(AdminUser admin, IEnumerable<string> roles)
        => new($"fake-access-token-{admin.Id}", DateTimeOffset.UtcNow.AddMinutes(60));
}
