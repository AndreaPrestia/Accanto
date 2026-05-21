using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.CareCircles;

public class CareCircleService : ICareCircleService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IValidator<CreateCareCircleRequest> _createValidator;
    private readonly IValidator<UpdateCareCircleRequest> _updateValidator;

    public CareCircleService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IValidator<CreateCareCircleRequest> createValidator,
        IValidator<UpdateCareCircleRequest> updateValidator)
    {
        _db = db;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<CareCircleDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from c in _db.CareCircles
            join m in _db.CareCircleMembers on c.Id equals m.CareCircleId
            where m.UserId == userId
            orderby c.CreatedAt descending
            select new { Circle = c, m.Role }
        ).ToListAsync(cancellationToken);

        return rows.Select(r => Map(r.Circle, r.Role)).ToList();
    }

    public async Task<CareCircleDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, id, CareCircleRole.Viewer, cancellationToken);

        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Cerchia non trovata.");
        var role = await _db.CareCircleMembers
            .Where(m => m.CareCircleId == id && m.UserId == userId)
            .Select(m => m.Role)
            .FirstAsync(cancellationToken);

        return Map(circle, role);
    }

    public async Task<CareCircleDto> CreateAsync(Guid userId, CreateCareCircleRequest request, CancellationToken cancellationToken = default)
    {
        var v = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var now = DateTimeOffset.UtcNow;
        var circle = new CareCircle
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = CareCircleStatus.Active,
            CreatedByUserId = userId,
            CreatedAt = now
        };
        var membership = new CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UserId = userId,
            Role = CareCircleRole.Owner,
            CreatedAt = now
        };

        _db.CareCircles.Add(circle);
        _db.CareCircleMembers.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(circle, CareCircleRole.Owner);
    }

    public async Task<CareCircleDto> UpdateAsync(Guid userId, Guid id, UpdateCareCircleRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, id, CareCircleRole.Caregiver, cancellationToken);

        var v = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Cerchia non trovata.");

        circle.Name = request.Name.Trim();
        circle.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        circle.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var role = await _db.CareCircleMembers
            .Where(m => m.CareCircleId == id && m.UserId == userId)
            .Select(m => m.Role)
            .FirstAsync(cancellationToken);
        return Map(circle, role);
    }

    public async Task ArchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, id, CareCircleRole.Owner, cancellationToken);

        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Cerchia non trovata.");
        circle.Status = CareCircleStatus.Archived;
        circle.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CareCircleDto Map(CareCircle c, CareCircleRole role) =>
        new(c.Id, c.Name, c.Description, c.Status, role, c.CreatedAt, c.UpdatedAt);

    private static AppValidationException ToValidation(FluentValidation.Results.ValidationResult v) =>
        new("Dati non validi.",
            v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
