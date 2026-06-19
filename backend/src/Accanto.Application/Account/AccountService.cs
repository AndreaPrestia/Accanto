using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Accanto.Application.Email;
using Accanto.Application.Security;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Account;

public class AccountService : IAccountService
{
    private readonly IAccantoDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IFileStorage _storage;
    private readonly ICircleEmailNotifier _email;
    private readonly IRefreshTokenService _refresh;
    private readonly ISecurityAuditLog _audit;
    private readonly ITwoFactorService _twoFactor;
    private readonly IUserErasureService _erasure;
    private readonly IValidator<ChangePasswordRequest> _changeValidator;
    private readonly IValidator<DeleteAccountRequest> _deleteValidator;

    public AccountService(
        IAccantoDbContext db,
        IPasswordHasher hasher,
        IFileStorage storage,
        ICircleEmailNotifier email,
        IRefreshTokenService refresh,
        ISecurityAuditLog audit,
        ITwoFactorService twoFactor,
        IUserErasureService erasure,
        IValidator<ChangePasswordRequest> changeValidator,
        IValidator<DeleteAccountRequest> deleteValidator)
    {
        _db = db;
        _hasher = hasher;
        _storage = storage;
        _email = email;
        _refresh = refresh;
        _audit = audit;
        _twoFactor = twoFactor;
        _erasure = erasure;
        _changeValidator = changeValidator;
        _deleteValidator = deleteValidator;
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        var v = await _changeValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ForbiddenException("La password attuale non è corretta.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        // Sblocca eventuale lockout: l'utente ha appena dimostrato di conoscere la password.
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        user.LastFailedLoginAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        // Sicurezza: invalida tutte le altre sessioni attive (refresh token) per impedire che
        // chi conosce la vecchia password mantenga l'accesso tramite un refresh token rubato.
        await _refresh.RevokeAllForUserAsync(userId, cancellationToken);

        await _audit.LogAsync(userId, SecurityAuditEventType.PasswordChanged, client: client, cancellationToken: cancellationToken);
        await _audit.LogAsync(userId, SecurityAuditEventType.AllSessionsRevoked, "In seguito a cambio password", client: client, cancellationToken: cancellationToken);

        _ = _email.SendSecurityEmailAsync(userId, "Password modificata", EmailTemplates.PasswordChanged(), CancellationToken.None);
    }

    public async Task DeleteAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        var v = await _deleteValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (user.IsErased)
            throw new ForbiddenException("Account gia' cancellato.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ForbiddenException("La password non è corretta.");

        // Se 2FA e' attivo: il client deve fornire un codice TOTP
        // valido o un recovery code. Senza un secondo fattore non
        // possiamo cancellare l'account in modo sicuro (rischio
        // session-hijack -> erasure malevolo).
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                throw new AppValidationException(
                    "Codice di autenticazione richiesto.",
                    new Dictionary<string, string[]> { ["TwoFactorCode"] = new[] { "Inserisci un codice TOTP o un codice di recupero." } });
            }

            var ok = await _twoFactor.VerifyUserCodeAsync(userId, request.TwoFactorCode!, cancellationToken);
            if (!ok)
            {
                ok = await _twoFactor.ConsumeRecoveryCodeAsync(userId, request.TwoFactorCode!, cancellationToken);
            }
            if (!ok) throw new ForbiddenException("Codice di autenticazione non valido.");
        }

        // Delegate al servizio GDPR: tombstone + cascade + outbox S3.
        await _erasure.EraseAsync(userId, "Cancellazione richiesta dall'utente", cancellationToken);
    }

    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "it", "en", "es" };

    public async Task UpdateLanguageAsync(Guid userId, UpdateLanguageRequest request, CancellationToken cancellationToken = default)
    {
        var lang = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim().ToLowerInvariant();
        if (lang is not null && !SupportedLanguages.Contains(lang))
        {
            throw new AppValidationException(
                "Lingua non supportata.",
                new Dictionary<string, string[]> { ["Language"] = new[] { "Lingue ammesse: it, en, es." } });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        user.Language = lang;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AppValidationException ToValidation(FluentValidation.Results.ValidationResult v) =>
        new("Dati non validi.",
            v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
