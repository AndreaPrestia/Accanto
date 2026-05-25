using Accanto.Application.Audit;
using Accanto.Application.CareCircles;
using Accanto.Application.Common.Exceptions;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using FluentAssertions;

namespace Accanto.Tests;

public class AuditServiceTests
{
    private static (AuditService svc, Accanto.Infrastructure.Persistence.AccantoDbContext db) Build()
    {
        var db = TestDb.Create();
        var auth = new CareCircleAuthorization(db);
        return (new AuditService(db, auth), db);
    }

    private static async Task<Guid> SeedCircleWithMember(
        Accanto.Infrastructure.Persistence.AccantoDbContext db,
        Guid memberId,
        CareCircleRole role)
    {
        var circle = new CareCircle
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = CareCircleStatus.Active,
            CreatedByUserId = memberId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.CareCircles.Add(circle);
        db.CareCircleMembers.Add(new CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UserId = memberId,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return circle.Id;
    }

    [Fact]
    public async Task List_requires_membership()
    {
        var (svc, db) = Build();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var circleId = await SeedCircleWithMember(db, owner, CareCircleRole.Owner);

        Func<Task> act = () => svc.ListAsync(stranger, circleId, 0, 50);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Viewer_can_read_log()
    {
        var (svc, db) = Build();
        var viewer = Guid.NewGuid();
        var circleId = await SeedCircleWithMember(db, viewer, CareCircleRole.Viewer);

        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = circleId,
            PerformedByUserId = viewer,
            ActionType = AuditActionType.CircleCreated,
            ResourceType = AuditResourceType.CareCircle,
            ResourceId = circleId,
            Summary = "Test",
            Timestamp = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var page = await svc.ListAsync(viewer, circleId, 0, 50);
        page.Total.Should().Be(1);
        page.Items.Should().HaveCount(1);
        page.Items[0].ActionType.Should().Be(AuditActionType.CircleCreated);
    }

    [Fact]
    public async Task Returns_only_entries_of_the_requested_circle()
    {
        var (svc, db) = Build();
        var member = Guid.NewGuid();
        var circleA = await SeedCircleWithMember(db, member, CareCircleRole.Owner);
        var circleB = await SeedCircleWithMember(db, member, CareCircleRole.Owner);

        db.AuditLogEntries.AddRange(
            new AuditLogEntry { Id = Guid.NewGuid(), CareCircleId = circleA, PerformedByUserId = member, ActionType = AuditActionType.EntryCreated, ResourceType = AuditResourceType.TimelineEntry, Timestamp = DateTimeOffset.UtcNow },
            new AuditLogEntry { Id = Guid.NewGuid(), CareCircleId = circleB, PerformedByUserId = member, ActionType = AuditActionType.EntryCreated, ResourceType = AuditResourceType.TimelineEntry, Timestamp = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        var pageA = await svc.ListAsync(member, circleA, 0, 50);
        pageA.Total.Should().Be(1);
        pageA.Items[0].CareCircleId.Should().Be(circleA);
    }

    [Fact]
    public async Task Pages_results_newest_first()
    {
        var (svc, db) = Build();
        var member = Guid.NewGuid();
        var circleId = await SeedCircleWithMember(db, member, CareCircleRole.Owner);
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (int i = 0; i < 5; i++)
        {
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                CareCircleId = circleId,
                PerformedByUserId = member,
                ActionType = AuditActionType.EntryCreated,
                ResourceType = AuditResourceType.TimelineEntry,
                Summary = $"e{i}",
                Timestamp = t0.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var page = await svc.ListAsync(member, circleId, 0, 2);
        page.Total.Should().Be(5);
        page.Items.Should().HaveCount(2);
        page.Items[0].Summary.Should().Be("e4");
        page.Items[1].Summary.Should().Be("e3");

        var page2 = await svc.ListAsync(member, circleId, 2, 2);
        page2.Items[0].Summary.Should().Be("e2");
        page2.Items[1].Summary.Should().Be("e1");
    }
}
