using Accanto.Admin.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Admin.Api.Controllers;

/// <summary>Elenco delle operazioni admin richieste (tracking del ciclo di vita).</summary>
[ApiController]
[Route("api/admin/operations")]
[Authorize]
public class AdminOperationsController : ControllerBase
{
    private readonly IAdminUserOperationsService _ops;

    public AdminOperationsController(IAdminUserOperationsService ops)
    {
        _ops = ops;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOperationListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _ops.ListOperationsAsync(page, pageSize, ct));

    [HttpGet("{operationId:guid}")]
    public async Task<ActionResult<AdminOperationDto>> Get(Guid operationId, CancellationToken ct)
        => Ok(await _ops.GetOperationAsync(operationId, ct));
}
