using Accanto.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "accanto-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private LocalFileStorage Create()
    {
        var opt = Options.Create(new StorageOptions { RootPath = _root, MaxFileSizeBytes = 1_000_000 });
        return new LocalFileStorage(opt, new NullFieldProtector());
    }

    [Fact]
    public async Task Save_and_OpenRead_roundtrip()
    {
        var storage = Create();
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var stored = await storage.SaveAsync(ms, "diagnosi.pdf", "application/pdf");

        stored.SizeInBytes.Should().Be(4);
        stored.RelativePath.Should().EndWith(".pdf");

        await using var read = await storage.OpenReadAsync(stored.RelativePath);
        var bytes = new byte[4];
        await read.ReadExactlyAsync(bytes);
        bytes.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task OpenRead_rejects_path_traversal()
    {
        var storage = Create();
        var act = async () => await storage.OpenReadAsync("../escape.txt");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task File_on_disk_is_encrypted_when_using_real_protector()
    {
        var opt = Options.Create(new StorageOptions { RootPath = _root, MaxFileSizeBytes = 1_000_000 });
        var protector = new Accanto.Infrastructure.Security.AesGcmFieldProtector(
            Options.Create(new Accanto.Infrastructure.Security.EncryptionOptions
            {
                MasterKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
            }));
        var storage = new LocalFileStorage(opt, protector);

        var plaintext = System.Text.Encoding.UTF8.GetBytes("contenuto sensibile in chiaro");
        using var ms = new MemoryStream(plaintext);
        var stored = await storage.SaveAsync(ms, "note.txt", "text/plain");

        // Dimensione registrata = plaintext
        stored.SizeInBytes.Should().Be(plaintext.LongLength);

        // Su disco i byte non corrispondono al plaintext
        var onDiskPath = Path.Combine(_root, stored.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var onDisk = await File.ReadAllBytesAsync(onDiskPath);
        onDisk.Should().NotEqual(plaintext);
        onDisk.Length.Should().Be(plaintext.Length + 12 + 16); // nonce + tag overhead

        // Lettura tramite storage decifra correttamente
        await using var read = await storage.OpenReadAsync(stored.RelativePath);
        var decrypted = new byte[plaintext.Length];
        await read.ReadExactlyAsync(decrypted);
        decrypted.Should().Equal(plaintext);
    }
}
