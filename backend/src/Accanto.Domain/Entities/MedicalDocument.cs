using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class MedicalDocument
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
