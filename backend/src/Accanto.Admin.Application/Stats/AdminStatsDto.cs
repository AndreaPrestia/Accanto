namespace Accanto.Admin.Application.Stats;

public sealed record AdminStatsDto(
    int TotalUsers,
    int DisabledUsers,
    long TotalStorageBytes,
    int TotalDocuments,
    int TotalTimelineEntries);
