namespace Accanto.Domain.Entities;

/// <summary>
/// Token "Expo Push" (formato <c>ExponentPushToken[xxx]</c>) registrato da
/// un'app mobile React Native per ricevere notifiche push.
///
/// È deliberatamente separato da <see cref="PushSubscription"/> (Web Push /
/// VAPID): hanno protocollo, payload e ciclo di vita diversi.
/// </summary>
public class DevicePushToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Token Expo nel formato <c>ExponentPushToken[xxx]</c>. Univoco
    /// per dispositivo: se l'utente reinstalla l'app o cambia device il
    /// token cambia.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Piattaforma sorgente: <c>ios</c>, <c>android</c>.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Nome leggibile del device, mostrato all'utente nelle impostazioni.</summary>
    public string? DeviceName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }
}
