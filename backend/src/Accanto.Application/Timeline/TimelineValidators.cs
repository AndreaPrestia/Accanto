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

public class BulkUpdateTimelineEntriesRequestValidator : AbstractValidator<BulkUpdateTimelineEntriesRequest>
{
    public BulkUpdateTimelineEntriesRequestValidator()
    {
        RuleFor(x => x.EntryIds).NotNull().WithMessage("Seleziona almeno una voce.");
        When(x => x.EntryIds is not null, () =>
        {
            RuleFor(x => x.EntryIds.Count).GreaterThan(0).WithMessage("Seleziona almeno una voce.");
            RuleFor(x => x.EntryIds.Count).LessThanOrEqualTo(100).WithMessage("Puoi aggiornare al massimo 100 voci per volta.");
        });
        RuleForEach(x => x.TagsToAdd).MaximumLength(40).When(x => x.TagsToAdd != null);
        RuleForEach(x => x.TagsToRemove).MaximumLength(40).When(x => x.TagsToRemove != null);
        RuleFor(x => x)
            .Must(x => (x.TagsToAdd is { Count: > 0 }) || (x.TagsToRemove is { Count: > 0 }) || x.NewVisibility.HasValue)
            .WithMessage("Specifica almeno una modifica (tag o visibilità).");
    }
}
