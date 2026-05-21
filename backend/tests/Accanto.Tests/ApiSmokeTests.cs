using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Auth;
using Accanto.Application.CareCircles;
using FluentAssertions;

namespace Accanto.Tests;

public class ApiSmokeTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;

    public ApiSmokeTests(AccantoFactory factory) { _factory = factory; }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_login_create_circle_flow()
    {
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("anna@example.com", "Anna", "passwordSegreta1"));
        var body = await register.Content.ReadAsStringAsync();
        register.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var create = await client.PostAsJsonAsync("/api/care-circles",
            new CreateCareCircleRequest("Mamma", "Test"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var mine = await client.GetFromJsonAsync<List<CareCircleDto>>("/api/care-circles", JsonOpts);
        mine.Should().NotBeNull();
        mine!.Should().HaveCount(1);
        mine[0].Name.Should().Be("Mamma");
    }
}
