using Accanto.Application.Timeline;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using FluentAssertions;
using FluentValidation;

namespace Accanto.Tests;

public class TimelineServiceTests
{
    private static (TimelineService svc, Accanto.Infrastructure.Persistence.AccantoDbContext db, Guid circleId, Guid alice, Guid bob) Setup()
    {
        var db = TestDb.Create();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var circleId = Guid.NewGuid();

        db.CareCircles.Add(new CareCircle
        {
            Id = circleId, Name = "C", Status = CareCircleStatus.Active,
            CreatedByUserId = alice, CreatedAt = DateTimeOffset.UtcNow
        });
        db.CareCircleMembers.AddRange(
            new CareCircleMember { Id = Guid.NewGuid(), CareCircleId = circleId, UserId = alice, Role = CareCircleRole.Owner, CreatedAt = DateTimeOffset.UtcNow },
            new CareCircleMember { Id = Guid.NewGuid(), CareCircleId = circleId, UserId = bob, Role = CareCircleRole.Caregiver, CreatedAt = DateTimeOffset.UtcNow }
        );
        db.SaveChanges();

        var auth = new CareCircleAuthorization(db);
        IValidator<CreateTimelineEntryRequest> cv = new CreateTimelineEntryRequestValidator();
        IValidator<UpdateTimelineEntryRequest> uv = new UpdateTimelineEntryRequestValidator();
        var svc = new TimelineService(db, auth, new NoOpPushService(), new NoOpCircleEmailNotifier(), new NoOpCircleMobilePushNotifier(), new NoOpAuditLog(), cv, uv, new BulkUpdateTimelineEntriesRequestValidator());
        return (svc, db, circleId, alice, bob);
    }

    [Fact]
    public async Task Private_entries_are_hidden_from_other_members()
    {
        var (svc, _, circleId, alice, bob) = Setup();

        await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.PersonalNote, "segreta", "x", new List<string>(),
            TimelineVisibility.Private));
        await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.MedicalUpdate, "pubblica", "y", new List<string>(),
            TimelineVisibility.Circle));

        var bobsList = await svc.ListAsync(bob, circleId, new TimelineQuery());
        bobsList.Should().HaveCount(1);
        bobsList[0].Title.Should().Be("pubblica");

        var alicesList = await svc.ListAsync(alice, circleId, new TimelineQuery());
        alicesList.Should().HaveCount(2);
    }

    [Fact]
    public async Task Date_filters_From_To_constrain_results_by_OccurredAt()
    {
        var (svc, _, circleId, alice, _) = Setup();

        var jan = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

        await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(jan, TimelineEntryType.MedicalUpdate, "gen", "x", new(), TimelineVisibility.Circle));
        await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(feb, TimelineEntryType.MedicalUpdate, "feb", "x", new(), TimelineVisibility.Circle));
        await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(mar, TimelineEntryType.MedicalUpdate, "mar", "x", new(), TimelineVisibility.Circle));

        // From only
        var fromFeb = await svc.ListAsync(alice, circleId, new TimelineQuery(From: feb));
        fromFeb.Select(e => e.Title).Should().BeEquivalentTo(new[] { "mar", "feb" });

        // To only (inclusive on the end)
        var untilFeb = await svc.ListAsync(alice, circleId, new TimelineQuery(To: feb));
        untilFeb.Select(e => e.Title).Should().BeEquivalentTo(new[] { "feb", "gen" });

        // Both
        var only = await svc.ListAsync(alice, circleId, new TimelineQuery(
            From: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero)));
        only.Should().ContainSingle(e => e.Title == "feb");
    }

    [Fact]
    public async Task BulkUpdate_adds_and_removes_tags_and_changes_visibility()
    {
        var (svc, _, circleId, alice, _) = Setup();

        var e1 = await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.MedicalUpdate, "a", "x", new List<string> { "vecchio" }, TimelineVisibility.Circle));
        var e2 = await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.MedicalUpdate, "b", "y", new List<string>(), TimelineVisibility.Circle));

        var result = await svc.BulkUpdateAsync(alice, circleId, new BulkUpdateTimelineEntriesRequest(
            new[] { e1.Id, e2.Id },
            TagsToAdd: new[] { "urgente" },
            TagsToRemove: new[] { "vecchio" },
            NewVisibility: TimelineVisibility.Private));

        result.Updated.Should().Be(2);
        result.Skipped.Should().Be(0);

        var fetched1 = await svc.GetAsync(alice, circleId, e1.Id);
        fetched1.Tags.Should().BeEquivalentTo(new[] { "urgente" });
        fetched1.Visibility.Should().Be(TimelineVisibility.Private);

        var fetched2 = await svc.GetAsync(alice, circleId, e2.Id);
        fetched2.Tags.Should().BeEquivalentTo(new[] { "urgente" });
    }

    [Fact]
    public async Task BulkUpdate_skips_private_entries_of_other_users()
    {
        var (svc, _, circleId, alice, bob) = Setup();

        var alicePrivate = await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.PersonalNote, "segreta", "x", new List<string>(), TimelineVisibility.Private));
        var publicEntry = await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.MedicalUpdate, "pubblica", "x", new List<string>(), TimelineVisibility.Circle));

        var result = await svc.BulkUpdateAsync(bob, circleId, new BulkUpdateTimelineEntriesRequest(
            new[] { alicePrivate.Id, publicEntry.Id },
            TagsToAdd: new[] { "tag" },
            TagsToRemove: null,
            NewVisibility: null));

        result.Updated.Should().Be(1);
        result.Skipped.Should().Be(1);

        // Voce privata di Alice resta invariata.
        var aliceView = await svc.GetAsync(alice, circleId, alicePrivate.Id);
        aliceView.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkUpdate_requires_at_least_one_operation()
    {
        var (svc, _, circleId, alice, _) = Setup();
        var entry = await svc.CreateAsync(alice, circleId, new CreateTimelineEntryRequest(
            DateTimeOffset.UtcNow, TimelineEntryType.MedicalUpdate, "a", "x", new List<string>(), TimelineVisibility.Circle));

        var act = async () => await svc.BulkUpdateAsync(alice, circleId, new BulkUpdateTimelineEntriesRequest(
            new[] { entry.Id }, null, null, null));

        await act.Should().ThrowAsync<Accanto.Application.Common.Exceptions.AppValidationException>();
    }
}
