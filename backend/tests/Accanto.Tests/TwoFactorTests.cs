using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using FluentAssertions;
using OtpNet;

namespace Accanto.Tests;

public class TwoFactorTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;

    public TwoFactorTests(AccantoFactory factory) { _factory = factory; }

    private async Task<(AuthResponse Auth, HttpClient Client)> RegisterAndAuthAsync(string email)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "U", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth, client);
    }

    private static string CurrentCode(string secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.ComputeTotp();
    }

    [Fact]
    public async Task Setup_then_enable_then_login_requires_totp()
    {
        var (_, client) = await RegisterAndAuthAsync("tfa1@example.com");

        var setupResp = await client.PostAsync("/api/account/2fa/setup", content: null);
        setupResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var setup = await setupResp.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(JsonOpts);
        setup!.Secret.Should().NotBeNullOrWhiteSpace();
        setup.OtpAuthUri.Should().StartWith("otpauth://totp/");

        var enableResp = await client.PostAsJsonAsync("/api/account/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(setup.Secret)));
        enableResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var enable = await enableResp.Content.ReadFromJsonAsync<EnableTwoFactorResponse>(JsonOpts);
        enable!.RecoveryCodes.Should().HaveCount(10);

        // Nuova login → richiede 2FA
        var anon = _factory.CreateClient();
        var loginResp = await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tfa1@example.com", "passwordSegreta1"));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResult>(JsonOpts);
        login!.RequiresTwoFactor.Should().BeTrue();
        login.Auth.Should().BeNull();
        login.TwoFactorToken.Should().NotBeNullOrWhiteSpace();

        // Codice sbagliato → 403
        var bad = await anon.PostAsJsonAsync("/api/auth/two-factor",
            new TwoFactorLoginRequest(login.TwoFactorToken!, "000000", null));
        bad.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Codice corretto → AuthResponse
        var good = await anon.PostAsJsonAsync("/api/auth/two-factor",
            new TwoFactorLoginRequest(login.TwoFactorToken!, CurrentCode(setup.Secret), null));
        good.StatusCode.Should().Be(HttpStatusCode.OK);
        var authFinal = await good.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        authFinal!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Recovery_code_consumes_and_works_once()
    {
        var (_, client) = await RegisterAndAuthAsync("tfa2@example.com");

        var setup = await (await client.PostAsync("/api/account/2fa/setup", null))
            .Content.ReadFromJsonAsync<TwoFactorSetupResponse>(JsonOpts);
        var enable = await (await client.PostAsJsonAsync("/api/account/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(setup!.Secret))))
            .Content.ReadFromJsonAsync<EnableTwoFactorResponse>(JsonOpts);
        var recovery = enable!.RecoveryCodes[0];

        var anon = _factory.CreateClient();
        var login = await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tfa2@example.com", "passwordSegreta1")))
            .Content.ReadFromJsonAsync<LoginResult>(JsonOpts);

        // Recovery code valido → OK
        var first = await anon.PostAsJsonAsync("/api/auth/two-factor",
            new TwoFactorLoginRequest(login!.TwoFactorToken!, null, recovery));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Stesso codice di recupero usato due volte → fallisce
        var anon2 = _factory.CreateClient();
        var login2 = await (await anon2.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tfa2@example.com", "passwordSegreta1")))
            .Content.ReadFromJsonAsync<LoginResult>(JsonOpts);
        var second = await anon2.PostAsJsonAsync("/api/auth/two-factor",
            new TwoFactorLoginRequest(login2!.TwoFactorToken!, null, recovery));
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Disable_2fa_restores_direct_login()
    {
        var (_, client) = await RegisterAndAuthAsync("tfa3@example.com");

        var setup = await (await client.PostAsync("/api/account/2fa/setup", null))
            .Content.ReadFromJsonAsync<TwoFactorSetupResponse>(JsonOpts);
        await client.PostAsJsonAsync("/api/account/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(setup!.Secret)));

        var disable = await client.PostAsJsonAsync("/api/account/2fa/disable",
            new DisableTwoFactorRequest("passwordSegreta1", CurrentCode(setup.Secret), null));
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anon = _factory.CreateClient();
        var loginResp = await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tfa3@example.com", "passwordSegreta1"));
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResult>(JsonOpts);
        login!.RequiresTwoFactor.Should().BeFalse();
        login.Auth.Should().NotBeNull();
        login.Auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }
}
