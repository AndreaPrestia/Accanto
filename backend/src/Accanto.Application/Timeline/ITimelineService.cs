namespace Accanto.Application.Timeline;

public interface ITimelineService
{
    Task<IReadOnlyList<TimelineEntryDto>> ListAsync(Guid userId, Guid careCircleId, TimelineQuery query, CancellationToken cancellationToken = default);
    Task<TimelineEntryDto> GetAsync(Guid userId, Guid careCircleId, Guid entryId, CancellationToken cancellationToken = default);
    Task<TimelineEntryDto> CreateAsync(Guid userId, Guid careCircleId, CreateTimelineEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimelineEntryDto> UpdateAsync(Guid userId, Guid careCircleId, Guid entryId, UpdateTimelineEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid careCircleId, Guid entryId, CancellationToken cancellationToken = default);
    Task<BulkUpdateResultDto> BulkUpdateAsync(Guid userId, Guid careCircleId, BulkUpdateTimelineEntriesRequest request, CancellationToken cancellationToken = default);
}
