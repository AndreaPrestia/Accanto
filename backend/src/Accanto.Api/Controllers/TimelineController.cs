using Accanto.Api.Common;
using Accanto.Application.Timeline;
using Accanto.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{careCircleId:guid}/timeline")]
public class TimelineController : ControllerBase
{
    private readonly ITimelineService _svc;
    private readonly ICurrentUser _currentUser;

    public TimelineController(ITimelineService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimelineEntryDto>>> List(
        Guid careCircleId,
        [FromQuery] TimelineEntryType? type,
        [FromQuery] string? tag,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), careCircleId, new TimelineQuery(type, tag, from, to), ct));

    [HttpGet("{entryId:guid}")]
    public async Task<ActionResult<TimelineEntryDto>> Get(Guid careCircleId, Guid entryId, CancellationToken ct)
        => Ok(await _svc.GetAsync(_currentUser.RequireUserId(), careCircleId, entryId, ct));

    [HttpPost]
    public async Task<ActionResult<TimelineEntryDto>> Create(Guid careCircleId, [FromBody] CreateTimelineEntryRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(_currentUser.RequireUserId(), careCircleId, request, ct);
        return CreatedAtAction(nameof(Get), new { careCircleId, entryId = dto.Id }, dto);
    }

    [HttpPut("{entryId:guid}")]
    public async Task<ActionResult<TimelineEntryDto>> Update(Guid careCircleId, Guid entryId, [FromBody] UpdateTimelineEntryRequest request, CancellationToken ct)
        => Ok(await _svc.UpdateAsync(_currentUser.RequireUserId(), careCircleId, entryId, request, ct));

    [HttpDelete("{entryId:guid}")]
    public async Task<IActionResult> Delete(Guid careCircleId, Guid entryId, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), careCircleId, entryId, ct);
        return NoContent();
    }

    [HttpPatch("bulk")]
    public async Task<ActionResult<BulkUpdateResultDto>> BulkUpdate(Guid careCircleId, [FromBody] BulkUpdateTimelineEntriesRequest request, CancellationToken ct)
        => Ok(await _svc.BulkUpdateAsync(_currentUser.RequireUserId(), careCircleId, request, ct));
}
