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
    }
}
