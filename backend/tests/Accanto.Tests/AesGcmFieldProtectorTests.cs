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

    // ------------ key-ring v2 ------------

    private const string KeyA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string KeyB = "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=";

    private static AesGcmFieldProtector Keyring(string? master, string? active, params (string id, string b64)[] keys)
    {
        var o = new EncryptionOptions { MasterKey = master, ActiveKeyId = active };
        foreach (var (id, b64) in keys) o.Keys[id] = b64;
        return new AesGcmFieldProtector(Options.Create(o));
    }

    [Fact]
    public void V2_token_roundtrip_uses_active_key()
    {
        var p = Keyring(master: null, active: "k1", ("k1", KeyA));
        var token = p.Encrypt("paziente Rossi");
        token.Should().StartWith("v2.k1.");
        p.Decrypt(token).Should().Be("paziente Rossi");
    }

    [Fact]
    public void V1_token_still_decryptable_when_keyring_configured()
    {
        var legacy = Keyring(master: KeyA, active: null);
        var token = legacy.Encrypt("vecchio dato");

        var ring = Keyring(master: KeyA, active: "k2", ("k2", KeyB));
        ring.Decrypt(token).Should().Be("vecchio dato");
    }

    [Fact]
    public void V2_token_with_unknown_key_id_throws()
    {
        var writer = Keyring(master: null, active: "k1", ("k1", KeyA));
        var token = writer.Encrypt("x");

        var reader = Keyring(master: null, active: "kZ", ("kZ", KeyB));
        var act = () => reader.Decrypt(token);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void ActiveKeyId_not_in_keys_throws()
    {
        var act = () => Keyring(master: null, active: "missing", ("k1", KeyA));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invalid_key_id_throws()
    {
        var o = new EncryptionOptions { ActiveKeyId = "ok" };
        o.Keys["ok"] = KeyA;
        o.Keys["bad id with space"] = KeyB;
        var act = () => new AesGcmFieldProtector(Options.Create(o));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void V2_bytes_roundtrip_via_magic_header()
    {
        var p = Keyring(master: null, active: "k1", ("k1", KeyA));
        var input = new byte[] { 7, 8, 9, 10, 11 };
        var blob = p.EncryptBytes(input);
        blob[0].Should().Be(0xA1);
        blob[1].Should().Be(0x02);
        p.DecryptBytes(blob).Should().Equal(input);
    }

    [Fact]
    public void V1_legacy_bytes_still_decryptable_under_keyring()
    {
        var legacy = Keyring(master: KeyA, active: null);
        var blob = legacy.EncryptBytes(new byte[] { 1, 2, 3 });
        blob[0].Should().NotBe(0xA1);

        var ring = Keyring(master: KeyA, active: "k2", ("k2", KeyB));
        ring.DecryptBytes(blob).Should().Equal(new byte[] { 1, 2, 3 });
    }
}
