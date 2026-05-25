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

public sealed record TimelineQuery(
    TimelineEntryType? Type = null,
    string? Tag = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
);

public sealed record BulkUpdateTimelineEntriesRequest(
    IReadOnlyList<Guid> EntryIds,
    IReadOnlyList<string>? TagsToAdd,
    IReadOnlyList<string>? TagsToRemove,
    TimelineVisibility? NewVisibility
);

public sealed record BulkUpdateResultDto(int Updated, int Skipped);
