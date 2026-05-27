using FluentValidation;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Ai;

public class TimelineSummaryRequestValidator : AbstractValidator<TimelineSummaryRequest>
{
    public TimelineSummaryRequestValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 90).WithMessage("days must be between 1 and 90.");
    }
}

public class DoctorQuestionDraftRequestValidator : AbstractValidator<DoctorQuestionDraftRequest>
{
    public DoctorQuestionDraftRequestValidator(IOptions<AiOptions> options)
    {
        var o = options.Value;
        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Argomento richiesto.")
            .MaximumLength(o.MaxTopicLength);
        RuleFor(x => x.Notes!)
            .MaximumLength(o.MaxNotesLength)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class RephraseRequestValidator : AbstractValidator<RephraseRequest>
{
    public RephraseRequestValidator(IOptions<AiOptions> options)
    {
        var o = options.Value;
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Testo richiesto.")
            .MaximumLength(o.MaxRephraseTextLength);
        RuleFor(x => x.Tone!)
            .MaximumLength(o.MaxRephraseToneLength)
            .When(x => !string.IsNullOrEmpty(x.Tone));
    }
}

public class CheckInReflectionRequestValidator : AbstractValidator<CheckInReflectionRequest>
{
    public CheckInReflectionRequestValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 90).WithMessage("days must be between 1 and 90.");
    }
}
