namespace Accanto.Application.Internal;

/// <summary>
/// Comandi account app-owned invocati dagli endpoint interni su richiesta del
/// control plane admin. Il dominio pubblico resta proprietario delle regole:
/// disable/enable/revoke e avvio cancellazione (erasure tombstone, MAI hard
/// delete diretto). Ogni comando richiede una motivazione (tracciata).
/// </summary>
public interface IInternalAdminAccountService
{
    Task DisableAsync(Guid userId, string? reason, CancellationToken cancellationToken = default);
    Task EnableAsync(Guid userId, string? reason, CancellationToken cancellationToken = default);
    Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Avvia la cancellazione GDPR (tombstone) dell'utente. Reason obbligatoria.</summary>
    Task StartDeletionAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
