using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Ai;
using Accanto.Application.Auth;
using Accanto.Application.CareCircles;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Tests;

public class AiHistoryEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ConfiguredFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ai:Provider", "ollama");
            builder.UseSetting("Ai:Model", "test-model");
            builder.ConfigureServices(services =>
            {
                var existing = services.Where(d => d.ServiceType == typeof(IAiAssistant)).ToList();
                foreach (var d in existing) services.Remove(d);
                services.AddSingleton<IAiAssistant, NullAiAssistant>();
            });
        }
    }

    [Fact]
    public async Task Calling_ai_persists_an_interaction_visible_in_history()
    {
        await using var factory = new ConfiguredFactory();
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("hist@example.com", "T", "passwordSegreta1"));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var call = await client.PostAsJsonAsync("/api/me/ai/checkin-reflection", new CheckInReflectionRequest(14));
        call.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await call.Content.ReadFromJsonAsync<AiResponse>(JsonOpts);
        body!.InteractionId.Should().NotBe(Guid.Empty);
        call.Headers.Should().Contain(h => h.Key == "X-AI-Cache");

        // Lista cronologia
        var list = await client.GetFromJsonAsync<AiInteractionListResponse>("/api/ai/interactions", JsonOpts);
        list!.Total.Should().BeGreaterThan(0);
        list.Items.Should().Contain(x => x.Id == body.InteractionId);

        // Dettaglio
        var detail = await client.GetFromJsonAsync<AiInteractionDetail>($"/api/ai/interactions/{body.InteractionId}", JsonOpts);
        detail!.Id.Should().Be(body.InteractionId);
        detail.Output.Should().NotBeNullOrEmpty();

        // Feedback
        var fb = await client.PostAsJsonAsync($"/api/ai/interactions/{body.InteractionId}/feedback",
            new SubmitAiFeedbackRequest("Up"));
        fb.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail2 = await client.GetFromJsonAsync<AiInteractionDetail>($"/api/ai/interactions/{body.InteractionId}", JsonOpts);
        detail2!.Feedback.Should().Be("Up");
    }

    [Fact]
    public async Task Other_user_cannot_read_my_interaction()
    {
        await using var factory = new ConfiguredFactory();
        var alice = factory.CreateClient();
        var aliceReg = await alice.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("alice@example.com", "Alice", "passwordSegreta1"));
        var aliceAuth = await aliceReg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        alice.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", aliceAuth!.AccessToken);

        var call = await alice.PostAsJsonAsync("/api/me/ai/checkin-reflection", new CheckInReflectionRequest(14));
        var body = await call.Content.ReadFromJsonAsync<AiResponse>(JsonOpts);

        var bob = factory.CreateClient();
        var bobReg = await bob.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("bob@example.com", "Bob", "passwordSegreta1"));
        var bobAuth = await bobReg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        bob.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bobAuth!.AccessToken);

        var resp = await bob.GetAsync($"/api/ai/interactions/{body!.InteractionId}");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
