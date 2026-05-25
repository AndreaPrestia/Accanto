using Accanto.Domain.Enums;

namespace Accanto.Application.Audit;

public sealed record AuditLogEntryDto(
    Guid Id,
    Guid CareCircleId,
    Guid PerformedByUserId,
    string? PerformedByDisplayName,
    AuditActionType ActionType,
    AuditResourceType ResourceType,
    Guid? ResourceId,
    string? Summary,
    DateTimeOffset Timestamp);
