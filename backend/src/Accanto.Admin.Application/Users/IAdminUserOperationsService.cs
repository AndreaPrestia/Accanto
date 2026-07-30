using Accanto.Admin.Application.Auth;

namespace Accanto.Admin.Application.Users;

public interface IAdminUserOperationsService
{
    Task<AdminUserListResponse> ListAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminUserMetadataDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AdminOperationResultDto> DisableAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResultDto> EnableAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResultDto> RevokeSessionsAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResultDto> StartDeletionAsync(AdminOperationContext ctx, Guid targetUserId, AdminUserOperationRequest request, CancellationToken cancellationToken = default);

    Task<AdminOperationListResponse> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminOperationDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}

/// <summary>Contesto dell'admin che richiede l'operazione (id, ruoli, client).</summary>
public sealed record AdminOperationContext(
    Guid AdminUserId,
    IReadOnlyList<string> Roles,
    AdminClientInfo? Client);
