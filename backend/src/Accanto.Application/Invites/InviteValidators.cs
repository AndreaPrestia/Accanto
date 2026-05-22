using Accanto.Domain.Enums;
using FluentValidation;

namespace Accanto.Application.Invites;

public class CreateInviteRequestValidator : AbstractValidator<CreateInviteRequest>
{
    public CreateInviteRequestValidator()
    {
        RuleFor(x => x.Role)
            .Must(r => r == CareCircleRole.Caregiver || r == CareCircleRole.Viewer)
            .WithMessage("Si possono invitare solo Caregiver o In ascolto.");
        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 90)
            .When(x => x.ExpiresInDays.HasValue)
            .WithMessage("La scadenza deve essere tra 1 e 90 giorni.");
        RuleFor(x => x.MaxUses)
            .InclusiveBetween(1, 50)
            .When(x => x.MaxUses.HasValue)
            .WithMessage("Il numero massimo di usi deve essere tra 1 e 50.");
    }
}
