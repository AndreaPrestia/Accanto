using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Validation;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.SharedUpdates;

public class SharedUpdateService : ISharedUpdateService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IValidator<CreateSharedUpdateRequest> _createValidator;

    public SharedUpdateService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IValidator<CreateSharedUpdateRequest> createValidator)
    {
        _db = db;
        _auth = auth;
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
        return Map(u);
    }

    public async Task DeleteAsync(Guid userId, Guid careCircleId, Guid updateId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        var u = await _db.SharedUpdates.FirstOrDefaultAsync(x => x.Id == updateId && x.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Aggiornamento non trovato.");
        _db.SharedUpdates.Remove(u);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SharedUpdateDto Map(SharedUpdate u) =>
        new(u.Id, u.CareCircleId, u.CreatedByUserId, u.Audience, u.Content, u.CreatedAt);
}
