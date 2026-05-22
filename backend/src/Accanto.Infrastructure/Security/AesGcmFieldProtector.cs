using System.Security.Cryptography;
using System.Text;
using Accanto.Application.Common.Security;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Security;

/// <summary>
/// Implementazione AES-256-GCM di <see cref="IFieldProtector"/>.
/// Formato blob bytes: [nonce(12)][ciphertext(N)][tag(16)].
/// Formato token string: "v1." + base64(blob).
/// </summary>
public sealed class AesGcmFieldProtector : IFieldProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string Version = "v1";

    private readonly byte[] _key;

    public AesGcmFieldProtector(IOptions<EncryptionOptions> options)
    {
        var raw = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Encryption:MasterKey non configurata. Genera una chiave con `openssl rand -base64 32` e impostala come variabile d'ambiente Encryption__MasterKey.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Encryption:MasterKey non e' un base64 valido.", ex);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Encryption:MasterKey deve decodificare a 32 byte (AES-256), trovati {key.Length}.");
        }

        _key = key;
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var blob = EncryptBytes(pt);
        return Version + "." + Convert.ToBase64String(blob);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        var dot = ciphertext.IndexOf('.');
        if (dot <= 0)
        {
            throw new CryptographicException("Token cifrato non valido: manca il prefisso di versione.");
        }

        var ver = ciphertext[..dot];
        if (ver != Version)
        {
            throw new CryptographicException($"Versione token non supportata: {ver}.");
        }

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(ciphertext[(dot + 1)..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Token cifrato non valido: base64 malformato.", ex);
        }

        var pt = DecryptBytes(blob);
        return Encoding.UTF8.GetString(pt);
    }

    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext)
    {
        var output = new byte[NonceSize + plaintext.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        var ct = output.AsSpan(NonceSize, plaintext.Length);
        var tag = output.AsSpan(NonceSize + plaintext.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ct, tag);
        return output;
    }

    public byte[] DecryptBytes(ReadOnlySpan<byte> ciphertextBlob)
    {
        if (ciphertextBlob.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Blob cifrato troppo corto.");
        }

        var nonce = ciphertextBlob[..NonceSize];
        var ctLen = ciphertextBlob.Length - NonceSize - TagSize;
        var ct = ciphertextBlob.Slice(NonceSize, ctLen);
        var tag = ciphertextBlob.Slice(NonceSize + ctLen, TagSize);

        var pt = new byte[ctLen];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ct, tag, pt);
        return pt;
    }
}
