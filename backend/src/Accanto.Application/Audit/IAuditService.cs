using Accanto.Application.Common;

namespace Accanto.Application.Audit;

public interface IAuditService
{
    Task<PagedResult<AuditLogEntryDto>> ListAsync(
        Guid userId,
        Guid careCircleId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
