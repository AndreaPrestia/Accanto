using Accanto.Domain.Enums;

namespace Accanto.Application.Timeline;

public sealed record CreateTimelineEntryRequest(
    DateTimeOffset OccurredAt,
    TimelineEntryType Type,
    string Title,
    string Content,
    List<string> Tags,
    TimelineVisibility Visibility
);

public sealed record UpdateTimelineEntryRequest(
    DateTimeOffset OccurredAt,
    TimelineEntryType Type,
    string Title,
    string Content,
    List<string> Tags,
    TimelineVisibility Visibility
);

public sealed record TimelineEntryDto(
    Guid Id,
    Guid CareCircleId,
    Guid CreatedByUserId,
    DateTimeOffset OccurredAt,
    TimelineEntryType Type,
    string Title,
    string Content,
    IReadOnlyList<string> Tags,
    TimelineVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public sealed record TimelineQuery(TimelineEntryType? Type = null, string? Tag = null);
