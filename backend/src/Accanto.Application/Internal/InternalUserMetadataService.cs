using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Internal;

public class InternalUserMetadataService : IInternalUserMetadataService
{
    private readonly IAccantoDbContext _db;

    public InternalUserMetadataService(IAccantoDbContext db)
    {
        _db = db;
    }

    public async Task<InternalUserListResponse> ListAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(u => u.Email.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }
        if (disabled.HasValue)
        {
            q = q.Where(u => u.IsDisabled == disabled.Value);
        }

        var total = await q.CountAsync(cancellationToken);

        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<InternalUserMetadataDto>(users.Count);
        foreach (var u in users)
            items.Add(await BuildAsync(u, cancellationToken));

        return new InternalUserListResponse(items, page, pageSize, total);
    }

    public async Task<InternalUserMetadataDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (u is null) return null;
        return await BuildAsync(u, cancellationToken);
    }

    // Aggregati calcolati per singolo utente. Volutamente SOLO conteggi/somme:
    // nessun nome, titolo, filename, path o contenuto viene mai materializzato.
    private async Task<InternalUserMetadataDto> BuildAsync(User u, CancellationToken ct)
    {
        var circleIds = await _db.CareCircleMembers.AsNoTracking()
            .Where(m => m.UserId == u.Id)
            .Select(m => m.CareCircleId)
            .ToListAsync(ct);

        var careCircleCount = circleIds.Count;

        int documentsCount = 0;
        long storageUsedBytes = 0;
        int timelineEntryCount = 0;

        if (circleIds.Count > 0)
        {
            documentsCount = await _db.MedicalDocuments.AsNoTracking()
                .CountAsync(d => circleIds.Contains(d.CareCircleId), ct);
            storageUsedBytes = await _db.MedicalDocuments.AsNoTracking()
                .Where(d => circleIds.Contains(d.CareCircleId))
                .SumAsync(d => (long?)d.SizeInBytes, ct) ?? 0;
            timelineEntryCount = await _db.TimelineEntries.AsNoTracking()
                .CountAsync(t => circleIds.Contains(t.CareCircleId), ct);
        }

        return new InternalUserMetadataDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.CreatedAt,
            u.IsDisabled,
            AccountStatus(u),
            u.DisabledAt,
            u.DisabledReason,
            careCircleCount,
            documentsCount,
            storageUsedBytes,
            timelineEntryCount);
    }

    private static string AccountStatus(User u)
    {
        if (u.IsErased) return "Erased";
        if (u.IsDisabled) return "Disabled";
        return "Active";
    }
}
