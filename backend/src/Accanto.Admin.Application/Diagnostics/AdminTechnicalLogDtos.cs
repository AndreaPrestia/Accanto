namespace Accanto.Admin.Application.Diagnostics;

public sealed record AdminTechnicalLogEntry(
    DateTimeOffset CreatedAt,
    string Level,
    string Component,
    string Event,
    string Code);

public sealed record AdminTechnicalLogListResponse(
    IReadOnlyList<AdminTechnicalLogEntry> Items,
    int Limit,
    int RetainedCount);

public interface IAdminTechnicalLogStore
{
    void RecordUnhandledApiError();
    AdminTechnicalLogListResponse List(int limit);
}

public sealed class InMemoryAdminTechnicalLogStore : IAdminTechnicalLogStore
{
    private const int Capacity = 200;
    private readonly object _gate = new();
    private readonly Queue<AdminTechnicalLogEntry> _entries = new();

    public void RecordUnhandledApiError()
    {
        var entry = new AdminTechnicalLogEntry(
            DateTimeOffset.UtcNow,
            "Error",
            "Admin API",
            "Unhandled API error",
            "ADMIN_API_500");

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
                _entries.Dequeue();
        }
    }

    public AdminTechnicalLogListResponse List(int limit)
    {
        var normalizedLimit = Math.Clamp(limit, 1, Capacity);
        lock (_gate)
        {
            return new AdminTechnicalLogListResponse(
                _entries.Reverse().Take(normalizedLimit).ToArray(),
                normalizedLimit,
                _entries.Count);
        }
    }
}