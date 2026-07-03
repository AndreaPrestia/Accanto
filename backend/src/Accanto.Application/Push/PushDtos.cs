using Accanto.Domain.Enums;

namespace Accanto.Application.Push;

/// <summary>
/// Token Expo registrato per un device dell'utente. Restituito dall'API
/// di account, mostrato nella lista "Dispositivi che ricevono notifiche".
/// </summary>
public sealed record DevicePushTokenDto(
    Guid Id,
    string Token,
    string Platform,
    string? DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);

/// <summary>
/// Payload di registrazione/upsert di un token Expo. Inviato dal client
/// mobile al primo avvio dopo il login (o quando il sistema operativo
/// rilascia un nuovo token).
/// </summary>
public sealed record RegisterDevicePushTokenRequest(
    string Token,
    string Platform,
    string? DeviceName);

/// <summary>
/// Body per la cancellazione "by token" usata dal mobile in fase di
/// logout (il client conosce solo il proprio Expo token, non il GUID
/// del record DB).
/// </summary>
public sealed record DeletePushDeviceRequest(string Token);

/// <summary>
/// Messaggio push da inoltrare all'Expo Push Service. Body keypair (title,
/// body) è quello che il sistema operativo mostra; <c>Data</c> arriva
/// all'app come payload custom e serve per deep-link / topic routing.
/// </summary>
public sealed record ExpoPushMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data,
    NotificationTopic Topic);

/// <summary>
/// Boundary HTTP verso l'Expo Push Service
/// (<c>https://exp.host/--/api/v2/push/send</c>). Astratta per testing
/// (mock HttpClient → ExpoPushClient → spy con risultati simulati).
/// </summary>
public interface IExpoPushClient
{
    /// <summary>
    /// Invia un singolo messaggio a una lista di token Expo. Ritorna la
    /// lista dei token che il server ha considerato invalidi e che vanno
    /// rimossi dal DB (es. <c>DeviceNotRegistered</c>).
    /// </summary>
    Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens,
        ExpoPushMessage message,
        CancellationToken cancellationToken = default);
}
