using Accanto.Admin.Application.Users;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Tests;

/// <summary>Copertura reason-required + audit-write per TUTTE le operazioni mutative.</summary>
public class AdminOperationsAuditTests
{
    private static AdminOperationContext Ctx(params string[] roles)
        => new(Guid.NewGuid(), roles.ToList(), null);

    private static AdminUserOperationsService Create(
        Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db,
        FakeInternalAppClient app,
        NoOpAdminAuditLog audit)
        => new(db, app, audit, TimeProvider.System);

    public static IEnumerable<object[]> MutatingOps()
    {
        yield return new object[] { "Disable", AdminOperationType.DisableUser, "User.Disable" };
        yield return new object[] { "Enable", AdminOperationType.EnableUser, "User.Enable" };
        yield return new object[] { "Revoke", AdminOperationType.RevokeUserSessions, "User.RevokeSessions" };
        yield return new object[] { "Delete", AdminOperationType.StartUserDeletion, "User.StartDeletion" };
    }

    private static Task Run(AdminUserOperationsService svc, string op, AdminOperationContext ctx, Guid target, AdminUserOperationRequest req)
        => op switch
        {
            "Disable" => svc.DisableAsync(ctx, target, req),
            "Enable" => svc.EnableAsync(ctx, target, req),
            "Revoke" => svc.RevokeSessionsAsync(ctx, target, req),
            "Delete" => svc.StartDeletionAsync(ctx, target, req),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [MemberData(nameof(MutatingOps))]
    public async Task Every_mutating_op_requires_reason(string op, AdminOperationType type, string auditAction)
    {
        _ = type; _ = auditAction;
        using var db = AdminTestDb.Create();
        var svc = Create(db, new FakeInternalAppClient(), new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => Run(svc, op, Ctx(AdminRoles.Owner), Guid.NewGuid(), new AdminUserOperationRequest("")))
            .Should().ThrowAsync<Accanto.Admin.Application.Common.AdminValidationException>();
    }

    [Theory]
    [MemberData(nameof(MutatingOps))]
    public async Task Every_mutating_op_writes_audit_and_operation(string op, AdminOperationType type, string auditAction)
    {
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var audit = new NoOpAdminAuditLog();
        var svc = Create(db, app, audit);
        var target = Guid.NewGuid();

        await Run(svc, op, Ctx(AdminRoles.Owner), target, new AdminUserOperationRequest("valid reason here"));

        audit.Calls.Should().Contain(c => c.Action == auditAction && c.TargetId == target.ToString());
        var record = await db.AdminOperations.SingleAsync();
        record.OperationType.Should().Be(type);
        record.Status.Should().Be(AdminOperationStatus.Completed);
    }

    [Theory]
    [MemberData(nameof(MutatingOps))]
    public async Task SecurityAuditor_cannot_run_any_mutating_op(string op, AdminOperationType type, string auditAction)
    {
        _ = type; _ = auditAction;
        using var db = AdminTestDb.Create();
        var app = new FakeInternalAppClient();
        var svc = Create(db, app, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => Run(svc, op, Ctx(AdminRoles.SecurityAuditor), Guid.NewGuid(), new AdminUserOperationRequest("valid reason here")))
            .Should().ThrowAsync<Accanto.Admin.Application.Common.AdminForbiddenException>();

        app.Calls.Should().BeEmpty();
    }
}
