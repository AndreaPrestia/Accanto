using Accanto.Application.Auth;
using Accanto.Application.Security;
using Accanto.Domain.Enums;

namespace Accanto.Tests;

public class NoOpSecurityAuditLog : ISecurityAuditLog
{
    public record Call(
        Guid? UserId,
        SecurityAuditEventType EventType,
        string? Summary,
        string? EmailAttempted,
        ClientInfo? Client);

    public List<Call> Calls { get; } = new();

    public Task LogAsync(
        Guid? userId,
        SecurityAuditEventType eventType,
        string? summary = null,
        string? emailAttempted = null,
        ClientInfo? client = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new Call(userId, eventType, summary, emailAttempted, client));
        return Task.CompletedTask;
    }
}
