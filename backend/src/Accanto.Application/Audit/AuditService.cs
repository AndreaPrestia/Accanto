using Accanto.Application.Common;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Audit;

public class AuditService : IAuditService
{
    private const int MaxTake = 200;

    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;

    public AuditService(IAccantoDbContext db, ICareCircleAuthorization auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<PagedResult<AuditLogEntryDto>> ListAsync(
        Guid userId,
        Guid careCircleId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        if (skip < 0) skip = 0;
        if (take <= 0) take = 50;
        if (take > MaxTake) take = MaxTake;

        var baseQuery = _db.AuditLogEntries.Where(a => a.CareCircleId == careCircleId);
        var total = await baseQuery.CountAsync(cancellationToken);

        var page = await baseQuery
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var userIds = page.Select(a => a.PerformedByUserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken);
        var byId = users.ToDictionary(u => u.Id, u => u.DisplayName);

        var items = page.Select(a => new AuditLogEntryDto(
            a.Id,
            a.CareCircleId,
            a.PerformedByUserId,
            byId.TryGetValue(a.PerformedByUserId, out var name) ? name : null,
            a.ActionType,
            a.ResourceType,
            a.ResourceId,
            a.Summary,
            a.Timestamp)).ToList();

        return new PagedResult<AuditLogEntryDto>(items, total, skip, take);
    }
}
