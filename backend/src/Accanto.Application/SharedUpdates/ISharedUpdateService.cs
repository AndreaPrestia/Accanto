namespace Accanto.Application.SharedUpdates;

public interface ISharedUpdateService
{
    Task<IReadOnlyList<SharedUpdateDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default);
    Task<SharedUpdateDto> GetAsync(Guid userId, Guid careCircleId, Guid updateId, CancellationToken cancellationToken = default);
    Task<SharedUpdateDto> CreateAsync(Guid userId, Guid careCircleId, CreateSharedUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid careCircleId, Guid updateId, CancellationToken cancellationToken = default);
}

public interface ISharedUpdateTemplateProvider
{
    IReadOnlyList<SharedUpdateTemplateDto> GetTemplates();
}
