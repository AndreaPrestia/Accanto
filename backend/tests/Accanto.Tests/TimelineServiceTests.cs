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
        var svc = new TimelineService(db, auth, cv, uv);
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
}
