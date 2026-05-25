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
        var svc = new TimelineService(db, auth, new NoOpPushService(), new NoOpAuditLog(), cv, uv);
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
}
