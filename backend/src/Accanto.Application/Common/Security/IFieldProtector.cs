namespace Accanto.Application.Common.Security;

/// <summary>
/// Cifratura/decifratura simmetrica autenticata (AES-GCM) per dati sensibili
/// memorizzati a riposo (campi DB e blob su disco).
/// </summary>
public interface IFieldProtector
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);

    byte[] EncryptBytes(ReadOnlySpan<byte> plaintext);
    byte[] DecryptBytes(ReadOnlySpan<byte> ciphertextBlob);
}
