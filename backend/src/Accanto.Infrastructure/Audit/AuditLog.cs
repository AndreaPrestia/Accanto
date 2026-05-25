using Accanto.Application.Audit;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accanto.Infrastructure.Audit;

public class AuditLog : IAuditLog
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLog> _logger;

    public AuditLog(IServiceScopeFactory scopeFactory, ILogger<AuditLog> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid careCircleId,
        Guid performedByUserId,
        AuditActionType actionType,
        AuditResourceType resourceType,
        Guid? resourceId = null,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                CareCircleId = careCircleId,
                PerformedByUserId = performedByUserId,
                ActionType = actionType,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Summary = Truncate(summary, 500),
                Timestamp = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // L'audit non deve mai propagare errori: limita ad un warning.
            _logger.LogWarning(ex, "Errore nella scrittura dell'audit log ({Action} su {Resource} per cerchio {Circle})", actionType, resourceType, careCircleId);
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
