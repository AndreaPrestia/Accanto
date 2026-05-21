using Accanto.Domain.Enums;

namespace Accanto.Application.Documents;

public sealed record UploadDocumentRequest(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    DocumentCategory Category,
    string? Notes,
    List<string> Tags
);

public sealed record DocumentDto(
    Guid Id,
    Guid CareCircleId,
    Guid UploadedByUserId,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    DocumentCategory Category,
    string? Notes,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt
);

public sealed record DocumentDownload(Stream Content, string ContentType, string OriginalFileName, long SizeInBytes);
