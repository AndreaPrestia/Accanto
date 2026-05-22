using Accanto.Application.Push;
using Accanto.Infrastructure.Push;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class PushServiceTests
{
    private static PushService BuildService(out IServiceScopeFactory sf, out Accanto.Infrastructure.Persistence.AccantoDbContext db, PushOptions? options = null)
    {
        db = TestDb.Create();
        var services = new ServiceCollection();
        services.AddSingleton<Accanto.Application.Common.Persistence.IAccantoDbContext>(db);
        sf = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var opt = Options.Create(options ?? new PushOptions());
        return new PushService(sf, opt, NullLogger<PushService>.Instance);
    }

    [Fact]
    public async Task Subscribe_then_unsubscribe_stores_and_removes()
    {
        var svc = BuildService(out _, out var db);
        var userId = Guid.NewGuid();
        await svc.SubscribeAsync(userId, new PushSubscriptionRequest("https://push.example/ep1", "p256dh-value", "auth-value", "ua/1.0"));

        db.PushSubscriptions.Should().HaveCount(1);
        db.PushSubscriptions.Single().UserId.Should().Be(userId);

        await svc.SubscribeAsync(userId, new PushSubscriptionRequest("https://push.example/ep1", "p256dh-new", "auth-new", "ua/1.0"));
        db.PushSubscriptions.Should().HaveCount(1);
        db.PushSubscriptions.Single().P256dh.Should().Be("p256dh-new");

        await svc.UnsubscribeAsync(userId, "https://push.example/ep1");
        db.PushSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public void VapidPublicKey_returns_null_when_not_configured()
    {
        var svc = BuildService(out _, out _);
        svc.GetVapidPublicKey().Should().BeNull();
    }

    [Fact]
    public void VapidPublicKey_returns_value_when_configured()
    {
        var svc = BuildService(out _, out _, new PushOptions { VapidPublicKey = "PUBKEY", VapidPrivateKey = "PRIVKEY" });
        svc.GetVapidPublicKey().Should().Be("PUBKEY");
    }

    [Fact]
    public async Task NotifyUsers_is_noop_without_vapid_keys()
    {
        var svc = BuildService(out _, out var db);
        await svc.NotifyUsersAsync(new[] { Guid.NewGuid() }, new PushNotificationPayload("t", "b", null));
        db.PushSubscriptions.Should().BeEmpty();
    }
}
