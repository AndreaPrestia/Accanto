namespace Accanto.Application.Push;

/// <summary>
/// Gestisce i token push registrati per ogni utente: registrazione/upsert
/// quando l'app mobile riceve un nuovo token Expo, deregistrazione su
/// logout o quando il client se ne ha l'intenzione esplicita.
/// </summary>
public interface IDevicePushTokenService
{
    Task<DevicePushTokenDto> RegisterAsync(
        Guid userId,
        RegisterDevicePushTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DevicePushTokenDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Rimuove un token per ID restituito da <see cref="ListAsync"/>.</summary>
    Task<bool> RemoveByIdAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rimuove un token per il valore opaco Expo. Usato dal client mobile
    /// al logout (non conosce gli ID del DB) e dal notifier per cleanup
    /// dei token segnalati come <c>DeviceNotRegistered</c>.
    /// </summary>
    Task<bool> RemoveByTokenAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup interno post-invio: rimuove i token segnalati come
    /// invalidi dall'Expo Push Service, indipendentemente da quale
    /// utente li possiede.
    /// </summary>
    Task RemoveInvalidTokensAsync(
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken = default);
}
