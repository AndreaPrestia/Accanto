using FluentValidation;

namespace Accanto.Application.CareCircles;

public class CreateCareCircleRequestValidator : AbstractValidator<CreateCareCircleRequest>
{
    public CreateCareCircleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Il nome è obbligatorio.").MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateCareCircleRequestValidator : AbstractValidator<UpdateCareCircleRequest>
{
    public UpdateCareCircleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Il nome è obbligatorio.").MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
