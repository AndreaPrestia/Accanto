using Accanto.Application.Push;
using FluentAssertions;
using Xunit;

namespace Accanto.Tests;

public class DevicePushTokenServiceTests
{
    [Fact]
    public async Task RegisterAsync_creates_new_token_with_normalized_platform()
    {
        var db = TestDb.Create();
        var svc = new DevicePushTokenService(db);
        var userId = Guid.NewGuid();

        var dto = await svc.RegisterAsync(userId, new RegisterDevicePushTokenRequest(
            Token: "ExponentPushToken[abc]",
            Platform: "  IOS  ",
            DeviceName: " iPhone di Andrea "));

        dto.Token.Should().Be("ExponentPushToken[abc]");
        dto.Platform.Should().Be("ios");
        dto.DeviceName.Should().Be("iPhone di Andrea");

        var rows = await svc.ListAsync(userId);
        rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task RegisterAsync_upserts_existing_token_and_reassigns_to_new_user()
    {
        // Caso reale: device riusato con secondo account dopo logout.
        var db = TestDb.Create();
        var svc = new DevicePushTokenService(db);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        const string token = "ExponentPushToken[shared]";

        await svc.RegisterAsync(firstUser, new RegisterDevicePushTokenRequest(token, "android", null));
        await svc.RegisterAsync(secondUser, new RegisterDevicePushTokenRequest(token, "android", "Pixel"));

        (await svc.ListAsync(firstUser)).Should().BeEmpty();
        var second = await svc.ListAsync(secondUser);
        second.Should().HaveCount(1);
        second[0].DeviceName.Should().Be("Pixel");
    }

    [Fact]
    public async Task RemoveByIdAsync_only_affects_owning_user()
    {
        var db = TestDb.Create();
        var svc = new DevicePushTokenService(db);
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();

        var ownerDto = await svc.RegisterAsync(owner, new RegisterDevicePushTokenRequest("tok-A", "ios", null));
        await svc.RegisterAsync(other, new RegisterDevicePushTokenRequest("tok-B", "ios", null));

        var removedByOther = await svc.RemoveByIdAsync(other, ownerDto.Id);
        removedByOther.Should().BeFalse(); // un altro utente non può cancellare il mio token

        var removedByOwner = await svc.RemoveByIdAsync(owner, ownerDto.Id);
        removedByOwner.Should().BeTrue();
        (await svc.ListAsync(owner)).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveByTokenAsync_works_for_logout_flow()
    {
        var db = TestDb.Create();
        var svc = new DevicePushTokenService(db);
        var userId = Guid.NewGuid();

        await svc.RegisterAsync(userId, new RegisterDevicePushTokenRequest("tok-logout", "ios", null));

        var ok = await svc.RemoveByTokenAsync(userId, "tok-logout");
        ok.Should().BeTrue();
        (await svc.ListAsync(userId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveInvalidTokensAsync_drops_all_matching_rows()
    {
        // Pattern usato dal CircleMobilePushNotifier dopo che Expo
        // segnala DeviceNotRegistered su uno o più token.
        var db = TestDb.Create();
        var svc = new DevicePushTokenService(db);
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        await svc.RegisterAsync(u1, new RegisterDevicePushTokenRequest("dead-1", "ios", null));
        await svc.RegisterAsync(u2, new RegisterDevicePushTokenRequest("dead-2", "android", null));
        await svc.RegisterAsync(u1, new RegisterDevicePushTokenRequest("alive", "ios", null));

        await svc.RemoveInvalidTokensAsync(new[] { "dead-1", "dead-2", "never-existed" });

        (await svc.ListAsync(u1)).Select(t => t.Token).Should().BeEquivalentTo(new[] { "alive" });
        (await svc.ListAsync(u2)).Should().BeEmpty();
    }
}
