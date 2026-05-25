using Accanto.Application.Audit;
using Accanto.Domain.Enums;

namespace Accanto.Tests;

public class NoOpAuditLog : IAuditLog
{
    public record Call(
        Guid CareCircleId,
        Guid PerformedByUserId,
        AuditActionType ActionType,
        AuditResourceType ResourceType,
        Guid? ResourceId,
        string? Summary);

    public List<Call> Calls { get; } = new();

    public Task LogAsync(
        Guid careCircleId,
        Guid performedByUserId,
        AuditActionType actionType,
        AuditResourceType resourceType,
        Guid? resourceId = null,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new Call(careCircleId, performedByUserId, actionType, resourceType, resourceId, summary));
        return Task.CompletedTask;
    }
}
