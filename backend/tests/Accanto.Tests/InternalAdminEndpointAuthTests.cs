using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Accanto.Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Accanto.Tests;

/// <summary>
/// Verifica che gli endpoint interni /internal/admin/* accettino SOLO il token
/// service-to-service (InternalAdmin) e rifiutino sia i JWT pubblici sia i JWT
/// admin-frontend. Privacy/security boundary (08-security-model.md).
/// </summary>
public class InternalAdminEndpointAuthTests
{
    private const string InternalKey = "internal-admin-test-key-at-least-32-chars-long";

    private sealed class InternalFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("InternalAdmin:Issuer", "accanto-internal-admin");
            builder.UseSetting("InternalAdmin:Audience", "accanto-internal-admin");
            builder.UseSetting("InternalAdmin:SigningKey", InternalKey);
        }
    }

    private static string MintToken(string issuer, string audience, string key, params Claim[] extraClaims)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) };
        claims.AddRange(extraClaims);
        var token = new JwtSecurityToken(issuer, audience, claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Internal_endpoint_accepts_service_token()
    {
        await using var factory = new InternalFactory();
        var client = factory.CreateClient();
        var token = MintToken("accanto-internal-admin", "accanto-internal-admin", InternalKey);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var resp = await client.GetAsync("/internal/admin/users");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Internal_endpoint_rejects_public_user_jwt()
    {
        await using var factory = new InternalFactory();
        var client = factory.CreateClient();

        // JWT pubblico emesso dalla registration flow (issuer/audience "accanto-test").
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("pub@example.com", "Pub", "passwordSegreta1"));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var resp = await client.GetAsync("/internal/admin/users");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Internal_endpoint_rejects_admin_frontend_jwt()
    {
        await using var factory = new InternalFactory();
        var client = factory.CreateClient();

        // Token con issuer/audience admin-frontend (diversi da internal) ma stessa chiave internal:
        // deve comunque essere rifiutato perche' issuer/audience non corrispondono.
        var adminToken = MintToken("accanto-admin", "accanto-admin", InternalKey);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        var resp = await client.GetAsync("/internal/admin/users");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Internal_endpoint_rejects_anonymous()
    {
        await using var factory = new InternalFactory();
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/internal/admin/users");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
