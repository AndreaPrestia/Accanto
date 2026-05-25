using Accanto.Application.Audit;
using Accanto.Application.Common;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Security;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Security;

public sealed record SecurityAuditEntryDto(
    Guid Id,
    Guid? UserId,
    SecurityAuditEventType EventType,
    string? Summary,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset Timestamp);

public interface ISecurityAuditQueryService
{
    Task<PagedResult<SecurityAuditEntryDto>> ListForUserAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);
}

public class SecurityAuditQueryService : ISecurityAuditQueryService
{
    private const int MaxTake = 200;
    private readonly IAccantoDbContext _db;

    public SecurityAuditQueryService(IAccantoDbContext db) => _db = db;

    public async Task<PagedResult<SecurityAuditEntryDto>> ListForUserAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0) take = 50;
        if (take > MaxTake) take = MaxTake;

        var query = _db.SecurityAuditLogEntries.Where(e => e.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(take)
            .Select(e => new SecurityAuditEntryDto(e.Id, e.UserId, e.EventType, e.Summary, e.IpAddress, e.UserAgent, e.Timestamp))
            .ToListAsync(cancellationToken);
        return new PagedResult<SecurityAuditEntryDto>(page, total, skip, take);
    }
}
