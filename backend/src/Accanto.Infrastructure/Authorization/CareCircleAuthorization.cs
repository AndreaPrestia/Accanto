using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Infrastructure.Authorization;

public class CareCircleAuthorization : ICareCircleAuthorization
{
    private readonly IAccantoDbContext _db;

    public CareCircleAuthorization(IAccantoDbContext db) { _db = db; }

    public async Task EnsureMemberAsync(Guid userId, Guid careCircleId, CareCircleRole minimumRole, CancellationToken cancellationToken = default)
    {
        var circleExists = await _db.CareCircles.AnyAsync(c => c.Id == careCircleId, cancellationToken);
        if (!circleExists)
            throw new NotFoundException("Cerchio di cura non trovato.");

        var member = await _db.CareCircleMembers
            .FirstOrDefaultAsync(m => m.CareCircleId == careCircleId && m.UserId == userId, cancellationToken);

        if (member is null)
            throw new ForbiddenException("Non sei membro di questo cerchio di cura.");

        // Lower ordinal = higher privilege (Owner=0, Caregiver=1, Viewer=2).
        if ((int)member.Role > (int)minimumRole)
            throw new ForbiddenException("Permessi insufficienti per questa operazione.");
    }
}
