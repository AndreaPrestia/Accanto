namespace Accanto.Admin.Application.Stats;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
