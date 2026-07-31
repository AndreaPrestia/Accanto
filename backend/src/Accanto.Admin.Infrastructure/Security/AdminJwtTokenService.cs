using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Accanto.Admin.Infrastructure.Security;

public class AdminJwtTokenService : IAdminJwtTokenService
{
    private readonly AdminJwtOptions _opt;
    private readonly AdminJwtSigningMaterial _signing;

    public AdminJwtTokenService(IOptions<AdminJwtOptions> opt, AdminJwtSigningMaterial signing)
    {
        _opt = opt.Value;
        _signing = signing;
    }

    public AdminAccessToken Issue(AdminUser admin, IEnumerable<string> roles)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var creds = new SigningCredentials(_signing.ActiveKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email),
            new("name", admin.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new AdminAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
