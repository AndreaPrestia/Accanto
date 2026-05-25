using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Auth;
using Accanto.Application.Wellbeing;
using FluentAssertions;

namespace Accanto.Tests;

public class CheckInTests : IClassFixture<AccantoFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AccantoFactory _factory;
    public CheckInTests(AccantoFactory factory) { _factory = factory; }

    private async Task<HttpClient> AuthAsync(string email)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "U", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Create_and_list_check_in()
    {
        var client = await AuthAsync($"checkin-{Guid.NewGuid():N}@example.com");

        var create = await client.PostAsJsonAsync("/api/account/check-ins",
            new CreateCheckInRequest(3, 2, 4, "Giornata difficile"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CaregiverCheckInDto>(JsonOpts);
        created!.Mood.Should().Be(3);
        created.Energy.Should().Be(2);
        created.Stress.Should().Be(4);
        created.Note.Should().Be("Giornata difficile");

        var list = await client.GetFromJsonAsync<List<CaregiverCheckInDto>>("/api/account/check-ins", JsonOpts);
        list.Should().NotBeNull();
        list!.Should().ContainSingle(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Validation_rejects_out_of_range_values()
    {
        var client = await AuthAsync($"checkin-{Guid.NewGuid():N}@example.com");
        var resp = await client.PostAsJsonAsync("/api/account/check-ins",
            new CreateCheckInRequest(0, 6, 3, null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Other_user_cannot_see_or_delete_check_in()
    {
        var alice = await AuthAsync($"alice-{Guid.NewGuid():N}@example.com");
        var bob = await AuthAsync($"bob-{Guid.NewGuid():N}@example.com");

        var create = await alice.PostAsJsonAsync("/api/account/check-ins",
            new CreateCheckInRequest(4, 4, 2, null));
        var created = await create.Content.ReadFromJsonAsync<CaregiverCheckInDto>(JsonOpts);

        var bobList = await bob.GetFromJsonAsync<List<CaregiverCheckInDto>>("/api/account/check-ins", JsonOpts);
        bobList!.Should().NotContain(c => c.Id == created!.Id);

        var bobDelete = await bob.DeleteAsync($"/api/account/check-ins/{created!.Id}");
        bobDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var aliceDelete = await alice.DeleteAsync($"/api/account/check-ins/{created.Id}");
        aliceDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
