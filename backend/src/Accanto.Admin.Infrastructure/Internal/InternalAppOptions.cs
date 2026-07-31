using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Accanto.Admin.Infrastructure.Internal;

/// <summary>
/// Config per il client service-to-service verso la app pubblica.
/// Sezione <c>InternalApp</c>: BaseUrl degli endpoint interni + credenziali
/// <c>InternalAdmin</c> (issuer/audience/chiave) che devono MATCHARE quelle
/// configurate nella app pubblica. Chiave DISTINTA da Jwt__ e AdminJwt__.
/// </summary>
public class InternalAppOptions
{
    /// <summary>Base URL della app pubblica (es. http://backend:8080).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Issuer { get; set; } = "accanto-internal-admin";
    public string Audience { get; set; } = "accanto-internal-admin";
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Durata del token service-to-service mintato per ogni chiamata.</summary>
    public int TokenLifetimeMinutes { get; set; } = 5;

    public SymmetricSecurityKey ResolveKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
            throw new InvalidOperationException(
                $"InternalApp:SigningKey troppo corta o mancante ({(SigningKey ?? string.Empty).Length} char, minimo 32).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
