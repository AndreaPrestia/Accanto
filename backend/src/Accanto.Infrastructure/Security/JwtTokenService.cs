using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Accanto.Application.Common.Security;
using Accanto.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Accanto.Infrastructure.Security;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;

    public JwtTokenService(IOptions<JwtOptions> opt) { _opt = opt.Value; }

    public AccessToken Issue(User user)
    {
        if (string.IsNullOrWhiteSpace(_opt.Key) || _opt.Key.Length < 32)
            throw new InvalidOperationException("Jwt key non configurata o troppo corta (almeno 32 caratteri).");

        var expires = DateTimeOffset.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(jwt, expires);
    }

    private const string TwoFactorPurposeClaim = "purpose";
    private const string TwoFactorPurposeValue = "2fa";

    public TwoFactorChallengeToken IssueTwoFactorChallenge(Guid userId, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(_opt.Key) || _opt.Key.Length < 32)
            throw new InvalidOperationException("Jwt key non configurata o troppo corta (almeno 32 caratteri).");

        var expires = DateTimeOffset.UtcNow.Add(lifetime);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(TwoFactorPurposeClaim, TwoFactorPurposeValue),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new TwoFactorChallengeToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public Guid? ValidateTwoFactorChallenge(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                // Stesso hardening del bearer principale: blocca algorithm
                // confusion (token forgiati con alg=none o cambio algoritmo).
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = _opt.Issuer,
                ValidAudience = _opt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)),
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var purpose = principal.FindFirst(TwoFactorPurposeClaim)?.Value;
            if (purpose != TwoFactorPurposeValue) return null;

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
