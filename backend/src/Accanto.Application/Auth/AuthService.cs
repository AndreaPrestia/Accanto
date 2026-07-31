using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Email;
using Accanto.Application.Security;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

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
    private readonly PasswordResetOptions _pwdResetOpt;
    private readonly IEmailService _email;
    private readonly ILogger<AuthService> _logger;
    private readonly TimeProvider _time;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;

    public AuthService(
        IAccantoDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        ITwoFactorService twoFactor,
        ISecurityAuditLog audit,
        IOptions<LockoutOptions> lockout,
        IOptions<TwoFactorOptions> twoFactorOptions,
        IOptions<PasswordResetOptions> passwordResetOptions,
        IEmailService email,
        ILogger<AuthService> logger,
        TimeProvider time,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
        _twoFactor = twoFactor;
        _audit = audit;
        _lockout = lockout.Value;
        _tfOpt = twoFactorOptions.Value;
        _pwdResetOpt = passwordResetOptions.Value;
        _email = email;
        _logger = logger;
        _time = time;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
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

        // Account disabilitato dal control plane admin: accesso negato.
        // Messaggio generico per non rivelare lo stato amministrativo.
        if (user.IsDisabled)
        {
            await _audit.LogAsync(user.Id, SecurityAuditEventType.LoginFailed, "Account disabilitato", email, client, cancellationToken);
            throw new ForbiddenException("Account disabilitato. Contatta il supporto.");
        }

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

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var result = await _forgotPasswordValidator.ValidateAsync(request, cancellationToken);
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

        // Anti-enumerazione: se l'utente non esiste o e' stato cancellato (tombstone GDPR),
        // logghiamo l'evento ma rispondiamo identico al caso "esiste". Cosi' il chiamante
        // non puo' distinguere se un'email e' registrata o meno.
        if (user is null || user.IsErased)
        {
            await _audit.LogAsync(null, SecurityAuditEventType.PasswordResetRequested, "Email sconosciuta", email, client, cancellationToken);
            return;
        }

        // Genera token cripto-sicuro (32 bytes Base64Url ~ 43 caratteri).
        var rawToken = GenerateUrlSafeToken();
        var tokenHash = HashToken(rawToken);
        var now = _time.GetUtcNow();
        var lifetimeMinutes = Math.Max(5, _pwdResetOpt.TokenLifetimeMinutes);

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(lifetimeMinutes),
            IpAddress = client?.IpAddress,
            UserAgent = client?.UserAgent,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var link = BuildResetLink(rawToken);
        var subject = "Reimposta la tua password Accanto";
        var html = BuildResetEmailHtml(user.DisplayName, link, lifetimeMinutes);

        try
        {
            await _email.SendAsync(user.Email, user.DisplayName, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            // L'EmailService gia' cattura le eccezioni internamente; questo e' un safety net
            // ulteriore per non rompere il flusso di richiesta reset se il sender ha bug.
            _logger.LogWarning(ex, "Errore inatteso durante l'invio email di reset password a {UserId}", user.Id);
        }

        await _audit.LogAsync(user.Id, SecurityAuditEventType.PasswordResetRequested, client: client, cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var result = await _resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new AppValidationException(
                "Dati non validi.",
                result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var tokenHash = HashToken(request.Token);
        var now = _time.GetUtcNow();

        var entry = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (entry is null || entry.UsedAt is not null || entry.ExpiresAt <= now)
        {
            throw new ForbiddenException("Token non valido o scaduto.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, cancellationToken);
        if (user is null || user.IsErased)
        {
            throw new ForbiddenException("Token non valido o scaduto.");
        }

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        // Reset stato lockout: il legittimo proprietario ha provato il reset.
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        user.LastFailedLoginAt = null;

        entry.UsedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // Revoca tutte le sessioni attive: chi possedeva la vecchia password e' fuori.
        await _refresh.RevokeAllForUserAsync(user.Id, cancellationToken);

        await _audit.LogAsync(user.Id, SecurityAuditEventType.PasswordResetCompleted, client: client, cancellationToken: cancellationToken);
    }

    private static string GenerateUrlSafeToken()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        return Base64UrlEncode(buf);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }

    private string BuildResetLink(string token)
    {
        var baseUrl = (_pwdResetOpt.PublicUrl ?? string.Empty).TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(_pwdResetOpt.ResetPath) ? "/reset-password" : _pwdResetOpt.ResetPath;
        if (!path.StartsWith('/')) path = "/" + path;
        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }

    private static string BuildResetEmailHtml(string displayName, string link, int lifetimeMinutes)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(displayName);
        var safeLink = System.Net.WebUtility.HtmlEncode(link);
        return $$"""
            <!DOCTYPE html>
            <html>
              <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #2b2b2b; max-width: 540px; margin: 0 auto; padding: 24px;">
                <p>Ciao {{safeName}},</p>
                <p>Hai richiesto di reimpostare la password del tuo account Accanto. Clicca il link qui sotto per scegliere una nuova password:</p>
                <p style="margin: 24px 0;">
                  <a href="{{safeLink}}" style="display: inline-block; padding: 12px 18px; background: #2f6f4f; color: #fff; text-decoration: none; border-radius: 6px;">
                    Reimposta password
                  </a>
                </p>
                <p style="font-size: 13px; color: #555;">
                  Il link scade tra <strong>{{lifetimeMinutes}} minuti</strong>. Se non hai richiesto tu il reset, puoi ignorare questa email: il tuo account resta sicuro.
                </p>
                <p style="font-size: 12px; color: #888; margin-top: 32px; word-break: break-all;">
                  Se il pulsante non funziona, copia e incolla questo URL nel browser:<br/>
                  {{safeLink}}
                </p>
              </body>
            </html>
            """;
    }

    private async Task<AuthResponse> BuildResponseAsync(User user, ClientInfo? client, CancellationToken cancellationToken)
    {
        var access = _jwt.Issue(user);
        var refresh = await _refresh.IssueAsync(user.Id, client, cancellationToken);
        return new AuthResponse(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Language, u.CreatedAt);
}
