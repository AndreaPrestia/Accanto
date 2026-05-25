using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Validation;
using Accanto.Application.Email;
using Accanto.Application.Push;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Timeline;

public class TimelineService : ITimelineService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IPushService _push;
    private readonly ICircleEmailNotifier _email;
    private readonly IAuditLog _audit;
    private readonly IValidator<CreateTimelineEntryRequest> _createValidator;
    private readonly IValidator<UpdateTimelineEntryRequest> _updateValidator;

    public TimelineService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IPushService push,
        ICircleEmailNotifier email,
        IAuditLog audit,
        IValidator<CreateTimelineEntryRequest> createValidator,
        IValidator<UpdateTimelineEntryRequest> updateValidator)
    {
        _db = db;
        _auth = auth;
        _push = push;
        _email = email;
        _audit = audit;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<TimelineEntryDto>> ListAsync(Guid userId, Guid careCircleId, TimelineQuery query, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var q = _db.TimelineEntries.Where(e => e.CareCircleId == careCircleId);

        // private notes only visible to their author
        q = q.Where(e => e.Visibility == TimelineVisibility.Circle || e.CreatedByUserId == userId);

        if (query.Type.HasValue)
        {
            var t = query.Type.Value;
            q = q.Where(e => e.Type == t);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            q = q.Where(e => e.Tags.Contains(tag));
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value;
            q = q.Where(e => e.OccurredAt >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value;
            q = q.Where(e => e.OccurredAt <= to);
        }

        var rows = await q.OrderByDescending(e => e.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<TimelineEntryDto> GetAsync(Guid userId, Guid careCircleId, Guid entryId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var entry = await _db.TimelineEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Voce non trovata.");

        if (entry.Visibility == TimelineVisibility.Private && entry.CreatedByUserId != userId)
        {
            throw new NotFoundException("Voce non trovata.");
        }

        return Map(entry);
    }

    public async Task<TimelineEntryDto> CreateAsync(Guid userId, Guid careCircleId, CreateTimelineEntryRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entry = new TimelineEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = careCircleId,
            CreatedByUserId = userId,
            OccurredAt = request.OccurredAt,
            Type = request.Type,
            Title = request.Title.Trim(),
            Content = request.Content,
            Tags = NormalizeTags(request.Tags),
            Visibility = request.Visibility,
            CreatedAt = now
        };

        _db.TimelineEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        if (entry.Visibility == TimelineVisibility.Circle)
        {
            var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == careCircleId, cancellationToken);
            var circleName = circle?.Name ?? "Cerchio";
            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            var authorName = author?.DisplayName ?? "Qualcuno";
            var payload = new PushNotificationPayload(
                Title: circleName,
                Body: $"Nuova voce nel diario: {entry.Title}",
                Url: $"/care-circles/{careCircleId}/timeline");
            _ = _push.NotifyCircleAsync(careCircleId, userId, payload, CancellationToken.None);
            _ = _email.NotifyCircleAsync(careCircleId, userId, NotificationTopic.TimelineEntryCreated,
                $"Nuova voce nel diario di {circleName}",
                EmailTemplates.TimelineEntryCreated(circleName, authorName, entry.Title),
                CancellationToken.None);
        }

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.EntryCreated, AuditResourceType.TimelineEntry, entry.Id, entry.Title, CancellationToken.None);

        return Map(entry);
    }

    public async Task<TimelineEntryDto> UpdateAsync(Guid userId, Guid careCircleId, Guid entryId, UpdateTimelineEntryRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        await _updateValidator.EnsureValidAsync(request, cancellationToken);

        var entry = await _db.TimelineEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Voce non trovata.");

        if (entry.Visibility == TimelineVisibility.Private && entry.CreatedByUserId != userId)
        {
            throw new ForbiddenException("Non puoi modificare questa voce.");
        }

        entry.OccurredAt = request.OccurredAt;
        entry.Type = request.Type;
        entry.Title = request.Title.Trim();
        entry.Content = request.Content;
        entry.Tags = NormalizeTags(request.Tags);
        entry.Visibility = request.Visibility;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.EntryUpdated, AuditResourceType.TimelineEntry, entry.Id, entry.Title, CancellationToken.None);

        return Map(entry);
    }

    public async Task DeleteAsync(Guid userId, Guid careCircleId, Guid entryId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        var entry = await _db.TimelineEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Voce non trovata.");

        if (entry.Visibility == TimelineVisibility.Private && entry.CreatedByUserId != userId)
        {
            throw new ForbiddenException("Non puoi eliminare questa voce.");
        }

        var title = entry.Title;
        _db.TimelineEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.EntryDeleted, AuditResourceType.TimelineEntry, entryId, title, CancellationToken.None);
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? Enumerable.Empty<string>())
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static TimelineEntryDto Map(TimelineEntry e) => new(
        e.Id, e.CareCircleId, e.CreatedByUserId, e.OccurredAt, e.Type,
        e.Title, e.Content, e.Tags, e.Visibility, e.CreatedAt, e.UpdatedAt);
}
