using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Users;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Tests;

public class AdminUserOperationsServiceTests
{
    private static AdminOperationContext Ctx(params string[] roles)
        => new(Guid.NewGuid(), roles.ToList(), null);

    private static AdminUserOperationsService Create(
        Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db,
        FakeInternalAppClient app,
        NoOpAdminAuditLog audit)
        => new(db, app, audit, TimeProvider.System);

    [Fact]
    public async Task Disable_completes_operation_and_audits()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var audit = new NoOpAdminAuditLog();
        var svc = Create(db, app, audit);
        var target = Guid.NewGuid();

        var result = await svc.DisableAsync(Ctx(AdminRoles.Owner), target, new AdminUserOperationRequest("support request"));

        result.Status.Should().Be("Completed");
        app.Calls.Should().ContainSingle().Which.Should().Be(("Disable", target, "support request"));

        var op = await db.AdminOperations.SingleAsync();
        op.OperationType.Should().Be(AdminOperationType.DisableUser);
        op.Status.Should().Be(AdminOperationStatus.Completed);
        op.TargetUserId.Should().Be(target);
        op.Reason.Should().Be("support request");
        op.CompletedAt.Should().NotBeNull();

        audit.Calls.Should().ContainSingle().Which.Action.Should().Be("User.Disable");
    }

    [Fact]
    public async Task Reason_is_required()
    {
        using var db = AdminTestDb.Create();
        var svc = Create(db, new FakeInternalAppClient(), new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.DisableAsync(Ctx(AdminRoles.Owner), Guid.NewGuid(), new AdminUserOperationRequest("  ")))
            .Should().ThrowAsync<AdminValidationException>();

        (await db.AdminOperations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SecurityAuditor_cannot_mutate()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var svc = Create(db, app, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.DisableAsync(Ctx(AdminRoles.SecurityAuditor), Guid.NewGuid(), new AdminUserOperationRequest("reason")))
            .Should().ThrowAsync<AdminForbiddenException>();

        app.Calls.Should().BeEmpty();
        (await db.AdminOperations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Operator_can_mutate()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var svc = Create(db, app, new NoOpAdminAuditLog());

        var result = await svc.EnableAsync(Ctx(AdminRoles.Operator), Guid.NewGuid(), new AdminUserOperationRequest("ok"));
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task RevokeSessions_works()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var svc = Create(db, app, new NoOpAdminAuditLog());
        var target = Guid.NewGuid();

        var result = await svc.RevokeSessionsAsync(Ctx(AdminRoles.Owner), target, new AdminUserOperationRequest("suspicious activity"));

        result.Status.Should().Be("Completed");
        app.Calls.Should().ContainSingle().Which.Item1.Should().Be("Revoke");
        (await db.AdminOperations.SingleAsync()).OperationType.Should().Be(AdminOperationType.RevokeUserSessions);
    }

    [Fact]
    public async Task StartDeletion_delegates_to_app_not_hard_delete()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var svc = Create(db, app, new NoOpAdminAuditLog());
        var target = Guid.NewGuid();

        var result = await svc.StartDeletionAsync(Ctx(AdminRoles.Owner), target, new AdminUserOperationRequest("user requested deletion"));

        result.Status.Should().Be("Completed");
        app.Calls.Should().ContainSingle().Which.Should().Be(("Delete", target, "user requested deletion"));
        (await db.AdminOperations.SingleAsync()).OperationType.Should().Be(AdminOperationType.StartUserDeletion);
    }

    [Fact]
    public async Task Failed_command_marks_operation_failed_and_rethrows()
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient { ThrowOnCommand = true };
        var svc = Create(db, app, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.DisableAsync(Ctx(AdminRoles.Owner), Guid.NewGuid(), new AdminUserOperationRequest("reason")))
            .Should().ThrowAsync<InvalidOperationException>();

        var op = await db.AdminOperations.SingleAsync();
        op.Status.Should().Be(AdminOperationStatus.Failed);
        op.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
