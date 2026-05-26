using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Ai;
using Accanto.Application.Auth;
using Accanto.Application.CareCircles;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Accanto.Tests;

public class AiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class NotConfiguredFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ai:Provider", "none");
        }
    }

    private sealed class ConfiguredFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Provider configurato + Null assistant registrato di default → endpoint funzionano.
            builder.UseSetting("Ai:Provider", "ollama");
            builder.UseSetting("Ai:Model", "test-model");
        }
    }

    private static async Task<(HttpClient client, Guid circleId)> SetupAuthedWithCircleAsync(AccantoFactory factory, string email)
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Tester", "passwordSegreta1"));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var create = await client.PostAsJsonAsync("/api/care-circles",
            new CreateCareCircleRequest("Famiglia", null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await create.Content.ReadFromJsonAsync<CareCircleDto>(JsonOpts);
        return (client, dto!.Id);
    }

    [Fact]
    public async Task Status_returns_unavailable_when_provider_is_none()
    {
        await using var factory = new NotConfiguredFactory();
        var client = factory.CreateClient();
        // Endpoint richiede auth.
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("nostatus@example.com", "T", "passwordSegreta1"));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var status = await client.GetFromJsonAsync<AiStatusResponse>("/api/ai/status", JsonOpts);
        status!.Available.Should().BeFalse();
        status.Provider.Should().Be("none");
    }

    [Fact]
    public async Task TimelineSummary_returns_503_when_provider_not_configured()
    {
        await using var factory = new NotConfiguredFactory();
        var (client, circleId) = await SetupAuthedWithCircleAsync(factory, "u503@example.com");

        // Anche se AI non è abilitato sul cerchio, il gate di sistema (503) viene controllato per primo.
        var resp = await client.PostAsJsonAsync(
            $"/api/care-circles/{circleId}/ai/timeline-summary",
            new TimelineSummaryRequest(7));
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task TimelineSummary_returns_403_when_ai_disabled_on_circle()
    {
        await using var factory = new ConfiguredFactory();
        var (client, circleId) = await SetupAuthedWithCircleAsync(factory, "u403@example.com");

        // AiEnabled è false di default → 403 ai_disabled_for_circle.
        var resp = await client.PostAsJsonAsync(
            $"/api/care-circles/{circleId}/ai/timeline-summary",
            new TimelineSummaryRequest(7));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enable_then_TimelineSummary_returns_200_with_placeholder()
    {
        await using var factory = new ConfiguredFactory();
        var (client, circleId) = await SetupAuthedWithCircleAsync(factory, "uhappy@example.com");

        var enable = await client.PutAsJsonAsync(
            $"/api/care-circles/{circleId}/ai/settings",
            new AiController_SetAiSettingsRequest(true));
        enable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resp = await client.PostAsJsonAsync(
            $"/api/care-circles/{circleId}/ai/timeline-summary",
            new TimelineSummaryRequest(7));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AiResponse>(JsonOpts);
        body!.Disclaimer.Should().NotBeNullOrWhiteSpace();
        body.Model.Should().Be("null");
    }

    [Fact]
    public async Task CheckInReflection_works_without_circle_when_configured()
    {
        await using var factory = new ConfiguredFactory();
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("ucheckin@example.com", "T", "passwordSegreta1"));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var resp = await client.PostAsJsonAsync("/api/me/ai/checkin-reflection",
            new CheckInReflectionRequest(14));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AiResponse>(JsonOpts);
        body!.Text.Should().NotBeNullOrWhiteSpace();
    }
}

// Mirror del DTO nidificato nel controller, per evitare di referenziare il namespace Api dai test.
public sealed record AiController_SetAiSettingsRequest(bool Enabled);
