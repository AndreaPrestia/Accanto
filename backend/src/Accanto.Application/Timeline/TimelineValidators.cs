using FluentValidation;

namespace Accanto.Application.Timeline;

public class CreateTimelineEntryRequestValidator : AbstractValidator<CreateTimelineEntryRequest>
{
    public CreateTimelineEntryRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Il titolo è obbligatorio.").MaximumLength(200);
        RuleFor(x => x.Content).NotNull().MaximumLength(10000).WithMessage("Il testo è troppo lungo.");
        RuleFor(x => x.Tags).NotNull();
        RuleForEach(x => x.Tags).MaximumLength(40);
    }
}

public class UpdateTimelineEntryRequestValidator : AbstractValidator<UpdateTimelineEntryRequest>
{
    public UpdateTimelineEntryRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Il titolo è obbligatorio.").MaximumLength(200);
        RuleFor(x => x.Content).NotNull().MaximumLength(10000);
        RuleFor(x => x.Tags).NotNull();
        RuleForEach(x => x.Tags).MaximumLength(40);
    }
}
