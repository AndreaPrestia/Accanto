using System.Security.Cryptography;
using System.Text;
using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Admin.Application.Auth;

public class AdminAuthService : IAdminAuthService
{
    private readonly IAccantoAdminDbContext _db;
    private readonly IAdminPasswordHasher _hasher;
    private readonly IAdminJwtTokenService _jwt;
    private readonly IAdminAuditLog _audit;
    private readonly AdminJwtOptions _jwtOpt;
    private readonly TimeProvider _time;

    public AdminAuthService(
        IAccantoAdminDbContext db,
        IAdminPasswordHasher hasher,
        IAdminJwtTokenService jwt,
        IAdminAuditLog audit,
        IOptions<AdminJwtOptions> jwtOptions,
        TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _audit = audit;
        _jwtOpt = jwtOptions.Value;
        _time = time;
    }

    public async Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new AdminValidationException("Email e password obbligatorie.");

        var email = request.Email.Trim().ToLowerInvariant();
        var admin = await _db.AdminUsers
            .Include(u => u.Roles).ThenInclude(r => r.AdminRole)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Messaggio generico anti-enumerazione: non rivelare se l'account esiste.
        // Un admin seedato senza password (PasswordHash vuoto) NON puo' loggare
        // finche' non completa il flusso di reset/impostazione password.
        if (admin is null || !admin.IsActive
            || string.IsNullOrEmpty(admin.PasswordHash)
            || !_hasher.Verify(request.Password, admin.PasswordHash))
            throw new AdminUnauthorizedException("Credenziali non valide.");

        admin.LastLoginAt = _time.GetUtcNow();
        var response = await IssueTokensAsync(admin, client, cancellationToken);

        await _audit.WriteAsync(admin.Id, "Admin.Login", "AdminUser", admin.Id.ToString(),
            ipAddress: client?.IpAddress, userAgent: client?.UserAgent, cancellationToken: cancellationToken);

        return response;
    }

    public async Task<AdminAuthResponse> RefreshAsync(AdminRefreshRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new AdminForbiddenException("Refresh token non valido.");

        var hash = Hash(request.RefreshToken);
        var session = await _db.AdminSessions
            .Include(s => s.AdminUser).ThenInclude(u => u.Roles).ThenInclude(r => r.AdminRole)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, cancellationToken)
            ?? throw new AdminForbiddenException("Refresh token non valido.");

        var now = _time.GetUtcNow();

        // Token gia' revocato → possibile riuso/compromissione: revoca tutte le sessioni admin.
        if (session.RevokedAt is not null)
        {
            await RevokeAllSessionsAsync(session.AdminUserId, cancellationToken);
            throw new AdminForbiddenException("Refresh token non valido.");
        }

        if (now >= session.ExpiresAt)
            throw new AdminForbiddenException("Refresh token scaduto.");

        if (!session.AdminUser.IsActive)
            throw new AdminForbiddenException("Account admin disabilitato.");

        // Rotazione: revoca il vecchio e emetti un nuovo refresh token.
        session.RevokedAt = now;
        return await IssueTokensAsync(session.AdminUser, client, cancellationToken);
    }

    public async Task LogoutAsync(AdminLogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var hash = Hash(request.RefreshToken);
        var session = await _db.AdminSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null)
            return;

        session.RevokedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(session.AdminUserId, "Admin.Logout", "AdminSession", session.Id.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task<AdminUserDto> GetMeAsync(Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var admin = await _db.AdminUsers
            .Include(u => u.Roles).ThenInclude(r => r.AdminRole)
            .FirstOrDefaultAsync(u => u.Id == adminUserId, cancellationToken)
            ?? throw new AdminNotFoundException("Admin non trovato.");

        return ToDto(admin);
    }

    // --- helpers ------------------------------------------------------------

    private async Task<AdminAuthResponse> IssueTokensAsync(AdminUser admin, AdminClientInfo? client, CancellationToken ct)
    {
        var roles = admin.Roles.Select(r => r.AdminRole.Name).ToList();
        var access = _jwt.Issue(admin, roles);

        var (raw, hash) = GenerateRefreshToken();
        var now = _time.GetUtcNow();
        var refreshExpires = now.AddDays(_jwtOpt.RefreshTokenExpiryDays);

        _db.AdminSessions.Add(new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = admin.Id,
            RefreshTokenHash = hash,
            CreatedAt = now,
            ExpiresAt = refreshExpires,
            IpAddress = Truncate(client?.IpAddress, 64),
            UserAgent = Truncate(client?.UserAgent, 500)
        });

        await _db.SaveChangesAsync(ct);

        return new AdminAuthResponse(access.Token, access.ExpiresAt, raw, refreshExpires, ToDto(admin));
    }

    private async Task RevokeAllSessionsAsync(Guid adminUserId, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var sessions = await _db.AdminSessions
            .Where(s => s.AdminUserId == adminUserId && s.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var s in sessions) s.RevokedAt = now;
        if (sessions.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private static AdminUserDto ToDto(AdminUser admin)
        => new(admin.Id, admin.Email, admin.DisplayName,
            admin.Roles.Select(r => r.AdminRole.Name).ToList());

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var raw = Base64UrlEncode(buffer);
        return (raw, Hash(raw));
    }

    private static string Hash(string raw)
    {
        Span<byte> dest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), dest);
        return Convert.ToHexString(dest).ToLowerInvariant();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
