using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Accanto.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; set; } = "accanto";
    public string Audience { get; set; } = "accanto";

    /// <summary>
    /// Single-key legacy. Se valorizzata e <see cref="Keys"/> e' vuoto, viene
    /// promossa internamente a una entry singola con id "legacy" (vedi
    /// <see cref="ResolveSigningMaterial"/>). Mantenuta per backward compat:
    /// tutti i deploy che oggi usano <c>Jwt__Key</c> continuano a funzionare
    /// senza modifiche di configurazione.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Dizionario <c>keyId → chiave HS256</c>. Tutte le chiavi presenti sono
    /// accettate in fase di validazione (per finestra di grace durante la
    /// rotazione). Solo <see cref="ActiveKeyId"/> viene usata per firmare i
    /// nuovi token.
    /// </summary>
    public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Id della chiave in <see cref="Keys"/> usata per firmare i nuovi token.
    /// Obbligatorio se <see cref="Keys"/> contiene piu' di una entry.
    /// </summary>
    public string? ActiveKeyId { get; set; }

    public int ExpiryMinutes { get; set; } = 480;
    public int RefreshTokenExpiryDays { get; set; } = 30;

    /// <summary>
    /// Materiale crittografico effettivo, calcolato in modo da unificare i
    /// due schemi di config (legacy <see cref="Key"/> vs multi-key
    /// <see cref="Keys"/>). Lanciata in fail-fast all'avvio per evitare che
    /// un deploy mal configurato passi l'health check.
    /// </summary>
    public JwtSigningMaterial ResolveSigningMaterial()
    {
        var combined = new Dictionary<string, SymmetricSecurityKey>(StringComparer.Ordinal);

        foreach (var (kid, secret) in Keys)
        {
            if (string.IsNullOrWhiteSpace(kid))
                throw new InvalidOperationException("Jwt:Keys ha una entry con keyId vuoto.");
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException(
                    $"Jwt:Keys:{kid} troppo corta: {(secret ?? string.Empty).Length} char, minimo 32 (256 bit).");
            combined[kid] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)) { KeyId = kid };
        }

        if (!string.IsNullOrWhiteSpace(Key))
        {
            if (Key.Length < 32)
                throw new InvalidOperationException(
                    $"Jwt:Key troppo corta: {Key.Length} char, minimo 32 (256 bit). Genera con: openssl rand -base64 48");
            if (!combined.ContainsKey(JwtSigningMaterial.LegacyKeyId))
            {
                combined[JwtSigningMaterial.LegacyKeyId] =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)) { KeyId = JwtSigningMaterial.LegacyKeyId };
            }
        }

        if (combined.Count == 0)
            throw new InvalidOperationException(
                "Jwt: nessuna chiave configurata. Imposta Jwt__Key oppure Jwt__Keys__<id> + Jwt__ActiveKeyId.");

        string activeId;
        if (!string.IsNullOrWhiteSpace(ActiveKeyId))
        {
            if (!combined.ContainsKey(ActiveKeyId))
                throw new InvalidOperationException(
                    $"Jwt:ActiveKeyId='{ActiveKeyId}' non presente in Jwt:Keys. Chiavi disponibili: {string.Join(", ", combined.Keys)}.");
            activeId = ActiveKeyId;
        }
        else if (combined.Count == 1)
        {
            activeId = combined.Keys.First();
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt: piu' chiavi configurate ma ActiveKeyId non impostato. Specifica Jwt__ActiveKeyId.");
        }

        return new JwtSigningMaterial(combined, activeId);
    }
}

/// <summary>
/// Snapshot immutabile delle chiavi JWT valide e di quale e' attiva.
/// Calcolato una sola volta all'avvio (registrato come singleton).
/// </summary>
public sealed class JwtSigningMaterial
{
    public const string LegacyKeyId = "legacy";

    public IReadOnlyDictionary<string, SymmetricSecurityKey> Keys { get; }
    public string ActiveKeyId { get; }
    public SymmetricSecurityKey ActiveKey => Keys[ActiveKeyId];

    public JwtSigningMaterial(IReadOnlyDictionary<string, SymmetricSecurityKey> keys, string activeKeyId)
    {
        Keys = keys;
        ActiveKeyId = activeKeyId;
    }

    /// <summary>
    /// Risolve le chiavi candidate per la validazione di un token. Se il
    /// token ha l'header <c>kid</c> noto, ritorna solo quella chiave; se
    /// <paramref name="kid"/> e' null o sconosciuto, ritorna tutte le chiavi
    /// configurate (i token vecchi pre-multikid non hanno <c>kid</c> → la
    /// libreria Microsoft.IdentityModel le prova in sequenza).
    /// </summary>
    public IEnumerable<SecurityKey> Resolve(string? kid)
    {
        if (!string.IsNullOrEmpty(kid) && Keys.TryGetValue(kid, out var match))
            return new[] { (SecurityKey)match };
        return Keys.Values.Cast<SecurityKey>();
    }
}
