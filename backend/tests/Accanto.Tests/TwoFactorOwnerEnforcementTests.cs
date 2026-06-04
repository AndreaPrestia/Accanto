using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Auth;
using Accanto.Application.CareCircles;
using Accanto.Domain.Entities;
using Accanto.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Tests;

public class TwoFactorOwnerEnforcementTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;
    public TwoFactorOwnerEnforcementTests(AccantoFactory factory) { _factory = factory; }

    private async Task<(HttpClient Client, Guid UserId, Guid CircleId)> RegisterOwnerAsync()
    {
        var client = _factory.CreateClient();
        var email = $"2fa-own-{Guid.NewGuid():N}@example.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Owner", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var create = await client.PostAsJsonAsync("/api/care-circles",
            new CreateCareCircleRequest("Mamma", null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var circle = await create.Content.ReadFromJsonAsync<CareCircleDto>(JsonOpts);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
            var user = db.Users.Single(u => u.Email == email);
            userId = user.Id;
        }
        return (client, userId, circle!.Id);
    }

    private void SetDeadline(Guid userId, DateTimeOffset? when)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
        var user = db.Users.Single(u => u.Id == userId);
        user.TwoFactorRequiredFromUtc = when;
        db.SaveChanges();
    }

    [Fact]
    public async Task Owner_within_grace_passes_and_exposes_header()
    {
        var (client, userId, circleId) = await RegisterOwnerAsync();
        // Promozione runtime (createAsync) ha gia' settato la deadline ~7gg nel futuro.
        var resp = await client.GetAsync($"/api/care-circles/{circleId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.Contains("X-2FA-Required-By").Should().BeTrue();
    }

    [Fact]
    public async Task Owner_past_grace_is_blocked_with_403_and_problem_code()
    {
        var (client, userId, circleId) = await RegisterOwnerAsync();
        SetDeadline(userId, DateTimeOffset.UtcNow.AddHours(-1));

        var resp = await client.GetAsync($"/api/care-circles/{circleId}");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("two_factor_required_for_owner");
    }

    [Fact]
    public async Task Owner_past_grace_can_still_reach_2fa_setup()
    {
        var (client, userId, _) = await RegisterOwnerAsync();
        SetDeadline(userId, DateTimeOffset.UtcNow.AddHours(-1));

        var resp = await client.PostAsync("/api/account/2fa/setup", content: null);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_past_grace_can_still_logout()
    {
        var (client, userId, _) = await RegisterOwnerAsync();
        SetDeadline(userId, DateTimeOffset.UtcNow.AddHours(-1));

        var resp = await client.PostAsync("/api/auth/logout", content: null);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonOwner_user_is_never_blocked()
    {
        // Registriamo un utente che NON crea nessun cerchio: non e' Owner.
        var client = _factory.CreateClient();
        var email = $"2fa-noown-{Guid.NewGuid():N}@example.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Plain", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // /api/account/me e' in whitelist, ma anche un endpoint NON-whitelist deve passare.
        var r1 = await client.GetAsync("/api/care-circles");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r1.Headers.Contains("X-2FA-Required-By").Should().BeFalse();
    }

    [Fact]
    public async Task Owner_with_null_deadline_gets_lazy_backfill()
    {
        var (client, userId, circleId) = await RegisterOwnerAsync();
        // Simulo un Owner pre-rollout: deadline = null.
        SetDeadline(userId, null);

        var resp = await client.GetAsync($"/api/care-circles/{circleId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.Contains("X-2FA-Required-By").Should().BeTrue();

        // Verifica che la deadline sia stata persistita.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
        var user = db.Users.Single(u => u.Id == userId);
        user.TwoFactorRequiredFromUtc.Should().NotBeNull();
        user.TwoFactorRequiredFromUtc!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
