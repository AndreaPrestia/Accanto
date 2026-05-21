using Accanto.Api.Common;
using Accanto.Application.SharedUpdates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{careCircleId:guid}/shared-updates")]
public class SharedUpdatesController : ControllerBase
{
    private readonly ISharedUpdateService _svc;
    private readonly ICurrentUser _currentUser;

    public SharedUpdatesController(ISharedUpdateService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SharedUpdateDto>>> List(Guid careCircleId, CancellationToken ct)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), careCircleId, ct));

    [HttpGet("{updateId:guid}")]
    public async Task<ActionResult<SharedUpdateDto>> Get(Guid careCircleId, Guid updateId, CancellationToken ct)
        => Ok(await _svc.GetAsync(_currentUser.RequireUserId(), careCircleId, updateId, ct));

    [HttpPost]
    public async Task<ActionResult<SharedUpdateDto>> Create(Guid careCircleId, [FromBody] CreateSharedUpdateRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(_currentUser.RequireUserId(), careCircleId, request, ct);
        return CreatedAtAction(nameof(Get), new { careCircleId, updateId = dto.Id }, dto);
    }

    [HttpDelete("{updateId:guid}")]
    public async Task<IActionResult> Delete(Guid careCircleId, Guid updateId, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), careCircleId, updateId, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/shared-update-templates")]
public class SharedUpdateTemplatesController : ControllerBase
{
    private readonly ISharedUpdateTemplateProvider _provider;
    public SharedUpdateTemplatesController(ISharedUpdateTemplateProvider provider) { _provider = provider; }

    [HttpGet]
    public ActionResult<IReadOnlyList<SharedUpdateTemplateDto>> Get() => Ok(_provider.GetTemplates());
}
