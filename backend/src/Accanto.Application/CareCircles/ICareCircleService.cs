namespace Accanto.Application.CareCircles;

public interface ICareCircleService
{
    Task<IReadOnlyList<CareCircleDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CareCircleDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<CareCircleDto> CreateAsync(Guid userId, CreateCareCircleRequest request, CancellationToken cancellationToken = default);
    Task<CareCircleDto> UpdateAsync(Guid userId, Guid id, UpdateCareCircleRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
