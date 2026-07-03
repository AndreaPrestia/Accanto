namespace Accanto.Infrastructure.Push;

/// <summary>
/// Opzioni di runtime per l'integrazione con Expo Push Service.
/// </summary>
public class ExpoPushOptions
{
    /// <summary>
    /// Endpoint base del servizio Expo. Default
    /// <c>https://exp.host/--/api/v2/push/send</c>. Overridabile per
    /// test/staging.
    /// </summary>
    public string Endpoint { get; set; } = "https://exp.host/--/api/v2/push/send";

    /// <summary>
    /// Access token Expo opzionale (richiesto solo se il progetto ha
    /// abilitato "Enhanced Security" su expo.dev). Quando presente viene
    /// inviato come <c>Bearer</c> nell'header Authorization.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Quando <c>true</c> il notifier non effettua chiamate HTTP ma logga
    /// solo. Comodo in test e in ambienti dove il push non è configurato.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Anti-spam: numero minimo di secondi tra due push consecutive verso
    /// lo stesso destinatario sullo stesso topic. Esempio: se l'utente A
    /// crea 10 timeline entries in 30 secondi, gli altri membri del cerchio
    /// ricevono UNA sola push (le successive sono droppate finché la
    /// finestra non scade). Default 30 secondi. 0 disattiva il throttle.
    /// </summary>
    public int MinSecondsBetweenPerUserTopic { get; set; } = 30;
}
