using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _rootFull;
    private readonly IFieldProtector _protector;

    public LocalFileStorage(IOptions<StorageOptions> opt, IFieldProtector protector)
    {
        var root = opt.Value.RootPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Storage RootPath non configurato.");
        Directory.CreateDirectory(root);
        _rootFull = Path.GetFullPath(root);
        if (!_rootFull.EndsWith(Path.DirectorySeparatorChar))
            _rootFull += Path.DirectorySeparatorChar;
        _protector = protector;
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName);
        if (ext.Length > 16) ext = ext.Substring(0, 16);
        ext = SanitizeExtension(ext);

        var now = DateTimeOffset.UtcNow;
        // subDir/internalName sono interamente server-generated (anno/mese a 4/2
        // cifre, GUID): nessun input utente, nessun segmento puo' essere assoluto.
        // codeql[cs/path-combine]
        var subDir = Path.Combine(now.Year.ToString("D4"), now.Month.ToString("D2"));
        // codeql[cs/path-combine]
        var dirFull = Path.Combine(_rootFull, subDir);
        Directory.CreateDirectory(dirFull);

        var internalName = Guid.NewGuid().ToString("N") + ext;
        // codeql[cs/path-combine]
        var relative = Path.Combine(subDir, internalName).Replace('\\', '/');
        // Difesa comunque applicata: GetFullPath + EnsureWithinRoot sotto.
        // codeql[cs/path-combine]
        var fullPath = Path.GetFullPath(Path.Combine(_rootFull, relative));
        EnsureWithinRoot(fullPath);

        // Carichiamo l'intero contenuto in memoria (limite upload 20 MB applicato a monte) e cifriamo con AES-GCM.
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var plaintext = ms.ToArray();
        var encrypted = _protector.EncryptBytes(plaintext);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await fs.WriteAsync(encrypted.AsMemory(), cancellationToken);
        }
        // Registriamo la dimensione del plaintext: e' quella significativa per l'utente.
        return new StoredFile(internalName, relative, plaintext.LongLength);
    }

    public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndGuard(relativePath);
        if (!File.Exists(full)) throw new FileNotFoundException("File non trovato.", relativePath);
        var encrypted = await File.ReadAllBytesAsync(full, cancellationToken);
        var plaintext = _protector.DecryptBytes(encrypted);
        return new MemoryStream(plaintext, writable: false);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndGuard(relativePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public async Task RewriteWithActiveKeyAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndGuard(relativePath);
        if (!File.Exists(full)) throw new FileNotFoundException("File non trovato.", relativePath);

        var encrypted = await File.ReadAllBytesAsync(full, cancellationToken);
        var plaintext = _protector.DecryptBytes(encrypted);
        var reEncrypted = _protector.EncryptBytes(plaintext);

        // Scrittura atomica: tmp accanto al file + replace.
        var tmp = full + ".rotating";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await fs.WriteAsync(reEncrypted.AsMemory(), cancellationToken);
        }
        File.Move(tmp, full, overwrite: true);
    }

    private string ResolveAndGuard(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Percorso vuoto.", nameof(relativePath));

        // Difesa in profondita' contro il path traversal: un segmento assoluto
        // (es. "C:\\x", "/etc/passwd") farebbe scartare _rootFull a Path.Combine
        // (cs/path-combine). Rifiutiamo esplicitamente path radicati e '..'
        // PRIMA di combinare, cosi' l'input non puo' uscire dalla root.
        // IsPathRooted va valutato sull'input originale (prima di normalizzare).
        var unixStyle = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(relativePath)
            || unixStyle.StartsWith('/')
            || unixStyle.Split('/').Any(seg => seg == ".."))
        {
            throw new UnauthorizedAccessException("Percorso non consentito.");
        }

        var normalized = unixStyle;
        var combined = Path.Combine(_rootFull, normalized.Replace('/', Path.DirectorySeparatorChar));
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
