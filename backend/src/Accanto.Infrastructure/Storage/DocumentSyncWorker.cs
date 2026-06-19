using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Storage;
using Accanto.Application.Documents;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Storage;

/// <summary>
/// BackgroundService che drena la tabella document_sync_outbox e
/// propaga PUT/DELETE su S3. Polling ogni N secondi (configurabile).
///
/// Backoff esponenziale per le righe fallite:
///   tentativo 1 -> retry dopo 60s
///   tentativo 2 -> retry dopo 5min
///   tentativo 3 -> retry dopo 30min
///   tentativo 4 -> retry dopo 2h
///   tentativo 5 -> retry dopo 6h
///   oltre MaxRetries -> status='failed' (manuale dall'admin via SQL).
/// </summary>
public class DocumentSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly S3DocumentReplicaOptions _options;
    private readonly ILogger<DocumentSyncWorker> _logger;

    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromSeconds(60),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6)
    };

    public DocumentSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<S3DocumentReplicaOptions> options,
        ILogger<DocumentSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DocumentSyncWorker disabilitato (S3DocumentReplica:Enabled=false).");
            return;
        }
        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            _logger.LogWarning("DocumentSyncWorker abilitato ma Bucket vuoto: worker non parte.");
            return;
        }

        _logger.LogInformation("DocumentSyncWorker avviato (poll ogni {Sec}s, batch {Batch}).",
            _options.PollIntervalSeconds, _options.BatchSize);

        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DocumentSyncWorker: errore nel ciclo di polling.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
        var replica = scope.ServiceProvider.GetRequiredService<IS3DocumentReplica>();

        var now = DateTimeOffset.UtcNow;
        var batch = await db.DocumentSyncOutbox
            .Where(o => (o.Status == "pending" || o.Status == "in_progress") && o.NextAttemptAt <= now)
            .OrderBy(o => o.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        foreach (var entry in batch)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (entry.Operation == "PUT")
                {
                    await replica.PutAsync(entry.StoragePath, ct);
                }
                else if (entry.Operation == "DELETE")
                {
                    await replica.DeleteAllVersionsAsync(entry.StoragePath, ct);
                }
                else
                {
                    throw new InvalidOperationException($"Operazione sconosciuta: {entry.Operation}");
                }

                entry.Status = "done";
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex)
            {
                entry.RetryCount++;
                entry.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                entry.UpdatedAt = DateTimeOffset.UtcNow;

                if (entry.RetryCount >= _options.MaxRetries)
                {
                    entry.Status = "failed";
                    _logger.LogError(ex, "DocumentSyncWorker: outbox {Id} ({Op} {Path}) permanente: {N} tentativi.",
                        entry.Id, entry.Operation, entry.StoragePath, entry.RetryCount);
                }
                else
                {
                    var delay = Backoff[Math.Min(entry.RetryCount - 1, Backoff.Length - 1)];
                    entry.Status = "pending";
                    entry.NextAttemptAt = DateTimeOffset.UtcNow.Add(delay);
                    _logger.LogWarning(ex, "DocumentSyncWorker: outbox {Id} ({Op} {Path}) tentativo {N} fallito, retry in {Delay}.",
                        entry.Id, entry.Operation, entry.StoragePath, entry.RetryCount, delay);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
