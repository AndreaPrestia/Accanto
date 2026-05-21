using FluentValidation;

namespace Accanto.Application.SharedUpdates;

public class CreateSharedUpdateRequestValidator : AbstractValidator<CreateSharedUpdateRequest>
{
    public CreateSharedUpdateRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Il testo è obbligatorio.").MaximumLength(4000);
    }
}
