using Accanto.Application.Common.Storage;
using Accanto.Application.Documents;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Storage;

/// <summary>
/// Replica S3 dei documenti caricati. Legge il blob gia' cifrato dal
/// filesystem locale e ne fa PUT su bucket S3-compatibile (IONOS / AWS).
///
/// Niente decifratura prima del PUT: lo storage cloud ottiene gli
/// stessi byte presenti su disco (AES-256-GCM applicativo). Stessa
/// key relativa = path 1:1, restore ricostruibile copia/sync.
/// </summary>
public class S3DocumentReplica : IS3DocumentReplica
{
    private readonly StorageOptions _localStorage;
    private readonly S3DocumentReplicaOptions _options;
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3DocumentReplica> _logger;
    private readonly string _rootFull;

    public S3DocumentReplica(
        IOptions<StorageOptions> localStorage,
        IOptions<S3DocumentReplicaOptions> options,
        IAmazonS3 client,
        ILogger<S3DocumentReplica> logger)
    {
        _localStorage = localStorage.Value;
        _options = options.Value;
        _client = client;
        _logger = logger;

        var root = _localStorage.RootPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Storage RootPath non configurato.");
        _rootFull = Path.GetFullPath(root);
        if (!_rootFull.EndsWith(Path.DirectorySeparatorChar))
            _rootFull += Path.DirectorySeparatorChar;
    }

    public async Task PutAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var localPath = ResolveAndGuard(storagePath);
        if (!File.Exists(localPath))
            throw new FileNotFoundException("File locale non trovato per replica S3.", storagePath);

        var key = BuildKey(storagePath);

        await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var put = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = fs,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            ContentType = "application/octet-stream"
        };
        await _client.PutObjectAsync(put, cancellationToken);
        _logger.LogInformation("S3 replica PUT ok: s3://{Bucket}/{Key}", _options.Bucket, key);
    }

    public async Task DeleteAllVersionsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(storagePath);

        // Enumera tutte le versioni della key. Sui bucket versionati una
        // semplice DeleteObject lascia la versione originale recuperabile,
        // quindi per GDPR dobbiamo cancellarle esplicitamente. I
        // delete-marker (eventuali) non contengono PII, restano e
        // verranno raccolti dalla manutenzione bucket — opzionale.
        string? versionIdMarker = null;
        var totalDeleted = 0;
        do
        {
            var listReq = new ListVersionsRequest
            {
                BucketName = _options.Bucket,
                Prefix = key,
                MaxKeys = 1000,
                VersionIdMarker = versionIdMarker
            };
            var listResp = await _client.ListVersionsAsync(listReq, cancellationToken);

            if (listResp.Versions is not null)
            {
                foreach (var v in listResp.Versions)
                {
                    if (v.Key != key) continue;
                    await _client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = _options.Bucket,
                        Key = key,
                        VersionId = v.VersionId
                    }, cancellationToken);
                    totalDeleted++;
                }
            }

            versionIdMarker = listResp.NextVersionIdMarker;
        }
        while (!string.IsNullOrEmpty(versionIdMarker));

        _logger.LogInformation("S3 replica DELETE all versions: s3://{Bucket}/{Key} ({N} versioni)",
            _options.Bucket, key, totalDeleted);
    }

    private string BuildKey(string storagePath)
    {
        var prefix = (_options.Prefix ?? string.Empty).TrimEnd('/');
        var rel = storagePath.Replace('\\', '/').TrimStart('/');
        return string.IsNullOrEmpty(prefix) ? rel : $"{prefix}/{rel}";
    }

    private string ResolveAndGuard(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Percorso vuoto.", nameof(relativePath));
        var combined = Path.Combine(_rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var full = Path.GetFullPath(combined);
        if (!full.StartsWith(_rootFull, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Percorso non consentito.");
        return full;
    }
}
