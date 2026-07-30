namespace Accanto.Application.Internal;

/// <summary>
/// Query di metadata utente per gli endpoint interni service-to-service.
/// Ritorna SOLO metadata e aggregati (vedi <see cref="InternalUserMetadataDto"/>).
/// Nessun contenuto utente, nessun dato clinico/familiare.
/// </summary>
public interface IInternalUserMetadataService
{
    Task<InternalUserListResponse> ListAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<InternalUserMetadataDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
