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
    /// <summary>
    /// Restituisce i modelli pronti per la lingua richiesta. <paramref name="acceptLanguage"/> è
    /// l'header HTTP Accept-Language grezzo (può essere null/vuoto). Fallback a italiano se la
    /// lingua non è supportata.
    /// </summary>
    IReadOnlyList<SharedUpdateTemplateDto> GetTemplates(string? acceptLanguage);
}
