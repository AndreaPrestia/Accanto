using System.Net;
using System.Net.Http.Json;
using Accanto.Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Accanto.Tests;

public class LockoutTests
{
    /// <summary>Soglie volutamente basse per esercitare il lockout in pochi tentativi.</summary>
    private sealed class LowLockoutFactory : AccantoFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Lockout:MaxFailedAttempts", "3");
            builder.UseSetting("Lockout:LockoutMinutes", "5");
            builder.UseSetting("Lockout:AttemptWindowMinutes", "10");
        }
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "U", "passwordSegreta1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_blocks_after_max_failed_attempts()
    {
        await using var factory = new LowLockoutFactory();
        var client = factory.CreateClient();
        const string email = "lock1@example.com";
        await RegisterAsync(client, email);

        // 3 tentativi falliti → al terzo l'account è bloccato.
        for (int i = 0; i < 3; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, "wrong-password"));
            bad.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Anche con la password GIUSTA, ora siamo bloccati.
        var stillBlocked = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "passwordSegreta1"));
        stillBlocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await stillBlocked.Content.ReadAsStringAsync();
        body.Should().Contain("blocc"); // "bloccato"
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempts()
    {
        await using var factory = new LowLockoutFactory();
        var client = factory.CreateClient();
        const string email = "lock2@example.com";
        await RegisterAsync(client, email);

        // 2 tentativi falliti (sotto la soglia di 3).
        for (int i = 0; i < 2; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, "wrong-password"));
            bad.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Login corretto → reset.
        var ok = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "passwordSegreta1"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2 nuovi tentativi falliti devono ancora essere ammessi senza lockout.
        for (int i = 0; i < 2; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, "wrong-password"));
            bad.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var good = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "passwordSegreta1"));
        good.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
