using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Security;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IAccantoDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenService _refresh;
    private readonly ITwoFactorService _twoFactor;
    private readonly ISecurityAuditLog _audit;
    private readonly LockoutOptions _lockout;
    private readonly TwoFactorOptions _tfOpt;
    private readonly TimeProvider _time;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IAccantoDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        ITwoFactorService twoFactor,
        ISecurityAuditLog audit,
        IOptions<LockoutOptions> lockout,
        IOptions<TwoFactorOptions> twoFactorOptions,
        TimeProvider time,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
        _twoFactor = twoFactor;
        _audit = audit;
        _lockout = lockout.Value;
        _tfOpt = twoFactorOptions.Value;
        _time = time;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var result = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new AppValidationException(
                "Dati non validi.",
                result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Esiste già un account con questa email.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(user.Id, SecurityAuditEventType.AccountRegistered, client: client, cancellationToken: cancellationToken);
        return await BuildResponseAsync(user, client, cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var result = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new AppValidationException(
                "Dati non validi.",
                result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            // Niente account → niente lockout da tracciare, ma stesso messaggio per non leakare l'esistenza.
            await _audit.LogAsync(null, SecurityAuditEventType.LoginFailed, "Email sconosciuta", email, client, cancellationToken);
            throw new ForbiddenException("Email o password non corretti.");
        }

        var now = _time.GetUtcNow();

        if (user.LockoutEndsAt is { } lockedUntil && lockedUntil > now)
        {
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
            await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginLocked, $"Account bloccato fino a {lockedUntil:O}", email, client, cancellationToken);
            throw new ForbiddenException(
                $"Account temporaneamente bloccato per troppi tentativi. Riprova tra {minutes} minuti.");
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            // Reset del contatore se l'ultimo tentativo è fuori dalla finestra.
            if (user.LastFailedLoginAt is { } lastFail &&
                (now - lastFail).TotalMinutes > _lockout.AttemptWindowMinutes)
            {
                user.FailedLoginAttempts = 0;
            }

            user.FailedLoginAttempts += 1;
            user.LastFailedLoginAt = now;

            if (_lockout.MaxFailedAttempts > 0 &&
                user.FailedLoginAttempts >= _lockout.MaxFailedAttempts)
            {
                user.LockoutEndsAt = now.AddMinutes(_lockout.LockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);
                await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginLocked, $"Lockout dopo {user.FailedLoginAttempts} tentativi", email, client, cancellationToken);
                throw new ForbiddenException(
                    $"Account temporaneamente bloccato per troppi tentativi. Riprova tra {_lockout.LockoutMinutes} minuti.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginFailed, "Password errata", email, client, cancellationToken);
            throw new ForbiddenException("Email o password non corretti.");
        }

        // Login riuscito → reset.
        if (user.FailedLoginAttempts != 0 || user.LockoutEndsAt is not null || user.LastFailedLoginAt is not null)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEndsAt = null;
            user.LastFailedLoginAt = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Se il 2FA è attivo, la login si ferma qui: ritorna un challenge token al client.
        if (user.TwoFactorEnabled)
        {
            var challenge = _jwt.IssueTwoFactorChallenge(user.Id, TimeSpan.FromMinutes(Math.Max(1, _tfOpt.ChallengeLifetimeMinutes)));
            await _audit.LogAsync(user.Id, SecurityAuditEventType.TwoFactorChallengeIssued, client: client, cancellationToken: cancellationToken);
            return new LoginResult(true, challenge.Token, challenge.ExpiresAt, null);
        }

        var auth = await BuildResponseAsync(user, client, cancellationToken);
        await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginSuccess, client: client, cancellationToken: cancellationToken);
        return new LoginResult(false, null, null, auth);
    }

    public async Task<AuthResponse> CompleteTwoFactorAsync(TwoFactorLoginRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var userId = _jwt.ValidateTwoFactorChallenge(request.TwoFactorToken)
            ?? throw new ForbiddenException("Challenge 2FA non valido o scaduto.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new ForbiddenException("Challenge 2FA non valido o scaduto.");

        if (!user.TwoFactorEnabled)
            throw new ForbiddenException("Challenge 2FA non valido o scaduto.");

        var ok = false;
        var usedRecovery = false;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            ok = await _twoFactor.VerifyUserCodeAsync(user.Id, request.Code!, cancellationToken);
        }
        if (!ok && !string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            ok = await _twoFactor.ConsumeRecoveryCodeAsync(user.Id, request.RecoveryCode!, cancellationToken);
            if (ok) usedRecovery = true;
        }

        if (!ok)
        {
            await _audit.LogAsync(user.Id, SecurityAuditEventType.TwoFactorFailed, client: client, cancellationToken: cancellationToken);
            throw new ForbiddenException("Codice 2FA non valido.");
        }

        if (usedRecovery)
            await _audit.LogAsync(user.Id, SecurityAuditEventType.RecoveryCodeUsed, client: client, cancellationToken: cancellationToken);
        await _audit.LogAsync(user.Id, SecurityAuditEventType.TwoFactorSuccess, client: client, cancellationToken: cancellationToken);
        await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginSuccess, client: client, cancellationToken: cancellationToken);

        return await BuildResponseAsync(user, client, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var (issued, userId) = await _refresh.RotateAsync(request.RefreshToken, client, cancellationToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new ForbiddenException("Refresh token non valido.");

        var access = _jwt.Issue(user);
        return new AuthResponse(access.Token, access.ExpiresAt, issued.Token, issued.ExpiresAt, ToDto(user));
    }

    public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
        => _refresh.RevokeAsync(request.RefreshToken, cancellationToken);

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");
        return ToDto(user);
    }

    private async Task<AuthResponse> BuildResponseAsync(User user, ClientInfo? client, CancellationToken cancellationToken)
    {
        var access = _jwt.Issue(user);
        var refresh = await _refresh.IssueAsync(user.Id, client, cancellationToken);
        return new AuthResponse(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Language, u.CreatedAt);
}
