using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Push;

public class DevicePushTokenService : IDevicePushTokenService
{
    private readonly IAccantoDbContext _db;

    public DevicePushTokenService(IAccantoDbContext db)
    {
        _db = db;
    }

    public async Task<DevicePushTokenDto> RegisterAsync(Guid userId, RegisterDevicePushTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Token mancante.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Platform))
            throw new ArgumentException("Platform mancante.", nameof(request));

        var token = request.Token.Trim();
        var platform = request.Platform.Trim().ToLowerInvariant();
        var deviceName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? null
            : request.DeviceName.Trim();

        var now = DateTimeOffset.UtcNow;

        // Upsert per Token (univoco): se esiste già lo riassegniamo a
        // questo utente. Caso reale: device condiviso, app reinstallata
        // con account diverso.
        var existing = await _db.DevicePushTokens
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
        if (existing is null)
        {
            existing = new DevicePushToken
            {
                Id = Guid.NewGuid(),
                Token = token,
                UserId = userId,
                Platform = platform,
                DeviceName = deviceName,
                CreatedAt = now,
                LastUsedAt = now
            };
            _db.DevicePushTokens.Add(existing);
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.LastUsedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(existing);
    }

    public async Task<IReadOnlyList<DevicePushTokenDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.DevicePushTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastUsedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<bool> RemoveByIdAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var row = await _db.DevicePushTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);
        if (row is null) return false;
        _db.DevicePushTokens.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveByTokenAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var row = await _db.DevicePushTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.UserId == userId, cancellationToken);
        if (row is null) return false;
        _db.DevicePushTokens.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveInvalidTokensAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null || tokens.Count == 0) return;
        var rows = await _db.DevicePushTokens
            .Where(t => tokens.Contains(t.Token))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return;
        _db.DevicePushTokens.RemoveRange(rows);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static DevicePushTokenDto ToDto(DevicePushToken t)
        => new(t.Id, t.Token, t.Platform, t.DeviceName, t.CreatedAt, t.LastUsedAt);
}
