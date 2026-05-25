using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Auth;
using Accanto.Application.Common;
using Accanto.Application.Security;
using Accanto.Domain.Enums;
using FluentAssertions;

namespace Accanto.Tests;

public class SecurityAuditTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;
    public SecurityAuditTests(AccantoFactory factory) { _factory = factory; }

    private async Task<(AuthResponse Auth, HttpClient Client, string Email)> RegisterAndAuthAsync(string email)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "U", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth, client, email);
    }

    [Fact]
    public async Task Register_logs_account_registered()
    {
        var (_, client, _) = await RegisterAndAuthAsync($"audit-{Guid.NewGuid():N}@example.com");

        var page = await client.GetFromJsonAsync<PagedResult<SecurityAuditEntryDto>>("/api/account/security-audit", JsonOpts);
        page!.Items.Should().Contain(e => e.EventType == SecurityAuditEventType.AccountRegistered);
    }

    [Fact]
    public async Task Login_failure_logs_login_failed_and_success_logs_login_success()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.com";
        var (_, _, _) = await RegisterAndAuthAsync(email);

        var anon = _factory.CreateClient();
        var bad = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrongpass1234"));
        bad.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ok = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "passwordSegreta1"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await ok.Content.ReadFromJsonAsync<LoginResult>(JsonOpts);
        var token = auth!.Auth!.AccessToken;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var page = await client.GetFromJsonAsync<PagedResult<SecurityAuditEntryDto>>("/api/account/security-audit", JsonOpts);

        page!.Items.Should().Contain(e => e.EventType == SecurityAuditEventType.LoginSuccess);
        // Il LoginFailed con email sconosciuta ha UserId=null, quindi non appare nella lista filtrata per userId.
        // Ma il LoginFailed con password errata su account esistente sì:
        page.Items.Should().Contain(e => e.EventType == SecurityAuditEventType.LoginFailed);
    }

    [Fact]
    public async Task ChangePassword_logs_password_changed_and_all_sessions_revoked()
    {
        var (_, client, _) = await RegisterAndAuthAsync($"audit-{Guid.NewGuid():N}@example.com");
        var resp = await client.PostAsJsonAsync("/api/account/change-password",
            new { CurrentPassword = "passwordSegreta1", NewPassword = "nuovaPassword456" });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await client.GetFromJsonAsync<PagedResult<SecurityAuditEntryDto>>("/api/account/security-audit", JsonOpts);
        page!.Items.Should().Contain(e => e.EventType == SecurityAuditEventType.PasswordChanged);
        page.Items.Should().Contain(e => e.EventType == SecurityAuditEventType.AllSessionsRevoked);
    }
}
