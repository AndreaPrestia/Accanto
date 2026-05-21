using Accanto.Domain.Enums;

namespace Accanto.Application.DoctorQuestions;

public sealed record CreateDoctorQuestionRequest(string Question, DoctorQuestionCategory Category);

public sealed record UpdateDoctorQuestionRequest(
    string Question,
    DoctorQuestionCategory Category,
    DoctorQuestionStatus Status,
    string? AnswerNotes
);

public sealed record DoctorQuestionDto(
    Guid Id,
    Guid CareCircleId,
    Guid CreatedByUserId,
    string Question,
    DoctorQuestionCategory Category,
    DoctorQuestionStatus Status,
    string? AnswerNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public sealed record DoctorQuestionTemplateDto(
    DoctorQuestionCategory Category,
    string CategoryLabel,
    IReadOnlyList<string> Questions
);
