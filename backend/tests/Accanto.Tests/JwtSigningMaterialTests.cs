using System.IdentityModel.Tokens.Jwt;
using Accanto.Application.Common.Security;
using Accanto.Domain.Entities;
using Accanto.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Accanto.Tests;

public class JwtSigningMaterialTests
{
    private const string KeyA = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkA"; // 33 char
    private const string KeyB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbB"; // 33 char
    private const string KeyShort = "tooShort"; // 8 char

    private static User MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "u@example.com",
        DisplayName = "U",
        PasswordHash = "x"
    };

    private static JwtTokenService MakeService(JwtOptions opt)
    {
        var signing = opt.ResolveSigningMaterial();
        return new JwtTokenService(Options.Create(opt), signing);
    }

    // --- ResolveSigningMaterial -----------------------------------------

    [Fact]
    public void Legacy_single_key_resolves_with_kid_legacy()
    {
        var opt = new JwtOptions { Key = KeyA };
        var sm = opt.ResolveSigningMaterial();

        sm.ActiveKeyId.Should().Be(JwtSigningMaterial.LegacyKeyId);
        sm.Keys.Should().HaveCount(1).And.ContainKey(JwtSigningMaterial.LegacyKeyId);
    }

    [Fact]
    public void Multi_key_with_explicit_active_id_resolves()
    {
        var opt = new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA, ["k2"] = KeyB },
            ActiveKeyId = "k2"
        };
        var sm = opt.ResolveSigningMaterial();

        sm.ActiveKeyId.Should().Be("k2");
        sm.Keys.Should().HaveCount(2);
    }

    [Fact]
    public void Multi_key_without_active_id_throws()
    {
        var opt = new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA, ["k2"] = KeyB }
        };
        var act = () => opt.ResolveSigningMaterial();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ActiveKeyId*");
    }

    [Fact]
    public void Active_id_not_in_keys_throws()
    {
        var opt = new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA },
            ActiveKeyId = "k99"
        };
        var act = () => opt.ResolveSigningMaterial();
        act.Should().Throw<InvalidOperationException>().WithMessage("*k99*non presente*");
    }

    [Fact]
    public void Short_key_throws_fail_fast()
    {
        var opt = new JwtOptions { Key = KeyShort };
        var act = () => opt.ResolveSigningMaterial();
        act.Should().Throw<InvalidOperationException>().WithMessage("*troppo corta*");
    }

    [Fact]
    public void Legacy_and_explicit_keys_merge()
    {
        // Scenario di rotazione "passa da legacy a multi-key":
        // tieni Jwt__Key per i token vecchi e aggiungi Jwt__Keys__k1 +
        // Jwt__ActiveKeyId=k1 per emettere i nuovi.
        var opt = new JwtOptions
        {
            Key = KeyA,
            Keys = new() { ["k1"] = KeyB },
            ActiveKeyId = "k1"
        };
        var sm = opt.ResolveSigningMaterial();

        sm.ActiveKeyId.Should().Be("k1");
        sm.Keys.Keys.Should().BeEquivalentTo(new[] { "k1", JwtSigningMaterial.LegacyKeyId });
    }

    // --- Issue + valida tramite resolver --------------------------------

    [Fact]
    public void Issued_token_carries_active_kid_in_header()
    {
        var svc = MakeService(new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA, ["k2"] = KeyB },
            ActiveKeyId = "k2"
        });

        var token = svc.Issue(MakeUser()).Token;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Header.Kid.Should().Be("k2");
    }

    [Fact]
    public void Token_signed_with_old_key_still_validates_during_grace()
    {
        // Step 1: firma con k1
        var svc1 = MakeService(new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA },
            ActiveKeyId = "k1"
        });
        var oldToken = svc1.Issue(MakeUser()).Token;

        // Step 2: rotazione → k2 attiva ma k1 ancora presente (grace)
        var optGrace = new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA, ["k2"] = KeyB },
            ActiveKeyId = "k2"
        };
        var sm = optGrace.ResolveSigningMaterial();

        ValidateOk(oldToken, sm);
    }

    [Fact]
    public void Token_signed_with_removed_key_is_rejected()
    {
        // Step 1: firma con k1
        var svc1 = MakeService(new JwtOptions
        {
            Keys = new() { ["k1"] = KeyA },
            ActiveKeyId = "k1"
        });
        var token = svc1.Issue(MakeUser()).Token;

        // Step 2: k1 rimossa, resta solo k2 → il vecchio token non deve passare
        var optAfterGrace = new JwtOptions
        {
            Keys = new() { ["k2"] = KeyB },
            ActiveKeyId = "k2"
        };
        var sm = optAfterGrace.ResolveSigningMaterial();

        ValidateThrows(token, sm);
    }

    [Fact]
    public void Legacy_token_without_kid_validates_when_legacy_key_present()
    {
        // Simula un token vecchio: firmato direttamente senza kid esplicito,
        // come faceva la versione single-key prima della multi-kid.
        var legacyKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(KeyA));
        var creds = new SigningCredentials(legacyKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "accanto",
            audience: "accanto",
            claims: new[] { new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        // ResolverMaterial in modalita' legacy: Jwt__Key valorizzata, niente Keys.
        var sm = new JwtOptions { Key = KeyA }.ResolveSigningMaterial();
        ValidateOk(token, sm);
    }

    // --- helpers --------------------------------------------------------

    private static void ValidateOk(string token, JwtSigningMaterial sm)
    {
        var handler = new JwtSecurityTokenHandler();
        var act = () => handler.ValidateToken(token, BuildTvp(sm), out _);
        act.Should().NotThrow();
    }

    private static void ValidateThrows(string token, JwtSigningMaterial sm)
    {
        var handler = new JwtSecurityTokenHandler();
        var act = () => handler.ValidateToken(token, BuildTvp(sm), out _);
        act.Should().Throw<SecurityTokenException>();
    }

    private static TokenValidationParameters BuildTvp(JwtSigningMaterial sm) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidIssuer = "accanto",
        ValidAudience = "accanto",
        IssuerSigningKeyResolver = (_, _, kid, _) => sm.Resolve(kid),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
}
