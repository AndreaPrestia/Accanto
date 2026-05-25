namespace Accanto.Infrastructure.Security;

/// <summary>
/// Configura il key-ring AES-GCM usato per cifrare campi DB e file.
///
/// Compatibilita' all'indietro:
/// - Se e' impostata solo <see cref="MasterKey"/>, l'app si comporta come la v0.2.x:
///   token "v1.&lt;base64&gt;" e blob raw [nonce|ct|tag].
/// - Se sono impostati <see cref="ActiveKeyId"/> + <see cref="Keys"/>, le nuove scritture
///   usano il formato "v2.&lt;keyId&gt;.&lt;base64&gt;" (e blob con magic header) mentre
///   le letture continuano a funzionare sia su v1 (via MasterKey, se ancora configurata)
///   sia su v2 (via il dizionario delle chiavi).
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>
    /// Chiave master legacy (formato v1), base64 → 32 byte. Lasciata configurata per
    /// consentire la lettura di dati cifrati prima dell'introduzione del key-ring.
    /// </summary>
    public string? MasterKey { get; set; }

    /// <summary>
    /// Id della chiave attiva all'interno di <see cref="Keys"/>. Le nuove scritture usano
    /// questa chiave ed emettono token v2.
    /// </summary>
    public string? ActiveKeyId { get; set; }

    /// <summary>
    /// Mappa "id chiave → chiave base64 (32 byte)". L'id viene inserito nel token v2,
    /// in modo da poter ruotare le chiavi mantenendo decifrabili i vecchi dati.
    /// </summary>
    public Dictionary<string, string> Keys { get; set; } = new();
}
