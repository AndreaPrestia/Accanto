using FluentValidation;

namespace Accanto.Application.DoctorQuestions;

public class CreateDoctorQuestionRequestValidator : AbstractValidator<CreateDoctorQuestionRequest>
{
    public CreateDoctorQuestionRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().WithMessage("La domanda è obbligatoria.").MaximumLength(2000);
    }
}

public class UpdateDoctorQuestionRequestValidator : AbstractValidator<UpdateDoctorQuestionRequest>
{
    public UpdateDoctorQuestionRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().WithMessage("La domanda è obbligatoria.").MaximumLength(2000);
        RuleFor(x => x.AnswerNotes).MaximumLength(5000);
    }
}
