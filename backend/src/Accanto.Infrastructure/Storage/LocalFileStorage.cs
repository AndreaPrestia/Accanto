using Accanto.Application.Common.Storage;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _rootFull;

    public LocalFileStorage(IOptions<StorageOptions> opt)
    {
        var root = opt.Value.RootPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Storage RootPath non configurato.");
        Directory.CreateDirectory(root);
        _rootFull = Path.GetFullPath(root);
        if (!_rootFull.EndsWith(Path.DirectorySeparatorChar))
            _rootFull += Path.DirectorySeparatorChar;
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName);
        if (ext.Length > 16) ext = ext.Substring(0, 16);
        ext = SanitizeExtension(ext);

        var now = DateTimeOffset.UtcNow;
        var subDir = Path.Combine(now.Year.ToString("D4"), now.Month.ToString("D2"));
        var dirFull = Path.Combine(_rootFull, subDir);
        Directory.CreateDirectory(dirFull);

        var internalName = Guid.NewGuid().ToString("N") + ext;
        var relative = Path.Combine(subDir, internalName).Replace('\\', '/');
        var fullPath = Path.GetFullPath(Path.Combine(_rootFull, relative));
        EnsureWithinRoot(fullPath);

        long size;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(fs, cancellationToken);
            size = fs.Length;
        }
        return new StoredFile(internalName, relative, size);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndGuard(relativePath);
        if (!File.Exists(full)) throw new FileNotFoundException("File non trovato.", relativePath);
        Stream stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndGuard(relativePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string ResolveAndGuard(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Percorso vuoto.", nameof(relativePath));
        var combined = Path.Combine(_rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var full = Path.GetFullPath(combined);
        EnsureWithinRoot(full);
        return full;
    }

    private void EnsureWithinRoot(string fullPath)
    {
        if (!fullPath.StartsWith(_rootFull, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Percorso non consentito.");
    }

    private static string SanitizeExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return string.Empty;
        var clean = new string(ext.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        return clean;
    }
}
