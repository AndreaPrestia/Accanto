using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class DoctorQuestion
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DoctorQuestionCategory Category { get; set; }
    public DoctorQuestionStatus Status { get; set; } = DoctorQuestionStatus.ToAsk;
    public string? AnswerNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
