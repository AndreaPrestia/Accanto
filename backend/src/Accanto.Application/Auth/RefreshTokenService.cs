using System.Security.Cryptography;
using System.Text;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IAccantoDbContext _db;
    private readonly RefreshTokenOptions _options;
    private readonly TimeProvider _time;

    public RefreshTokenService(IAccantoDbContext db, IOptions<RefreshTokenOptions> options, TimeProvider time)
    {
        _db = db;
        _options = options.Value;
        _time = time;
    }

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, ClientInfo? client, CancellationToken cancellationToken = default)
    {
        var (raw, hash) = GenerateToken();
        var now = _time.GetUtcNow();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.ExpiryDays),
            UserAgent = Truncate(client?.UserAgent, 500),
            IpAddress = Truncate(client?.IpAddress, 64)
        };
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(entity.Id, raw, entity.ExpiresAt);
    }

    public async Task<(IssuedRefreshToken Token, Guid UserId)> RotateAsync(string rawToken, ClientInfo? client, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ForbiddenException("Refresh token non valido.");
        }

        var hash = Hash(rawToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new ForbiddenException("Refresh token non valido.");

        var now = _time.GetUtcNow();

        // Token già revocato → potenziale riuso/compromesso: revoca tutte le sessioni dell'utente.
        if (existing.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(existing.UserId, cancellationToken);
            throw new ForbiddenException("Refresh token non valido.");
        }

        if (now >= existing.ExpiresAt)
        {
            throw new ForbiddenException("Refresh token scaduto.");
        }

        existing.RevokedAt = now;

        var (raw, newHash) = GenerateToken();
        var next = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.ExpiryDays),
            UserAgent = Truncate(client?.UserAgent, 500),
            IpAddress = Truncate(client?.IpAddress, 64)
        };
        existing.ReplacedByTokenId = next.Id;

        _db.RefreshTokens.Add(next);
        await _db.SaveChangesAsync(cancellationToken);
        return (new IssuedRefreshToken(next.Id, raw, next.ExpiresAt), existing.UserId);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }
        var hash = Hash(rawToken);
        var entity = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (entity is null || entity.RevokedAt is not null)
        {
            return;
        }
        entity.RevokedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeByIdAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Sessione non trovata.");
        if (entity.RevokedAt is not null)
        {
            return;
        }
        entity.RevokedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var t in tokens)
        {
            t.RevokedAt = now;
        }
        if (tokens.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ActiveSessionDto>> ListActiveAsync(Guid userId, string? currentRawToken, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var currentHash = string.IsNullOrWhiteSpace(currentRawToken) ? null : Hash(currentRawToken);
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens
            .Select(t => new ActiveSessionDto(
                t.Id,
                t.CreatedAt,
                t.ExpiresAt,
                t.UserAgent,
                t.IpAddress,
                currentHash is not null && t.TokenHash == currentHash))
            .ToList();
    }

    private static (string Raw, string Hash) GenerateToken()
    {
        // 32 byte = 256 bit di entropia, base64url encoded → ~43 caratteri sicuri per URL.
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var raw = Base64UrlEncode(buffer);
        return (raw, Hash(raw));
    }

    private static string Hash(string raw)
    {
        Span<byte> destination = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), destination);
        return Convert.ToHexString(destination).ToLowerInvariant();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
