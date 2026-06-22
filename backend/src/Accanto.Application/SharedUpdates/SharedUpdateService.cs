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

namespace Accanto.Application.SharedUpdates;

public class SharedUpdateService : ISharedUpdateService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IAuditLog _audit;
    private readonly ICircleEmailNotifier _email;
    private readonly ICircleMobilePushNotifier _mobilePush;
    private readonly IValidator<CreateSharedUpdateRequest> _createValidator;

    public SharedUpdateService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IAuditLog audit,
        ICircleEmailNotifier email,
        ICircleMobilePushNotifier mobilePush,
        IValidator<CreateSharedUpdateRequest> createValidator)
    {
        _db = db;
        _auth = auth;
        _audit = audit;
        _email = email;
        _mobilePush = mobilePush;
        _createValidator = createValidator;
    }

    public async Task<IReadOnlyList<SharedUpdateDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);
        var rows = await _db.SharedUpdates
            .Where(u => u.CareCircleId == careCircleId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<SharedUpdateDto> GetAsync(Guid userId, Guid careCircleId, Guid updateId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);
        var u = await _db.SharedUpdates.FirstOrDefaultAsync(x => x.Id == updateId && x.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Aggiornamento non trovato.");
        return Map(u);
    }

    public async Task<SharedUpdateDto> CreateAsync(Guid userId, Guid careCircleId, CreateSharedUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        var u = new SharedUpdate
        {
            Id = Guid.NewGuid(),
            CareCircleId = careCircleId,
            CreatedByUserId = userId,
            Audience = request.Audience,
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.SharedUpdates.Add(u);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.UpdateCreated, AuditResourceType.SharedUpdate, u.Id, null, CancellationToken.None);

        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == careCircleId, cancellationToken);
        var circleName = circle?.Name ?? "Cerchio";
        var author = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        var authorName = author?.DisplayName ?? "Qualcuno";
        _ = _email.NotifyCircleAsync(careCircleId, userId, NotificationTopic.SharedUpdateCreated,
            $"Nuovo aggiornamento da {circleName}",
            EmailTemplates.SharedUpdateCreated(circleName, authorName),
            CancellationToken.None);
        _ = _mobilePush.NotifyCircleAsync(
            careCircleId,
            userId,
            NotificationTopic.SharedUpdateCreated,
            circleName,
            $"Nuovo aggiornamento da {authorName}",
            new Dictionary<string, string>
            {
                ["circleId"] = careCircleId.ToString(),
                ["updateId"] = u.Id.ToString()
            },
            CancellationToken.None);

        return Map(u);
    }

    public async Task DeleteAsync(Guid userId, Guid careCircleId, Guid updateId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        var u = await _db.SharedUpdates.FirstOrDefaultAsync(x => x.Id == updateId && x.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Aggiornamento non trovato.");
        _db.SharedUpdates.Remove(u);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.UpdateDeleted, AuditResourceType.SharedUpdate, updateId, null, CancellationToken.None);
    }

    private static SharedUpdateDto Map(SharedUpdate u) =>
        new(u.Id, u.CareCircleId, u.CreatedByUserId, u.Audience, u.Content, u.CreatedAt);
}
