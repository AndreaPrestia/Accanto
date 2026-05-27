using Accanto.Application.Audit;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Ai;

public interface IAiInteractionStore
{
    /// <summary>Salva una interazione (chiamata dall'AiService dopo ogni esecuzione).</summary>
    Task<AiInteraction> AddAsync(AiInteraction entity, CancellationToken ct = default);

    /// <summary>
    /// Lista paginata delle interazioni visibili all'utente. Se <paramref name="circleId"/>
    /// è valorizzato e l'utente è Owner del cerchio, include anche le interazioni degli altri
    /// membri legate a quel cerchio (le check-in-reflection sono sempre escluse).
    /// </summary>
    Task<AiInteractionListResponse> ListAsync(Guid userId, Guid? circleId, AiInteractionFunction? function,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>Dettaglio decifrato. 404 se non esiste, 403 se l'utente non ha accesso.</summary>
    Task<AiInteractionDetail> GetAsync(Guid userId, Guid interactionId, CancellationToken ct = default);

    /// <summary>Registra il feedback (replace su valore esistente).</summary>
    Task SubmitFeedbackAsync(Guid userId, Guid interactionId, AiFeedback value, CancellationToken ct = default);
}

public sealed class AiInteractionStore : IAiInteractionStore
{
    private readonly IAccantoDbContext _db;
    private readonly IAuditLog _audit;

    public AiInteractionStore(IAccantoDbContext db, IAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AiInteraction> AddAsync(AiInteraction entity, CancellationToken ct = default)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (entity.CreatedAt == default) entity.CreatedAt = DateTimeOffset.UtcNow;
        _db.AiInteractions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<AiInteractionListResponse> ListAsync(Guid userId, Guid? circleId, AiInteractionFunction? function,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AiInteractions.AsNoTracking().AsQueryable();

        if (circleId.HasValue)
        {
            // Authz: serve essere membro del cerchio. Owner → vede tutte le interazioni del cerchio,
            // tranne le CheckInReflection (sempre personali, CareCircleId = null comunque).
            var member = await _db.CareCircleMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CareCircleId == circleId.Value && m.UserId == userId, ct);
            if (member is null)
                throw new ForbiddenException("not_member_of_care_circle");

            if (member.Role == CareCircleRole.Owner)
            {
                query = query.Where(x => x.CareCircleId == circleId.Value);
            }
            else
            {
                query = query.Where(x => x.CareCircleId == circleId.Value && x.UserId == userId);
            }
        }
        else
        {
            // Default: solo le proprie.
            query = query.Where(x => x.UserId == userId);
        }

        if (function.HasValue)
            query = query.Where(x => x.Function == function.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AiInteractionSummary(
                x.Id,
                x.UserId,
                x.CareCircleId,
                x.Function.ToString(),
                x.Verdict.ToString(),
                x.Feedback.HasValue ? x.Feedback.Value.ToString() : null,
                x.Model,
                x.Language,
                x.TookMs,
                x.CreatedAt))
            .ToListAsync(ct);

        return new AiInteractionListResponse(items, page, pageSize, total);
    }

    public async Task<AiInteractionDetail> GetAsync(Guid userId, Guid interactionId, CancellationToken ct = default)
    {
        var x = await _db.AiInteractions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == interactionId, ct)
            ?? throw new NotFoundException("ai_interaction_not_found");

        await EnsureCanRead(userId, x, ct);

        if (x.UserId != userId)
        {
            _ = _audit.LogAsync(x.CareCircleId ?? Guid.Empty, userId, AuditActionType.AiInteractionViewed,
                AuditResourceType.Ai, x.Id, $"viewed:{x.Function}", CancellationToken.None);
        }

        return new AiInteractionDetail(
            x.Id, x.UserId, x.CareCircleId,
            x.Function.ToString(),
            x.Verdict.ToString(),
            x.Feedback.HasValue ? x.Feedback.Value.ToString() : null,
            x.Model, x.PromptVersion, x.Language, x.TookMs, x.CacheHit, x.CreatedAt,
            x.InputJsonEncrypted,    // già decifrato da EF value-converter
            x.OutputEncrypted);
    }

    public async Task SubmitFeedbackAsync(Guid userId, Guid interactionId, AiFeedback value, CancellationToken ct = default)
    {
        var entity = await _db.AiInteractions.FirstOrDefaultAsync(e => e.Id == interactionId, ct)
            ?? throw new NotFoundException("ai_interaction_not_found");

        // Il feedback può essere lasciato solo dall'autore della richiesta.
        if (entity.UserId != userId)
            throw new ForbiddenException("ai_feedback_forbidden");

        entity.Feedback = value;
        entity.FeedbackAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _ = _audit.LogAsync(entity.CareCircleId ?? Guid.Empty, userId, AuditActionType.AiFeedbackSubmitted,
            AuditResourceType.Ai, entity.Id, $"feedback:{value}", CancellationToken.None);
    }

    private async Task EnsureCanRead(Guid userId, AiInteraction x, CancellationToken ct)
    {
        // Autore: sempre.
        if (x.UserId == userId) return;

        // Check-in reflection: sempre solo l'autore. Non importa se chi chiede è Owner.
        if (x.Function == AiInteractionFunction.CheckInReflection || x.CareCircleId is null)
            throw new ForbiddenException("ai_interaction_forbidden");

        // Owner del cerchio: ok.
        var member = await _db.CareCircleMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CareCircleId == x.CareCircleId && m.UserId == userId, ct);
        if (member?.Role != CareCircleRole.Owner)
            throw new ForbiddenException("ai_interaction_forbidden");
    }
}
