using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class TimelineEntry
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public TimelineEntryType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public TimelineVisibility Visibility { get; set; } = TimelineVisibility.Circle;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
