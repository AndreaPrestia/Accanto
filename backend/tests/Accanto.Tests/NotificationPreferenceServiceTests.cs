using Accanto.Application.Notifications;
using Accanto.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Accanto.Tests;

public class NotificationPreferenceServiceTests
{
    [Fact]
    public async Task Get_returns_all_topics_default_enabled_when_no_rows()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        var prefs = await svc.GetAsync(userId);

        prefs.Should().HaveCount(Enum.GetValues<NotificationTopic>().Length);
        prefs.Should().OnlyContain(p => p.EmailEnabled);
    }

    [Fact]
    public async Task Update_persists_and_returns_current_state()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        var updated = await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.TimelineEntryCreated, false),
            new NotificationPreferenceDto(NotificationTopic.SharedUpdateCreated, false)
        }));

        updated.Should().Contain(p => p.Topic == NotificationTopic.TimelineEntryCreated && !p.EmailEnabled);
        updated.Should().Contain(p => p.Topic == NotificationTopic.SharedUpdateCreated && !p.EmailEnabled);
        updated.Should().Contain(p => p.Topic == NotificationTopic.DoctorQuestionAnswered && p.EmailEnabled);

        // round-trip
        var fetched = await svc.GetAsync(userId);
        fetched.Single(p => p.Topic == NotificationTopic.TimelineEntryCreated).EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Update_is_idempotent_and_only_changes_supplied_topics()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.InviteAccepted, false)
        }));
        await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.TimelineEntryCreated, false)
        }));

        var fetched = await svc.GetAsync(userId);
        fetched.Single(p => p.Topic == NotificationTopic.InviteAccepted).EmailEnabled.Should().BeFalse();
        fetched.Single(p => p.Topic == NotificationTopic.TimelineEntryCreated).EmailEnabled.Should().BeFalse();
        fetched.Single(p => p.Topic == NotificationTopic.SharedUpdateCreated).EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Get_returns_PushEnabled_default_true_when_no_rows()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        var prefs = await svc.GetAsync(userId);

        prefs.Should().OnlyContain(p => p.PushEnabled == true);
    }

    [Fact]
    public async Task Update_with_null_PushEnabled_preserves_existing_value()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.TimelineEntryCreated, EmailEnabled: true, PushEnabled: false)
        }));

        await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.TimelineEntryCreated, EmailEnabled: false, PushEnabled: null)
        }));

        var fetched = await svc.GetAsync(userId);
        var p = fetched.Single(x => x.Topic == NotificationTopic.TimelineEntryCreated);
        p.EmailEnabled.Should().BeFalse();
        p.PushEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Update_with_PushEnabled_only_changes_push_flag()
    {
        var db = TestDb.Create();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdateAsync(userId, new UpdateNotificationPreferencesRequest(new[]
        {
            new NotificationPreferenceDto(NotificationTopic.SharedUpdateCreated, EmailEnabled: true, PushEnabled: false)
        }));

        var fetched = await svc.GetAsync(userId);
        var p = fetched.Single(x => x.Topic == NotificationTopic.SharedUpdateCreated);
        p.EmailEnabled.Should().BeTrue();
        p.PushEnabled.Should().BeFalse();
    }
}
