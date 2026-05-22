using Accanto.Api.Common;
using Accanto.Application.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{circleId:guid}/export")]
public class CareCircleExportController : ControllerBase
{
    private readonly ICareCircleExportService _svc;
    private readonly ICurrentUser _currentUser;

    public CareCircleExportController(ICareCircleExportService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(
        Guid circleId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var result = await _svc.ExportPdfAsync(_currentUser.RequireUserId(), circleId, from, to, ct);
        return File(result.Bytes, "application/pdf", result.FileName);
    }
}
