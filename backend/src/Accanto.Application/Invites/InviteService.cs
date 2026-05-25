using System.Security.Cryptography;
using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Invites;

public class InviteService : IInviteService
{
    private const int DefaultExpiryDays = 7;
    private const int DefaultMaxUses = 1;
    private const int TokenBytes = 32;

    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IAuditLog _audit;
    private readonly IValidator<CreateInviteRequest> _createValidator;

    public InviteService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IAuditLog audit,
        IValidator<CreateInviteRequest> createValidator)
    {
        _db = db;
        _auth = auth;
        _audit = audit;
        _createValidator = createValidator;
    }

    public async Task<InviteDto> CreateAsync(Guid userId, Guid careCircleId, CreateInviteRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Owner, cancellationToken);

        var v = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var now = DateTimeOffset.UtcNow;
        var invite = new CareCircleInvite
        {
            Id = Guid.NewGuid(),
            CareCircleId = careCircleId,
            CreatedByUserId = userId,
            Token = GenerateToken(),
            Role = request.Role,
            ExpiresAt = now.AddDays(request.ExpiresInDays ?? DefaultExpiryDays),
            MaxUses = request.MaxUses ?? DefaultMaxUses,
            UsedCount = 0,
            CreatedAt = now
        };

        _db.CareCircleInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.InviteCreated, AuditResourceType.Invite, invite.Id, $"Ruolo {invite.Role}", CancellationToken.None);

        return Map(invite, now);
    }

    public async Task<IReadOnlyList<InviteDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Owner, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var invites = await _db.CareCircleInvites
            .Where(i => i.CareCircleId == careCircleId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(i => Map(i, now)).ToList();
    }

    public async Task RevokeAsync(Guid userId, Guid careCircleId, Guid inviteId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Owner, cancellationToken);

        var invite = await _db.CareCircleInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Invito non trovato.");

        if (invite.RevokedAt is not null) return;

        invite.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.InviteRevoked, AuditResourceType.Invite, invite.Id, null, CancellationToken.None);
    }

    public async Task<InvitePreviewDto> PreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        var invite = await _db.CareCircleInvites
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invito non trovato o non più valido.");

        EnsureUsable(invite);

        var circle = await _db.CareCircles
            .FirstOrDefaultAsync(c => c.Id == invite.CareCircleId, cancellationToken)
            ?? throw new NotFoundException("Cerchio non trovato.");

        var creator = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == invite.CreatedByUserId, cancellationToken);

        return new InvitePreviewDto(
            circle.Name,
            invite.Role,
            invite.ExpiresAt,
            creator?.DisplayName ?? "Qualcuno"
        );
    }

    public async Task<Guid> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var invite = await _db.CareCircleInvites
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invito non trovato o non più valido.");

        EnsureUsable(invite);

        var existing = await _db.CareCircleMembers
            .FirstOrDefaultAsync(m => m.CareCircleId == invite.CareCircleId && m.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            // Già membro: non consumo l'invito, ritorno l'id del cerchio per consentire il redirect.
            return invite.CareCircleId;
        }

        _db.CareCircleMembers.Add(new CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = invite.CareCircleId,
            UserId = userId,
            Role = invite.Role,
            CreatedAt = DateTimeOffset.UtcNow
        });

        invite.UsedCount += 1;
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(invite.CareCircleId, userId, AuditActionType.MemberJoined, AuditResourceType.Membership, userId, $"Ruolo {invite.Role}", CancellationToken.None);

        return invite.CareCircleId;
    }

    private static void EnsureUsable(CareCircleInvite invite)
    {
        var now = DateTimeOffset.UtcNow;
        if (invite.RevokedAt is not null)
            throw new ForbiddenException("Questo invito è stato revocato.");
        if (invite.ExpiresAt <= now)
            throw new ForbiddenException("Questo invito è scaduto.");
        if (invite.UsedCount >= invite.MaxUses)
            throw new ForbiddenException("Questo invito ha già raggiunto il numero massimo di usi.");
    }

    private static InviteDto Map(CareCircleInvite i, DateTimeOffset now)
    {
        var isActive = i.RevokedAt is null && i.ExpiresAt > now && i.UsedCount < i.MaxUses;
        return new InviteDto(i.Id, i.CareCircleId, i.Role, i.Token, i.ExpiresAt, i.MaxUses, i.UsedCount, i.RevokedAt, i.CreatedAt, isActive);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        // base64url senza padding: URL-safe.
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static AppValidationException ToValidation(FluentValidation.Results.ValidationResult v) =>
        new("Dati non validi.",
            v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
