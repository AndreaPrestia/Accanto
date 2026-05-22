namespace Accanto.Infrastructure.Security;

public sealed class EncryptionOptions
{
    /// <summary>
    /// Chiave master in base64. Deve decodificare a esattamente 32 byte (AES-256).
    /// Generabile con: openssl rand -base64 32
    /// </summary>
    public string? MasterKey { get; set; }
}
