namespace Accanto.Application.Documents;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default);
    Task<DocumentDto> GetAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default);
    Task<DocumentDto> UploadAsync(Guid userId, Guid careCircleId, UploadDocumentRequest request, CancellationToken cancellationToken = default);
    Task<DocumentDownload> DownloadAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default);
}
