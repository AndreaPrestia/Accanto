namespace Accanto.Application.Export;

public sealed record CareCircleExportResult(byte[] Bytes, string FileName);

public interface ICareCircleExportService
{
    Task<CareCircleExportResult> ExportPdfAsync(
        Guid userId,
        Guid careCircleId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
