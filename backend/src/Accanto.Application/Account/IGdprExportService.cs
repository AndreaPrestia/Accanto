namespace Accanto.Application.Account;

public interface IGdprExportService
{
    Task<GdprExportResult> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record GdprExportResult(string FileName, byte[] Content);
