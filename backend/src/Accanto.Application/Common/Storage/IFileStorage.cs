namespace Accanto.Application.Common.Storage;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed record StoredFile(string InternalFileName, string RelativePath, long SizeInBytes);
