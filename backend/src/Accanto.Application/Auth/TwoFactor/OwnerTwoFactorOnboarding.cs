using Accanto.Application.Common.Persistence;
using Accanto.Application.Email;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Auth.TwoFactor;

/// <summary>
/// Logica condivisa di "promozione a Owner": setta il timer di grace 2FA
/// se l'utente non ne ha gia' uno attivo, e invia l'email informativa.
/// Best-effort: errori di mail loggati e ignorati.
/// </summary>
public interface IOwnerTwoFactorOnboarding
{
    Task OnPromotedToOwnerAsync(Guid userId, string circleName, CancellationToken cancellationToken = default);
}

public sealed class OwnerTwoFactorOnboarding : IOwnerTwoFactorOnboarding
{
    private readonly IAccantoDbContext _db;
    private readonly ICircleEmailNotifier _email;
    private readonly TwoFactorOptions _opt;
    private readonly ILogger<OwnerTwoFactorOnboarding> _logger;

    public OwnerTwoFactorOnboarding(
        IAccantoDbContext db,
        ICircleEmailNotifier email,
        IOptions<TwoFactorOptions> opt,
        ILogger<OwnerTwoFactorOnboarding> logger)
    {
        _db = db;
        _email = email;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task OnPromotedToOwnerAsync(Guid userId, string circleName, CancellationToken cancellationToken = default)
    {
        if (!_opt.RequireForOwners) return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return;

        // Se 2FA gia' attiva o gia' c'e' una deadline -> niente da fare.
        if (user.TwoFactorEnabled || user.TwoFactorRequiredFromUtc is not null) return;

        var deadline = DateTimeOffset.UtcNow.AddHours(_opt.OwnerGraceHours);
        user.TwoFactorRequiredFromUtc = deadline;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Race con un altro path di promozione: leggi il valore esistente.
            _logger.LogDebug(ex, "Concorrenza nel set TwoFactorRequiredFromUtc per utente {User}", userId);
            return;
        }

        // Notifica email di sicurezza (bypassa preferenze topic).
        _ = _email.SendSecurityEmailAsync(userId, "2FA obbligatorio per ruolo Owner",
            EmailTemplates.TwoFactorRequiredForOwner(circleName, deadline), CancellationToken.None);
    }
}
