using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Accanto.Api.Configuration;

/// <summary>
/// Opzioni per l'autenticazione service-to-service degli endpoint interni
/// (<c>/internal/admin/*</c>) chiamati ESCLUSIVAMENTE dal control plane admin.
/// Sezione di config dedicata <c>InternalAdmin</c> con issuer/audience/chiave
/// DISTINTI sia dal JWT pubblico (<c>Jwt</c>) sia dal JWT admin (<c>AdminJwt</c>).
/// </summary>
public class InternalAdminOptions
{
    public string Issuer { get; set; } = "accanto-internal-admin";
    public string Audience { get; set; } = "accanto-internal-admin";
    public string SigningKey { get; set; } = string.Empty;

    public SymmetricSecurityKey ResolveKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
            throw new InvalidOperationException(
                $"InternalAdmin:SigningKey troppo corta o mancante ({(SigningKey ?? string.Empty).Length} char, minimo 32). Genera con: openssl rand -base64 48");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
