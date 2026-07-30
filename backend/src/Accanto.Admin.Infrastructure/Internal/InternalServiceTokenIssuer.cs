using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Accanto.Admin.Infrastructure.Internal;

/// <summary>
/// Minta token JWT service-to-service di breve durata per chiamare gli endpoint
/// interni della app pubblica. Firmati con la chiave InternalAdmin condivisa.
/// </summary>
public class InternalServiceTokenIssuer
{
    private readonly InternalAppOptions _opt;

    public InternalServiceTokenIssuer(IOptions<InternalAppOptions> opt)
    {
        _opt = opt.Value;
    }

    public string Issue()
    {
        var creds = new SigningCredentials(_opt.ResolveKey(), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "accanto-admin-api"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(Math.Max(1, _opt.TokenLifetimeMinutes)),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
