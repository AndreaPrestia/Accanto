using Accanto.Application.Common.Security;

namespace Accanto.Tests;

/// <summary>
/// Implementazione passthrough di <see cref="IFieldProtector"/> per i test unitari
/// che non vogliono dipendere dalla configurazione di una chiave master.
/// </summary>
internal sealed class NullFieldProtector : IFieldProtector
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext) => plaintext.ToArray();
    public byte[] DecryptBytes(ReadOnlySpan<byte> ciphertextBlob) => ciphertextBlob.ToArray();
}
