using Accanto.Application.Account;
using Accanto.Application.Auth;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Security;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Internal;

public class InternalAdminAccountService : IInternalAdminAccountService
{
    private readonly IAccantoDbContext _db;
    private readonly IRefreshTokenService _refresh;
    private readonly IUserErasureService _erasure;
    private readonly ISecurityAuditLog _audit;
    private readonly TimeProvider _time;

    public InternalAdminAccountService(
        IAccantoDbContext db,
        IRefreshTokenService refresh,
        IUserErasureService erasure,
        ISecurityAuditLog audit,
        TimeProvider time)
    {
        _db = db;
        _refresh = refresh;
        _erasure = erasure;
        _audit = audit;
        _time = time;
    }

    public async Task DisableAsync(Guid userId, string? reason, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!user.IsDisabled)
        {
            user.IsDisabled = true;
            user.DisabledAt = _time.GetUtcNow();
            user.DisabledReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Disabilitare un account revoca anche le sessioni attive (accesso immediato negato).
        await _refresh.RevokeAllForUserAsync(userId, cancellationToken);
        await _audit.LogAsync(userId, SecurityAuditEventType.AllSessionsRevoked, "Account disabilitato da admin", cancellationToken: cancellationToken);
    }

    public async Task EnableAsync(Guid userId, string? reason, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (user.IsDisabled)
        {
            user.IsDisabled = false;
            user.DisabledAt = null;
            user.DisabledReason = null;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists) throw new NotFoundException("Utente non trovato.");

        await _refresh.RevokeAllForUserAsync(userId, cancellationToken);
        await _audit.LogAsync(userId, SecurityAuditEventType.AllSessionsRevoked, "Sessioni revocate da admin", cancellationToken: cancellationToken);
    }

    public async Task StartDeletionAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppValidationException("La motivazione e' obbligatoria per avviare la cancellazione.");

        // Delega al servizio GDPR app-owned: tombstone, NON hard delete diretto.
        await _erasure.EraseAsync(userId, $"Richiesta admin: {reason.Trim()}", cancellationToken);
    }
}
