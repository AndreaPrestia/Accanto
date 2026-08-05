using Accanto.Admin.Application.Users;

namespace Accanto.Admin.Application.Stats;

// Aggrega iterando l'internal client: nessun endpoint aggregato nel backend pubblico.
public class AdminStatsService : IAdminStatsService
{
    private const int PageSize = 100;
    private const int MaxPages = 200; // safety net ~20k utenti

    private readonly IInternalAppClient _app;

    public AdminStatsService(IInternalAppClient app)
    {
        _app = app;
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        long totalStorage = 0;
        int totalDocs = 0, totalTimeline = 0, disabled = 0, totalUsers = 0;

        for (var page = 1; page <= MaxPages; page++)
        {
            var res = await _app.ListUsersAsync(null, null, page, PageSize, cancellationToken);
            if (page == 1) totalUsers = res.Total;

            foreach (var u in res.Items)
            {
                totalStorage += u.StorageUsedBytes;
                totalDocs += u.DocumentsCount;
                totalTimeline += u.TimelineEntryCount;
                if (u.IsDisabled) disabled++;
            }

            if (res.Items.Count < PageSize || page * PageSize >= res.Total) break;
        }

        return new AdminStatsDto(totalUsers, disabled, totalStorage, totalDocs, totalTimeline);
    }
}
