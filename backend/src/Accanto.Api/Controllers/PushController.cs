using Accanto.Api.Common;
using Accanto.Application.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly IPushService _push;
    private readonly ICurrentUser _currentUser;

    public PushController(IPushService push, ICurrentUser currentUser)
    {
        _push = push;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpGet("vapid-public-key")]
    public ActionResult<VapidPublicKeyDto> GetVapidPublicKey()
    {
        var key = _push.GetVapidPublicKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }
        return Ok(new VapidPublicKeyDto(key));
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
        {
            return BadRequest();
        }
        await _push.SubscribeAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest();
        }
        await _push.UnsubscribeAsync(_currentUser.RequireUserId(), request.Endpoint, ct);
        return NoContent();
    }
}
