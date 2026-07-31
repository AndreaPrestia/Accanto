using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Accanto.Admin.Application.Common.Security;

/// <summary>
/// Opzioni JWT del control plane admin. Sezione di configurazione DEDICATA
/// (<c>AdminJwt</c>): issuer/audience/chiavi DISTINTI da quelli pubblici
/// (<c>Jwt</c>). Cosi' un token pubblico non e' valido sugli endpoint admin
/// e viceversa. Supporta single-key legacy e multi-key con rotazione.
/// </summary>
public class AdminJwtOptions
{
    public string Issuer { get; set; } = "accanto-admin";
    public string Audience { get; set; } = "accanto-admin";

    /// <summary>Single-key legacy (<c>AdminJwt__Key</c> / <c>AdminJwt__SigningKey</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Dizionario keyId → chiave HS256 per rotazione zero-downtime.</summary>
    public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Id della chiave attiva per firmare nuovi token (obbligatorio se piu' di una chiave).</summary>
    public string? ActiveKeyId { get; set; }

    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;

    public AdminJwtSigningMaterial ResolveSigningMaterial()
    {
        var combined = new Dictionary<string, SymmetricSecurityKey>(StringComparer.Ordinal);

        foreach (var (kid, secret) in Keys)
        {
            if (string.IsNullOrWhiteSpace(kid))
                throw new InvalidOperationException("AdminJwt:Keys ha una entry con keyId vuoto.");
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException(
                    $"AdminJwt:Keys:{kid} troppo corta: {(secret ?? string.Empty).Length} char, minimo 32 (256 bit).");
            combined[kid] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)) { KeyId = kid };
        }

        if (!string.IsNullOrWhiteSpace(Key))
        {
            if (Key.Length < 32)
                throw new InvalidOperationException(
                    $"AdminJwt:Key troppo corta: {Key.Length} char, minimo 32 (256 bit). Genera con: openssl rand -base64 48");
            if (!combined.ContainsKey(AdminJwtSigningMaterial.LegacyKeyId))
            {
                combined[AdminJwtSigningMaterial.LegacyKeyId] =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)) { KeyId = AdminJwtSigningMaterial.LegacyKeyId };
            }
        }

        if (combined.Count == 0)
            throw new InvalidOperationException(
                "AdminJwt: nessuna chiave configurata. Imposta AdminJwt__Key oppure AdminJwt__Keys__<id> + AdminJwt__ActiveKeyId.");

        string activeId;
        if (!string.IsNullOrWhiteSpace(ActiveKeyId))
        {
            if (!combined.ContainsKey(ActiveKeyId))
                throw new InvalidOperationException(
                    $"AdminJwt:ActiveKeyId='{ActiveKeyId}' non presente in AdminJwt:Keys. Chiavi: {string.Join(", ", combined.Keys)}.");
            activeId = ActiveKeyId;
        }
        else if (combined.Count == 1)
        {
            activeId = combined.Keys.First();
        }
        else
        {
            throw new InvalidOperationException(
                "AdminJwt: piu' chiavi configurate ma ActiveKeyId non impostato. Specifica AdminJwt__ActiveKeyId.");
        }

        return new AdminJwtSigningMaterial(combined, activeId);
    }
}

/// <summary>Snapshot immutabile delle chiavi JWT admin valide + chiave attiva.</summary>
public sealed class AdminJwtSigningMaterial
{
    public const string LegacyKeyId = "legacy";

    public IReadOnlyDictionary<string, SymmetricSecurityKey> Keys { get; }
    public string ActiveKeyId { get; }
    public SymmetricSecurityKey ActiveKey => Keys[ActiveKeyId];

    public AdminJwtSigningMaterial(IReadOnlyDictionary<string, SymmetricSecurityKey> keys, string activeKeyId)
    {
        Keys = keys;
        ActiveKeyId = activeKeyId;
    }

    public IEnumerable<SecurityKey> Resolve(string? kid)
    {
        if (!string.IsNullOrEmpty(kid) && Keys.TryGetValue(kid, out var match))
            return new[] { (SecurityKey)match };
        return Keys.Values.Cast<SecurityKey>();
    }
}
