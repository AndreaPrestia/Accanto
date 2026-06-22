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
}
