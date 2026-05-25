using System.Net;
using System.Net.Http.Json;
using Accanto.Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Accanto.Tests;

public class RateLimitTests
{
    private sealed class LowLimitFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Soglie volutamente basse per esercitare il 429 nei test.
            builder.UseSetting("RateLimit:Login:PermitLimit", "2");
            builder.UseSetting("RateLimit:Login:Window", "00:01:00");
            builder.UseSetting("RateLimit:Register:PermitLimit", "2");
            builder.UseSetting("RateLimit:Register:Window", "00:01:00");
            base.ConfigureWebHost(builder);
        }
    }

    [Fact]
    public async Task Login_returns_429_after_permit_limit_is_exceeded()
    {
        await using var factory = new LowLimitFactory();
        var client = factory.CreateClient();

        // Le credenziali sono volutamente sbagliate: ci interessa solo che il limiter conti i tentativi.
        var body = new LoginRequest("ghost@example.com", "wrong-password");

        var first = await client.PostAsJsonAsync("/api/auth/login", body);
        var second = await client.PostAsJsonAsync("/api/auth/login", body);
        var third = await client.PostAsJsonAsync("/api/auth/login", body);

        first.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Register_returns_429_after_permit_limit_is_exceeded()
    {
        await using var factory = new LowLimitFactory();
        var client = factory.CreateClient();

        var ok1 = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("first@example.com", "First", "passwordSegreta1"));
        var ok2 = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("second@example.com", "Second", "passwordSegreta1"));
        var blocked = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("third@example.com", "Third", "passwordSegreta1"));

        ok1.StatusCode.Should().Be(HttpStatusCode.OK);
        ok2.StatusCode.Should().Be(HttpStatusCode.OK);
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
