namespace Accanto.Infrastructure.Storage;

public class StorageOptions
{
    public string RootPath { get; set; } = "/data/storage";
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
