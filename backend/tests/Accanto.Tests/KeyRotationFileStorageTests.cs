using Accanto.Infrastructure.Security;
using Accanto.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class KeyRotationFileStorageTests
{
    private const string KeyA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string KeyB = "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=";

    private static AesGcmFieldProtector Build(string? master, string? active, params (string id, string b64)[] keys)
    {
        var o = new EncryptionOptions { MasterKey = master, ActiveKeyId = active };
        foreach (var (id, b64) in keys) o.Keys[id] = b64;
        return new AesGcmFieldProtector(Options.Create(o));
    }

    private static LocalFileStorage Storage(string root, AesGcmFieldProtector p)
        => new(Options.Create(new StorageOptions { RootPath = root, MaxFileSizeBytes = 1024 * 1024 }), p);

    [Fact]
    public async Task RewriteWithActiveKey_converts_legacy_v1_file_to_v2_with_active_key()
    {
        var root = Path.Combine(Path.GetTempPath(), "accanto-rotate-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            // Step 1: scrittura iniziale con la sola chiave legacy A (formato v1).
            var legacy = Build(master: KeyA, active: null);
            var legacyStorage = Storage(root, legacy);
            var payload = new byte[] { 10, 20, 30, 40, 50, 60 };
            var stored = await legacyStorage.SaveAsync(new MemoryStream(payload), "x.bin", "application/octet-stream");

            // Step 2: rotazione con key-ring A+B, active=B.
            var ring = Build(master: KeyA, active: "kB", ("kA", KeyA), ("kB", KeyB));
            var ringStorage = Storage(root, ring);
            await ringStorage.RewriteWithActiveKeyAsync(stored.RelativePath);

            // Verifica intermedia: il file su disco ora ha il magic header v2.
            var diskBytes = await File.ReadAllBytesAsync(Path.Combine(root, stored.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            diskBytes[0].Should().Be(0xA1);
            diskBytes[1].Should().Be(0x02);

            // Step 3: lettura con un protector che conosce SOLO la chiave B (legacy dismessa).
            var bOnly = Build(master: null, active: "kB", ("kB", KeyB));
            var bOnlyStorage = Storage(root, bOnly);
            await using var s = await bOnlyStorage.OpenReadAsync(stored.RelativePath);
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            ms.ToArray().Should().Equal(payload);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
