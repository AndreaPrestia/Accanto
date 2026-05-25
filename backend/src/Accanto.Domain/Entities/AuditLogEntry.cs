using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public AuditActionType ActionType { get; set; }
    public AuditResourceType ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
