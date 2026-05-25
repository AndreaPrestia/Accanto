using Accanto.Application.Auth;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accanto.Infrastructure.Security;

using Accanto.Application.Security;

public class SecurityAuditLog : ISecurityAuditLog
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SecurityAuditLog> _logger;
    private readonly TimeProvider _time;

    public SecurityAuditLog(IServiceScopeFactory scopeFactory, ILogger<SecurityAuditLog> logger, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _time = time;
    }

    public async Task LogAsync(
        Guid? userId,
        SecurityAuditEventType eventType,
        string? summary = null,
        string? emailAttempted = null,
        ClientInfo? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            db.SecurityAuditLogEntries.Add(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmailAttempted = Truncate(emailAttempted, 320),
                EventType = eventType,
                Summary = Truncate(summary, 500),
                IpAddress = Truncate(client?.IpAddress, 64),
                UserAgent = Truncate(client?.UserAgent, 500),
                Timestamp = _time.GetUtcNow()
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore nella scrittura del security audit log ({Event} per utente {UserId})", eventType, userId);
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
