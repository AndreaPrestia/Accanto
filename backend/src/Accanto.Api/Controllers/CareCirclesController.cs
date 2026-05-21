using Accanto.Api.Common;
using Accanto.Application.CareCircles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles")]
public class CareCirclesController : ControllerBase
{
    private readonly ICareCircleService _svc;
    private readonly ICurrentUser _currentUser;

    public CareCirclesController(ICareCircleService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CareCircleDto>>> Mine(CancellationToken ct)
        => Ok(await _svc.GetMineAsync(_currentUser.RequireUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CareCircleDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _svc.GetByIdAsync(_currentUser.RequireUserId(), id, ct));

    [HttpPost]
    public async Task<ActionResult<CareCircleDto>> Create([FromBody] CreateCareCircleRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(_currentUser.RequireUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CareCircleDto>> Update(Guid id, [FromBody] UpdateCareCircleRequest request, CancellationToken ct)
        => Ok(await _svc.UpdateAsync(_currentUser.RequireUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _svc.ArchiveAsync(_currentUser.RequireUserId(), id, ct);
        return NoContent();
    }
}
