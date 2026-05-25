using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Account;
using Accanto.Application.Auth;
using FluentAssertions;

namespace Accanto.Tests;

public class RefreshTokenTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;

    public RefreshTokenTests(AccantoFactory factory) { _factory = factory; }

    private async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "U", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        return auth;
    }

    [Fact]
    public async Task Register_returns_access_and_refresh_token()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "rt1@example.com");
        auth.RefreshTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_is_revoked()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "rt2@example.com");

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = await refreshed.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        next!.RefreshToken.Should().NotBe(auth.RefreshToken);

        // reusing the OLD token must fail (reuse detection)
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        reuse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // and after reuse detection the new token is ALSO revoked
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(next.RefreshToken));
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "rt3@example.com");

        var logout = await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(auth.RefreshToken));
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        refreshed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sessions_list_and_revoke()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "rt4@example.com");

        // second login → second session
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("rt4@example.com", "passwordSegreta1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResult>(JsonOpts);
        loginResult.Should().NotBeNull();
        loginResult!.RequiresTwoFactor.Should().BeFalse();
        var auth2 = loginResult.Auth!;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth2!.AccessToken);

        var listResp = await client.GetAsync($"/api/account/sessions?current={Uri.EscapeDataString(auth2.RefreshToken)}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await listResp.Content.ReadFromJsonAsync<List<ActiveSessionDto>>(JsonOpts);
        sessions.Should().NotBeNull();
        sessions!.Should().HaveCount(2);
        sessions.Should().ContainSingle(s => s.Current);

        var other = sessions.First(s => !s.Current);
        var del = await client.DeleteAsync($"/api/account/sessions/{other.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // the revoked session's refresh token must no longer work
        var refreshRevoked = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        refreshRevoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Change_password_revokes_all_sessions()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "rt5@example.com");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var change = await client.PostAsJsonAsync("/api/account/change-password",
            new ChangePasswordRequest("passwordSegreta1", "nuovaPassword999"));
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        refreshed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
