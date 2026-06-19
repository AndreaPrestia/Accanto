using Accanto.Application.Auth;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Storage;
using Accanto.Application.Security;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Accanto.Application.Account;

public class UserErasureService : IUserErasureService
{
    private readonly IAccantoDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IRefreshTokenService _refresh;
    private readonly ISecurityAuditLog _audit;
    private readonly ILogger<UserErasureService> _logger;

    public UserErasureService(
        IAccantoDbContext db,
        IFileStorage storage,
        IRefreshTokenService refresh,
        ISecurityAuditLog audit,
        ILogger<UserErasureService> logger)
    {
        _db = db;
        _storage = storage;
        _refresh = refresh;
        _audit = audit;
        _logger = logger;
    }

    public async Task EraseAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Una motivazione e' obbligatoria per l'erasure.", nameof(reason));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        // Idempotente: chiamate multiple non rompono nulla. Tornare
        // subito evita di ri-emettere audit/outbox per un utente
        // gia' cancellato.
        if (user.IsErased)
        {
            _logger.LogInformation("Erasure skipped: utente {UserId} gia' tombstone", userId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // 1) Cancella tutti i documenti caricati dall'utente: per
        // ognuno enqueue un DELETE nell'outbox S3 (che cancella tutte
        // le versioni) e prova a rimuovere il blob locale.
        var ownDocuments = await _db.MedicalDocuments
            .Where(d => d.UploadedByUserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var doc in ownDocuments)
        {
            _db.DocumentSyncOutbox.Add(new DocumentSyncOutboxEntry
            {
                Id = Guid.NewGuid(),
                DocumentId = null,
                StoragePath = doc.StoragePath,
                Operation = "DELETE",
                Status = "pending",
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
                NextAttemptAt = now
            });
            try { await _storage.DeleteAsync(doc.StoragePath, cancellationToken); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erasure: blob locale non rimosso per {Path}", doc.StoragePath);
            }
        }
        _db.MedicalDocuments.RemoveRange(ownDocuments);

        // 2) Identifica i cerchi dell'utente e separa quelli condivisi
        // (con altri membri) da quelli di cui e' l'unico membro. I
        // condivisi NON si cancellano: la membership viene rimossa.
        // Quelli solo-utente vanno hard-deleted (con cascade documenti
        // del cerchio, timeline, questions, updates, invites).
        var myMemberships = await _db.CareCircleMembers
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);
        var myCircleIds = myMemberships.Select(m => m.CareCircleId).ToList();

        var sharedCircleIds = await _db.CareCircleMembers
            .Where(m => myCircleIds.Contains(m.CareCircleId) && m.UserId != userId)
            .Select(m => m.CareCircleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var soleCircleIds = myCircleIds.Except(sharedCircleIds).ToList();

        // 2a) Per cerchi solo-utente: cascade hard-delete contenuti.
        // I documenti del cerchio vengono cancellati anche da S3 via
        // outbox (alcuni potrebbero gia' essere stati gestiti al
        // punto 1 se UploadedByUserId == userId; il vincolo storage
        // path ne previene duplicati nel worker).
        if (soleCircleIds.Count > 0)
        {
            var soleDocs = await _db.MedicalDocuments
                .Where(d => soleCircleIds.Contains(d.CareCircleId))
                .ToListAsync(cancellationToken);
            foreach (var doc in soleDocs)
            {
                _db.DocumentSyncOutbox.Add(new DocumentSyncOutboxEntry
                {
                    Id = Guid.NewGuid(),
                    DocumentId = null,
                    StoragePath = doc.StoragePath,
                    Operation = "DELETE",
                    Status = "pending",
                    RetryCount = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    NextAttemptAt = now
                });
                try { await _storage.DeleteAsync(doc.StoragePath, cancellationToken); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erasure: blob locale non rimosso per {Path}", doc.StoragePath);
                }
            }
            _db.MedicalDocuments.RemoveRange(soleDocs);

            var timeline = await _db.TimelineEntries
                .Where(t => soleCircleIds.Contains(t.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.TimelineEntries.RemoveRange(timeline);

            var questions = await _db.DoctorQuestions
                .Where(q => soleCircleIds.Contains(q.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.DoctorQuestions.RemoveRange(questions);

            var updates = await _db.SharedUpdates
                .Where(s => soleCircleIds.Contains(s.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.SharedUpdates.RemoveRange(updates);

            var invites = await _db.CareCircleInvites
                .Where(i => soleCircleIds.Contains(i.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.CareCircleInvites.RemoveRange(invites);

            var circles = await _db.CareCircles
                .Where(c => soleCircleIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            // CareCircleMember dipende dal cerchio via cascata EF.
            _db.CareCircles.RemoveRange(circles);
        }

        // 2b) Cerchi condivisi: rimuovi solo la membership. I
        // contenuti restano agli altri membri.
        var sharedMemberships = myMemberships
            .Where(m => sharedCircleIds.Contains(m.CareCircleId))
            .ToList();
        _db.CareCircleMembers.RemoveRange(sharedMemberships);

        // 3) Pulizia dati personali collaterali.
        var pushSubs = await _db.PushSubscriptions
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.PushSubscriptions.RemoveRange(pushSubs);

        var prefs = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.UserNotificationPreferences.RemoveRange(prefs);

        var checkIns = await _db.CaregiverCheckIns
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.CaregiverCheckIns.RemoveRange(checkIns);

        // Inviti pendenti emessi dall'utente verso terzi: cancellati.
        var sentInvites = await _db.CareCircleInvites
            .Where(i => i.CreatedByUserId == userId)
            .ToListAsync(cancellationToken);
        _db.CareCircleInvites.RemoveRange(sentInvites);

        // 4) Tombstone: PII sostituiti, login impossibile.
        // Email: sostituita con un valore univoco non recuperabile.
        // PasswordHash: stringa vuota (Verify() fallisce sempre).
        // 2FA: disattivato e segreti azzerati.
        var shortId = userId.ToString("N").Substring(0, 12);
        user.Email = $"erased-{shortId}@accanto.invalid";
        user.DisplayName = "Utente cancellato";
        user.PasswordHash = string.Empty;
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorRecoveryCodesJson = null;
        user.TwoFactorRequiredFromUtc = null;
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        user.LastFailedLoginAt = null;
        user.Language = null;
        user.IsErased = true;
        user.ErasedAt = now;
        user.ErasureReason = reason.Trim().Substring(0, Math.Min(500, reason.Trim().Length));

        // 5) Salva (transazione: utente tombstoned + cascade + outbox
        // entries tutti insieme).
        await _db.SaveChangesAsync(cancellationToken);

        // 6) Revoca tutte le sessioni attive (refresh tokens).
        await _refresh.RevokeAllForUserAsync(userId, cancellationToken);

        // 7) Audit (NON anonimizzato: lo storico resta).
        await _audit.LogAsync(userId, SecurityAuditEventType.AccountErased,
            $"Right-to-erasure: {user.ErasureReason}", cancellationToken: cancellationToken);

        _logger.LogInformation(
            "User erasure completato per {UserId}: {DocsOwn} doc personali, {SoleCircles} cerchi propri rimossi, {SharedCircles} membership condivise rimosse",
            userId, ownDocuments.Count, soleCircleIds.Count, sharedMemberships.Count);
    }
}
