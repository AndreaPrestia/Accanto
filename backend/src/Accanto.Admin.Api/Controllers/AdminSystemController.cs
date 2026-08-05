using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Application.Stats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Api.Controllers;

/// <summary>
/// Health/status tecnico del control plane. Espone SOLO stato operativo
/// (Healthy/Degraded/Down), mai payload o contenuti. Nessun dato sensibile.
/// </summary>
[ApiController]
[Route("api/admin/system")]
[Authorize]
public class AdminSystemController : ControllerBase
{
    private readonly IAccantoAdminDbContext _adminDb;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public AdminSystemController(IAccantoAdminDbContext adminDb, IHttpClientFactory httpFactory, IConfiguration config)
    {
        _adminDb = adminDb;
        _httpFactory = httpFactory;
        _config = config;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var adminDb = await ProbeAdminDbAsync(ct);
        var publicInternal = await ProbePublicInternalAsync(ct);

        var payload = new
        {
            adminApi = "Healthy",
            adminDb = adminDb,
            publicApiInternal = publicInternal,
            checkedAt = DateTimeOffset.UtcNow
        };

        var allHealthy = adminDb == "Healthy" && publicInternal == "Healthy";
        return allHealthy ? Ok(payload) : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats([FromServices] IAdminStatsService svc, CancellationToken ct)
        => Ok(await svc.GetStatsAsync(ct));

    private async Task<string> ProbeAdminDbAsync(CancellationToken ct)
    {
        try
        {
            var ok = await _adminDb.AdminUsers.AsNoTracking().AnyAsync(ct);
            return ok || true ? "Healthy" : "Degraded";
        }
        catch
        {
            return "Down";
        }
    }

    private async Task<string> ProbePublicInternalAsync(CancellationToken ct)
    {
        var baseUrl = _config["InternalApp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return "Unknown";
        try
        {
            var client = _httpFactory.CreateClient();
            using var resp = await client.GetAsync($"{baseUrl.TrimEnd('/')}/health/live", ct);
            return resp.IsSuccessStatusCode ? "Healthy" : "Degraded";
        }
        catch
        {
            return "Down";
        }
    }
}
