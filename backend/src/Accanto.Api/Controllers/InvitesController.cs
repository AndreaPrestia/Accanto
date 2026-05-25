using Accanto.Api.Common;
using Accanto.Application.Invites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Accanto.Api.Controllers;

[ApiController]
[Route("api")]
public class InvitesController : ControllerBase
{
    private readonly IInviteService _svc;
    private readonly ICurrentUser _currentUser;

    public InvitesController(IInviteService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    // --- gestione inviti per un cerchio (Owner only, applicato nel service) ---

    [HttpPost("care-circles/{circleId:guid}/invites")]
    [Authorize]
    [EnableRateLimiting("invite-create")]
    public async Task<ActionResult<InviteDto>> Create(Guid circleId, [FromBody] CreateInviteRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(_currentUser.RequireUserId(), circleId, request, ct);
        return Ok(dto);
    }

    [HttpGet("care-circles/{circleId:guid}/invites")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<InviteDto>>> List(Guid circleId, CancellationToken ct)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), circleId, ct));

    [HttpDelete("care-circles/{circleId:guid}/invites/{inviteId:guid}")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid circleId, Guid inviteId, CancellationToken ct)
    {
        await _svc.RevokeAsync(_currentUser.RequireUserId(), circleId, inviteId, ct);
        return NoContent();
    }

    // --- uso dell'invito da parte di chi è stato invitato ---

    /// <summary>
    /// Anteprima dell'invito: pubblica (non richiede login) per consentire al destinatario
    /// di sapere a cosa sta per dire sì prima di registrarsi o fare login.
    /// </summary>
    [HttpGet("invites/{token}/preview")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string token, CancellationToken ct)
        => Ok(await _svc.PreviewAsync(token, ct));

    /// <summary>
    /// Accetta l'invito e aggiunge l'utente corrente come membro del cerchio.
    /// Restituisce l'id del cerchio per consentire il redirect.
    /// </summary>
    [HttpPost("invites/{token}/accept")]
    [Authorize]
    public async Task<ActionResult<AcceptInviteResponse>> Accept(string token, CancellationToken ct)
    {
        var circleId = await _svc.AcceptAsync(_currentUser.RequireUserId(), token, ct);
        return Ok(new AcceptInviteResponse(circleId));
    }
}

public sealed record AcceptInviteResponse(Guid CareCircleId);
