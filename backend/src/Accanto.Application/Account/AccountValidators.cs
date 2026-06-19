using FluentValidation;

namespace Accanto.Application.Account;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Inserisci la password attuale.")
            .MaximumLength(200);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Inserisci la nuova password.")
            .MinimumLength(8).WithMessage("La nuova password deve avere almeno 8 caratteri.")
            .MaximumLength(200);

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("La nuova password deve essere diversa da quella attuale.");
    }
}

public class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Inserisci la password per confermare.")
            .MaximumLength(200);

        RuleFor(x => x.Confirmation)
            .NotEmpty().WithMessage("Conferma l'operazione digitando ERASE.")
            .Must(c => string.Equals(c, "ERASE", StringComparison.Ordinal))
            .WithMessage("Conferma non valida: digita esattamente ERASE.");

        // TwoFactorCode e' opzionale qui (puo' essere TOTP o recovery
        // code); la presenza-quando-richiesta e' verificata dal
        // service in base allo stato 2FA dell'utente.
        RuleFor(x => x.TwoFactorCode)
            .MaximumLength(40)
            .When(x => !string.IsNullOrEmpty(x.TwoFactorCode));
    }
}
