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
        return new LocalFileStorage(opt);
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
}
