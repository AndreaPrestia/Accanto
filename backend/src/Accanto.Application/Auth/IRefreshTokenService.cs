namespace Accanto.Application.Auth;

public interface IRefreshTokenService
{
    /// <summary>
    /// Genera un nuovo refresh token, ne salva l'hash e restituisce il valore in chiaro
    /// (l'unico momento in cui esiste fuori dal client).
    /// </summary>
    Task<IssuedRefreshToken> IssueAsync(Guid userId, ClientInfo? client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida un token in chiaro, lo revoca e ne emette uno nuovo (rotation).
    /// Se il token era già stato revocato in precedenza, considera la sessione compromessa
    /// e revoca tutte le altre sessioni dell'utente.
    /// </summary>
    Task<(IssuedRefreshToken Token, Guid UserId)> RotateAsync(string rawToken, ClientInfo? client, CancellationToken cancellationToken = default);

    /// <summary>Revoca il token fornito (logout di una singola sessione).</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revoca una sessione specifica per id, verificando che appartenga all'utente.</summary>
    Task RevokeByIdAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>Revoca tutte le sessioni attive dell'utente (cambio password, eliminazione account).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Elenco delle sessioni attive dell'utente, marcando quella corrente se conosciuta.</summary>
    Task<IReadOnlyList<ActiveSessionDto>> ListActiveAsync(Guid userId, string? currentRawToken, CancellationToken cancellationToken = default);
}

public sealed record IssuedRefreshToken(Guid Id, string Token, DateTimeOffset ExpiresAt);
