namespace Accanto.Application.DoctorQuestions;

public interface IDoctorQuestionService
{
    Task<IReadOnlyList<DoctorQuestionDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default);
    Task<DoctorQuestionDto> CreateAsync(Guid userId, Guid careCircleId, CreateDoctorQuestionRequest request, CancellationToken cancellationToken = default);
    Task<DoctorQuestionDto> UpdateAsync(Guid userId, Guid careCircleId, Guid questionId, UpdateDoctorQuestionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid careCircleId, Guid questionId, CancellationToken cancellationToken = default);
}

public interface IDoctorQuestionTemplateProvider
{
    IReadOnlyList<DoctorQuestionTemplateDto> GetTemplates();
}
