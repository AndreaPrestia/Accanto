using Accanto.Application.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

/// <summary>
/// Endpoint INTERNI service-to-service per il control plane admin.
/// NON destinati ai browser: richiedono lo scheme di autenticazione dedicato
/// <c>InternalAdminScheme</c> (token service-to-service). I JWT pubblici e i
/// JWT admin frontend vengono rifiutati perche' issuer/audience/chiave sono diversi.
/// Espongono SOLO metadata utente e comandi account: MAI contenuti utente.
/// </summary>
[ApiController]
[Route("internal/admin/users")]
[Authorize(AuthenticationSchemes = InternalAdminScheme.Name)]
public class InternalAdminUsersController : ControllerBase
{
    private readonly IInternalUserMetadataService _metadata;
    private readonly IInternalAdminAccountService _account;

    public InternalAdminUsersController(IInternalUserMetadataService metadata, IInternalAdminAccountService account)
    {
        _metadata = metadata;
        _account = account;
    }

    [HttpGet]
    public async Task<ActionResult<InternalUserListResponse>> List(
        [FromQuery] string? q,
        [FromQuery] bool? disabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _metadata.ListAsync(q, disabled, page, pageSize, ct));

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<InternalUserMetadataDto>> Get(Guid userId, CancellationToken ct)
    {
        var dto = await _metadata.GetAsync(userId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{userId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid userId, [FromBody] InternalSetDisabledRequest request, CancellationToken ct)
    {
        await _account.DisableAsync(userId, request.Reason, ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/enable")]
    public async Task<IActionResult> Enable(Guid userId, [FromBody] InternalSetDisabledRequest request, CancellationToken ct)
    {
        await _account.EnableAsync(userId, request.Reason, ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken ct)
    {
        await _account.RevokeSessionsAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/deletion-requests")]
    public async Task<IActionResult> StartDeletion(Guid userId, [FromBody] InternalStartDeletionRequest request, CancellationToken ct)
    {
        await _account.StartDeletionAsync(userId, request.Reason, ct);
        return NoContent();
    }
}

/// <summary>Nome dello scheme di autenticazione service-to-service interno.</summary>
public static class InternalAdminScheme
{
    public const string Name = "InternalAdminScheme";
}
