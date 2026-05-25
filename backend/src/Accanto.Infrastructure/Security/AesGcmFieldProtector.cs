using System.Security.Cryptography;
using System.Text;
using Accanto.Application.Common.Security;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Security;

/// <summary>
/// AES-256-GCM con supporto key-ring.
///
/// Formati supportati:
/// - String token v1 (legacy):  "v1.&lt;base64(nonce|ct|tag)&gt;" cifrato con MasterKey.
/// - String token v2:           "v2.&lt;keyId&gt;.&lt;base64(nonce|ct|tag)&gt;" cifrato con Keys[keyId].
/// - Blob bytes v1 (legacy):    [nonce(12) | ct | tag(16)] cifrato con MasterKey.
/// - Blob bytes v2:             [0xA1, 0x02, keyIdLen:1, keyIdAscii, nonce(12), ct, tag(16)] cifrato con Keys[keyId].
/// </summary>
public sealed class AesGcmFieldProtector : IFieldProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string TokenV1 = "v1";
    private const string TokenV2 = "v2";
    private static readonly byte[] BlobMagicV2 = { 0xA1, 0x02 };

    private readonly byte[]? _legacyKey;
    private readonly string? _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    public AesGcmFieldProtector(IOptions<EncryptionOptions> options)
    {
        var o = options.Value;

        if (!string.IsNullOrWhiteSpace(o.MasterKey))
            _legacyKey = ParseKey(o.MasterKey, "Encryption:MasterKey");

        foreach (var kv in o.Keys)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                throw new InvalidOperationException("Encryption:Keys contiene un id vuoto.");
            if (!IsValidKeyId(kv.Key))
                throw new InvalidOperationException(
                    $"Encryption:Keys:{kv.Key}: id non valido. Sono ammessi lettere, cifre, '-' e '_' (max 32 char).");
            _keys[kv.Key] = ParseKey(kv.Value, $"Encryption:Keys:{kv.Key}");
        }

        _activeKeyId = string.IsNullOrWhiteSpace(o.ActiveKeyId) ? null : o.ActiveKeyId;
        if (_activeKeyId != null && !_keys.ContainsKey(_activeKeyId))
        {
            throw new InvalidOperationException(
                $"Encryption:ActiveKeyId='{_activeKeyId}' non e' presente in Encryption:Keys.");
        }

        if (_legacyKey is null && _activeKeyId is null)
        {
            throw new InvalidOperationException(
                "Encryption: nessuna chiave configurata. Imposta Encryption:MasterKey (legacy) "
                + "oppure Encryption:ActiveKeyId + Encryption:Keys (key-ring v2).");
        }
    }

    /// <summary>Id della chiave attiva (per le nuove scritture v2), oppure null se siamo solo in legacy v1.</summary>
    public string? ActiveKeyId => _activeKeyId;

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var pt = Encoding.UTF8.GetBytes(plaintext);

        if (_activeKeyId is not null)
        {
            var key = _keys[_activeKeyId];
            var blob = EncryptRaw(pt, key);
            return $"{TokenV2}.{_activeKeyId}.{Convert.ToBase64String(blob)}";
        }

        var legacyBlob = EncryptRaw(pt, _legacyKey!);
        return $"{TokenV1}.{Convert.ToBase64String(legacyBlob)}";
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        var firstDot = ciphertext.IndexOf('.');
        if (firstDot <= 0)
            throw new CryptographicException("Token cifrato non valido: manca il prefisso di versione.");

        var ver = ciphertext[..firstDot];
        if (ver == TokenV1)
        {
            if (_legacyKey is null)
                throw new CryptographicException("Token v1 ricevuto ma Encryption:MasterKey non e' configurata.");
            var blob = FromBase64(ciphertext[(firstDot + 1)..]);
            return Encoding.UTF8.GetString(DecryptRaw(blob, _legacyKey));
        }

        if (ver == TokenV2)
        {
            var secondDot = ciphertext.IndexOf('.', firstDot + 1);
            if (secondDot < 0)
                throw new CryptographicException("Token v2 non valido: manca il key id.");
            var keyId = ciphertext.Substring(firstDot + 1, secondDot - firstDot - 1);
            if (!_keys.TryGetValue(keyId, out var key))
                throw new CryptographicException($"Token v2 cifrato con key id '{keyId}' sconosciuto.");
            var blob = FromBase64(ciphertext[(secondDot + 1)..]);
            return Encoding.UTF8.GetString(DecryptRaw(blob, key));
        }

        throw new CryptographicException($"Versione token non supportata: {ver}.");
    }

    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext)
    {
        if (_activeKeyId is not null)
        {
            var key = _keys[_activeKeyId];
            var keyIdBytes = Encoding.ASCII.GetBytes(_activeKeyId);
            var raw = EncryptRaw(plaintext, key);
            var output = new byte[BlobMagicV2.Length + 1 + keyIdBytes.Length + raw.Length];
            var span = output.AsSpan();
            BlobMagicV2.CopyTo(span);
            span[BlobMagicV2.Length] = (byte)keyIdBytes.Length;
            keyIdBytes.CopyTo(span.Slice(BlobMagicV2.Length + 1));
            raw.CopyTo(span.Slice(BlobMagicV2.Length + 1 + keyIdBytes.Length));
            return output;
        }

        return EncryptRaw(plaintext, _legacyKey!);
    }

    public byte[] DecryptBytes(ReadOnlySpan<byte> ciphertextBlob)
    {
        if (ciphertextBlob.Length >= BlobMagicV2.Length
            && ciphertextBlob[0] == BlobMagicV2[0]
            && ciphertextBlob[1] == BlobMagicV2[1])
        {
            if (ciphertextBlob.Length < BlobMagicV2.Length + 1)
                throw new CryptographicException("Blob v2: header troncato.");
            int keyIdLen = ciphertextBlob[BlobMagicV2.Length];
            if (keyIdLen <= 0 || keyIdLen > 32)
                throw new CryptographicException("Blob v2: key id len fuori range.");
            var headerLen = BlobMagicV2.Length + 1 + keyIdLen;
            if (ciphertextBlob.Length < headerLen + NonceSize + TagSize)
                throw new CryptographicException("Blob v2: payload troppo corto.");
            var keyId = Encoding.ASCII.GetString(ciphertextBlob.Slice(BlobMagicV2.Length + 1, keyIdLen));
            if (!_keys.TryGetValue(keyId, out var key))
                throw new CryptographicException($"Blob v2 cifrato con key id '{keyId}' sconosciuto.");
            return DecryptRaw(ciphertextBlob.Slice(headerLen), key);
        }

        if (_legacyKey is null)
            throw new CryptographicException("Blob legacy ricevuto ma Encryption:MasterKey non e' configurata.");
        return DecryptRaw(ciphertextBlob, _legacyKey);
    }

    private static byte[] EncryptRaw(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        var output = new byte[NonceSize + plaintext.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        var ct = output.AsSpan(NonceSize, plaintext.Length);
        var tag = output.AsSpan(NonceSize + plaintext.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ct, tag);
        return output;
    }

    private static byte[] DecryptRaw(ReadOnlySpan<byte> blob, byte[] key)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Blob cifrato troppo corto.");
        var nonce = blob[..NonceSize];
        var ctLen = blob.Length - NonceSize - TagSize;
        var ct = blob.Slice(NonceSize, ctLen);
        var tag = blob.Slice(NonceSize + ctLen, TagSize);

        var pt = new byte[ctLen];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ct, tag, pt);
        return pt;
    }

    private static byte[] ParseKey(string raw, string optionPath)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{optionPath} non e' un base64 valido.", ex);
        }

        if (key.Length != 32)
            throw new InvalidOperationException($"{optionPath} deve decodificare a 32 byte (AES-256), trovati {key.Length}.");
        return key;
    }

    private static byte[] FromBase64(string s)
    {
        try { return Convert.FromBase64String(s); }
        catch (FormatException ex) { throw new CryptographicException("Token cifrato non valido: base64 malformato.", ex); }
    }

    private static bool IsValidKeyId(string id)
    {
        if (id.Length == 0 || id.Length > 32) return false;
        foreach (var c in id)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return true;
    }
}
