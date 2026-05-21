namespace Accanto.Application.Documents;

public class DocumentStorageOptions
{
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024; // 20 MB
    public string[] AllowedContentTypes { get; set; } =
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain"
    };
}
