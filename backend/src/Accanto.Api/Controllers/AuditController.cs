using Accanto.Api.Common;
using Accanto.Application.Audit;
using Accanto.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{careCircleId:guid}/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _svc;
    private readonly ICurrentUser _currentUser;

    public AuditController(IAuditService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> List(
        Guid careCircleId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), careCircleId, skip, take, ct));
}
