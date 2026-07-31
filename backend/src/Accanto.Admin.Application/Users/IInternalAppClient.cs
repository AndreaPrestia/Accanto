namespace Accanto.Admin.Application.Users;

/// <summary>
/// Client service-to-service verso gli endpoint interni della app pubblica
/// (<c>/internal/admin/*</c>). Autenticato con token InternalAdmin dedicato.
/// Espone SOLO metadata e comandi account: nessun contenuto utente.
/// </summary>
public interface IInternalAppClient
{
    Task<AdminUserListResponse> ListUsersAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminUserMetadataDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DisableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default);
    Task EnableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default);
    Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task StartUserDeletionAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
