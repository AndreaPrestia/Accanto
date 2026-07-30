using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Entities;
using Accanto.Admin.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Application.Users;

public class AdminUserOperationsService : IAdminUserOperationsService
{
    private static readonly HashSet<string> MutatingRoles = new(StringComparer.Ordinal)
        { AdminRoles.Owner, AdminRoles.Operator };

    private readonly IAccantoAdminDbContext _db;
    private readonly IInternalAppClient _app;
    private readonly IAdminAuditLog _audit;
    private readonly TimeProvider _time;

    public AdminUserOperationsService(
        IAccantoAdminDbContext db,
        IInternalAppClient app,
        IAdminAuditLog audit,
        TimeProvider time)
    {
        _db = db;
        _app = app;
        _audit = audit;
        _time = time;
    }

    public Task<AdminUserListResponse> ListAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default)
        => _app.ListUsersAsync(query, disabled, page, pageSize, cancellationToken);

    public async Task<AdminUserMetadataDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _app.GetUserAsync(userId, cancellationToken)
           ?? throw new AdminNotFoundException("Utente non trovato.");

    public Task<AdminOperationResultDto> DisableAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken ct = default)
        => ExecuteAsync(ctx, targetUserId, request, AdminOperationType.DisableUser, "User.Disable",
            (id, reason, c) => _app.DisableUserAsync(id, reason, c), ct);

    public Task<AdminOperationResultDto> EnableAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken ct = default)
        => ExecuteAsync(ctx, targetUserId, request, AdminOperationType.EnableUser, "User.Enable",
            (id, reason, c) => _app.EnableUserAsync(id, reason, c), ct);

    public Task<AdminOperationResultDto> RevokeSessionsAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken ct = default)
        => ExecuteAsync(ctx, targetUserId, request, AdminOperationType.RevokeUserSessions, "User.RevokeSessions",
            (id, _, c) => _app.RevokeUserSessionsAsync(id, c), ct);

    public Task<AdminOperationResultDto> StartDeletionAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken ct = default)
        => ExecuteAsync(ctx, targetUserId, request, AdminOperationType.StartUserDeletion, "User.StartDeletion",
            (id, reason, c) => _app.StartUserDeletionAsync(id, reason!, c), ct);

    private async Task<AdminOperationResultDto> ExecuteAsync(
        AdminOperationContext ctx,
        Guid targetUserId,
        AdminUserOperationRequest request,
        AdminOperationType type,
        string auditAction,
        Func<Guid, string?, CancellationToken, Task> command,
        CancellationToken ct)
    {
        // Ruolo: solo Owner/Operator possono mutare. SecurityAuditor e' read-only.
        if (!ctx.Roles.Any(MutatingRoles.Contains))
            throw new AdminForbiddenException("Ruolo non autorizzato a eseguire operazioni sugli utenti.");

        // Reason obbligatoria su OGNI azione mutativa admin.
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AdminValidationException("La motivazione (reason) e' obbligatoria.");

        if (targetUserId == Guid.Empty)
            throw new AdminValidationException("Target utente non valido.");

        var reason = request.Reason.Trim();
        var now = _time.GetUtcNow();

        var operation = new AdminOperation
        {
            Id = Guid.NewGuid(),
            RequestedByAdminUserId = ctx.AdminUserId,
            OperationType = type,
            TargetUserId = targetUserId,
            Status = AdminOperationStatus.Pending,
            Reason = reason,
            CreatedAt = now
        };
        _db.AdminOperations.Add(operation);

        try
        {
            await command(targetUserId, reason, ct);
            operation.Status = AdminOperationStatus.Completed;
            operation.CompletedAt = _time.GetUtcNow();
        }
        catch (Exception ex)
        {
            operation.Status = AdminOperationStatus.Failed;
            operation.CompletedAt = _time.GetUtcNow();
            operation.ErrorMessage = ex.GetType().Name;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync(ctx.AdminUserId, auditAction, "User", targetUserId.ToString(), reason,
                ctx.Client?.IpAddress, ctx.Client?.UserAgent, ct);
            throw;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(ctx.AdminUserId, auditAction, "User", targetUserId.ToString(), reason,
            ctx.Client?.IpAddress, ctx.Client?.UserAgent, ct);

        return new AdminOperationResultDto(operation.Id, operation.Status.ToString());
    }

    public async Task<AdminOperationListResponse> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.AdminOperations.AsNoTracking().OrderByDescending(o => o.CreatedAt);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new AdminOperationDto(
                o.Id, o.RequestedByAdminUserId, o.OperationType, o.TargetUserId,
                o.Status, o.Reason, o.CreatedAt, o.CompletedAt, o.ErrorMessage))
            .ToListAsync(cancellationToken);

        return new AdminOperationListResponse(items, page, pageSize, total);
    }
}
