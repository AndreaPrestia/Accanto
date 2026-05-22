using Accanto.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class AesGcmFieldProtectorTests
{
    // 32 byte casuali (deterministici) in base64
    private const string TestKeyB64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static AesGcmFieldProtector Create(string? key = TestKeyB64) =>
        new(Options.Create(new EncryptionOptions { MasterKey = key }));

    [Fact]
    public void String_roundtrip_preserves_value()
    {
        var p = Create();
        var token = p.Encrypt("dati clinici riservati");
        token.Should().StartWith("v1.");
        p.Decrypt(token).Should().Be("dati clinici riservati");
    }

    [Fact]
    public void String_ciphertext_differs_each_time_due_to_random_nonce()
    {
        var p = Create();
        var a = p.Encrypt("hello");
        var b = p.Encrypt("hello");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Bytes_roundtrip_preserves_value()
    {
        var p = Create();
        var input = new byte[] { 1, 2, 3, 4, 5 };
        var blob = p.EncryptBytes(input);
        blob.Should().NotEqual(input);
        p.DecryptBytes(blob).Should().Equal(input);
    }

    [Fact]
    public void Tampered_blob_fails()
    {
        var p = Create();
        var blob = p.EncryptBytes(new byte[] { 9, 9, 9 });
        blob[^1] ^= 0x01; // corrompi il tag
        var act = () => p.DecryptBytes(blob);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Missing_key_throws()
    {
        var act = () => Create(null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Wrong_key_size_throws()
    {
        // 16 byte di zeri in base64 (AES-128, non supportato qui)
        var act = () => Create("AAAAAAAAAAAAAAAAAAAAAA==");
        act.Should().Throw<InvalidOperationException>();
    }
}
