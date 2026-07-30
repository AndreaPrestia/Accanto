using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Users;
using Accanto.Admin.Domain.Entities;
using Accanto.Admin.Domain.Enums;
using FluentAssertions;

namespace Accanto.Admin.Tests;

public class AdminOperationDetailTests
{
    [Fact]
    public async Task GetOperation_returns_detail()
    {
        using var db = AdminTestDb.Create();
        var op = new AdminOperation
        {
            Id = Guid.NewGuid(),
            RequestedByAdminUserId = Guid.NewGuid(),
            OperationType = AdminOperationType.DisableUser,
            TargetUserId = Guid.NewGuid(),
            Status = AdminOperationStatus.Completed,
            Reason = "support request",
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.AdminOperations.Add(op);
        await db.SaveChangesAsync();
        var svc = new AdminUserOperationsService(db, new FakeInternalAppClient(), new NoOpAdminAuditLog(), TimeProvider.System);

        var dto = await svc.GetOperationAsync(op.Id);

        dto.Id.Should().Be(op.Id);
        dto.OperationType.Should().Be(AdminOperationType.DisableUser);
        dto.Status.Should().Be(AdminOperationStatus.Completed);
        dto.Reason.Should().Be("support request");
    }

    [Fact]
    public async Task GetOperation_unknown_throws_not_found()
    {
        using var db = AdminTestDb.Create();
        var svc = new AdminUserOperationsService(db, new FakeInternalAppClient(), new NoOpAdminAuditLog(), TimeProvider.System);

        await FluentActions.Invoking(() => svc.GetOperationAsync(Guid.NewGuid()))
            .Should().ThrowAsync<AdminNotFoundException>();
    }
}
