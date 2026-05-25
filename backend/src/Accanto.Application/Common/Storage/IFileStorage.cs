namespace Accanto.Application.Common.Storage;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decifra il file con il key-ring corrente e lo riscrive in-place cifrato con la chiave attiva.
    /// Usato dalla CLI di rotazione: non da invocare dal codice applicativo.
    /// </summary>
    Task RewriteWithActiveKeyAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed record StoredFile(string InternalFileName, string RelativePath, long SizeInBytes);
