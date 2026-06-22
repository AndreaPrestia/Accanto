using Accanto.Application.CareCircles;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Invites;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using FluentAssertions;
using FluentValidation;

namespace Accanto.Tests;

public class InviteServiceTests
{
    private static (InviteService invites, CareCircleService circles, Accanto.Infrastructure.Persistence.AccantoDbContext db) Build()
    {
        var db = TestDb.Create();
        var auth = new CareCircleAuthorization(db);
        var circles = new CareCircleService(
            db, auth,
            new NoOpAuditLog(),
            new NoOpOwnerTwoFactorOnboarding(),
            new CreateCareCircleRequestValidator(),
            new UpdateCareCircleRequestValidator());
        IValidator<CreateInviteRequest> createV = new CreateInviteRequestValidator();
        var invites = new InviteService(db, auth, new NoOpAuditLog(), new NoOpCircleEmailNotifier(), new NoOpCircleMobilePushNotifier(), new NoOpOwnerTwoFactorOnboarding(), createV);
        return (invites, circles, db);
    }

    [Fact]
    public async Task Create_requires_Owner()
    {
        var (invites, circles, db) = Build();
        var owner = Guid.NewGuid();
        var caregiver = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Mamma", null));

        db.CareCircleMembers.Add(new CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UserId = caregiver,
            Role = CareCircleRole.Caregiver,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var act = async () => await invites.CreateAsync(caregiver, circle.Id, new CreateInviteRequest(CareCircleRole.Viewer, null, null));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Create_rejects_Owner_as_target_role()
    {
        var (invites, circles, _) = Build();
        var owner = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Papà", null));

        var act = async () => await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Owner, null, null));
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Create_generates_unique_url_safe_token_and_active_status()
    {
        var (invites, circles, _) = Build();
        var owner = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Zia", null));

        var a = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Caregiver, 7, 1));
        var b = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Caregiver, 7, 1));

        a.Token.Should().NotBe(b.Token);
        a.Token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        a.IsActive.Should().BeTrue();
        a.MaxUses.Should().Be(1);
        a.UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task Accept_adds_membership_and_consumes_use()
    {
        var (invites, circles, db) = Build();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Mamma", null));
        var invite = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Caregiver, 7, 1));

        var returnedCircleId = await invites.AcceptAsync(invitee, invite.Token);

        returnedCircleId.Should().Be(circle.Id);
        var member = db.CareCircleMembers.SingleOrDefault(m => m.CareCircleId == circle.Id && m.UserId == invitee);
        member.Should().NotBeNull();
        member!.Role.Should().Be(CareCircleRole.Caregiver);

        var refreshed = db.CareCircleInvites.Single(i => i.Id == invite.Id);
        refreshed.UsedCount.Should().Be(1);
    }

    [Fact]
    public async Task Accept_is_noop_when_already_member()
    {
        var (invites, circles, db) = Build();
        var owner = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Papà", null));
        var invite = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Caregiver, 7, 1));

        // Owner accetta il proprio invito: è già membro, non deve consumare il link né cambiare ruolo.
        var returnedCircleId = await invites.AcceptAsync(owner, invite.Token);

        returnedCircleId.Should().Be(circle.Id);
        var members = db.CareCircleMembers.Where(m => m.CareCircleId == circle.Id).ToList();
        members.Should().HaveCount(1);
        members[0].Role.Should().Be(CareCircleRole.Owner);

        var refreshed = db.CareCircleInvites.Single(i => i.Id == invite.Id);
        refreshed.UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task Accept_fails_when_revoked()
    {
        var (invites, circles, _) = Build();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Mamma", null));
        var invite = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Viewer, 7, 5));

        await invites.RevokeAsync(owner, circle.Id, invite.Id);

        var act = async () => await invites.AcceptAsync(invitee, invite.Token);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Accept_fails_when_max_uses_reached()
    {
        var (invites, circles, db) = Build();
        var owner = Guid.NewGuid();
        var firstInvitee = Guid.NewGuid();
        var secondInvitee = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Nonno", null));
        var invite = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Viewer, 7, 1));

        await invites.AcceptAsync(firstInvitee, invite.Token);

        var act = async () => await invites.AcceptAsync(secondInvitee, invite.Token);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Accept_fails_when_expired()
    {
        var (invites, circles, db) = Build();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Nonna", null));
        var invite = await invites.CreateAsync(owner, circle.Id, new CreateInviteRequest(CareCircleRole.Viewer, 1, 1));

        // Forzo la scadenza nel passato direttamente sul record.
        var row = db.CareCircleInvites.Single(i => i.Id == invite.Id);
        row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var act = async () => await invites.AcceptAsync(invitee, invite.Token);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Preview_returns_circle_name_and_creator()
    {
        var (invites, circles, db) = Build();
        var ownerId = Guid.NewGuid();
        db.Users.Add(new Accanto.Domain.Entities.User
        {
            Id = ownerId,
            Email = "anna@example.org",
            DisplayName = "Anna",
            PasswordHash = "x",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var circle = await circles.CreateAsync(ownerId, new CreateCareCircleRequest("Mamma", null));
        var invite = await invites.CreateAsync(ownerId, circle.Id, new CreateInviteRequest(CareCircleRole.Caregiver, 7, 1));

        var preview = await invites.PreviewAsync(invite.Token);

        preview.CircleName.Should().Be("Mamma");
        preview.Role.Should().Be(CareCircleRole.Caregiver);
        preview.InvitedByDisplayName.Should().Be("Anna");
    }
}
