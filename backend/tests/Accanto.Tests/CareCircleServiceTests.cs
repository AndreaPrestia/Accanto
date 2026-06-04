using Accanto.Application.CareCircles;
using Accanto.Application.Common.Exceptions;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using FluentAssertions;
using FluentValidation;

namespace Accanto.Tests;

public class CareCircleServiceTests
{
    private static CareCircleService Build(out Accanto.Infrastructure.Persistence.AccantoDbContext db)
    {
        db = TestDb.Create();
        var auth = new CareCircleAuthorization(db);
        IValidator<CreateCareCircleRequest> createV = new CreateCareCircleRequestValidator();
        IValidator<UpdateCareCircleRequest> updateV = new UpdateCareCircleRequestValidator();
        return new CareCircleService(db, auth, new NoOpAuditLog(), new NoOpOwnerTwoFactorOnboarding(), createV, updateV);
    }

    [Fact]
    public async Task Create_makes_creator_an_Owner_member()
    {
        var svc = Build(out var db);
        var userId = Guid.NewGuid();

        var dto = await svc.CreateAsync(userId, new CreateCareCircleRequest("Mamma", "Note"));

        dto.MyRole.Should().Be(CareCircleRole.Owner);
        dto.Status.Should().Be(CareCircleStatus.Active);

        var members = db.CareCircleMembers.Where(m => m.CareCircleId == dto.Id).ToList();
        members.Should().HaveCount(1);
        members[0].UserId.Should().Be(userId);
        members[0].Role.Should().Be(CareCircleRole.Owner);
    }

    [Fact]
    public async Task GetById_throws_Forbidden_for_non_member()
    {
        var svc = Build(out _);
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var dto = await svc.CreateAsync(owner, new CreateCareCircleRequest("Mamma", null));

        var act = async () => await svc.GetByIdAsync(stranger, dto.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Archive_requires_Owner_role()
    {
        var svc = Build(out var db);
        var owner = Guid.NewGuid();
        var caregiver = Guid.NewGuid();
        var dto = await svc.CreateAsync(owner, new CreateCareCircleRequest("Papà", null));

        db.CareCircleMembers.Add(new Accanto.Domain.Entities.CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = dto.Id,
            UserId = caregiver,
            Role = CareCircleRole.Caregiver,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var act = async () => await svc.ArchiveAsync(caregiver, dto.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
