using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class SharedUpdate
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public SharedUpdateAudience Audience { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
