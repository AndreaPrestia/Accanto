using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class SecurityAuditLogEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? EmailAttempted { get; set; }
    public SecurityAuditEventType EventType { get; set; }
    public string? Summary { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
