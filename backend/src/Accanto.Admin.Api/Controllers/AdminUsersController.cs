using Accanto.Admin.Api.Common;
using Accanto.Admin.Application.Auth;
using Accanto.Admin.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Admin.Api.Controllers;

/// <summary>
/// Endpoint admin per metadata utente e operazioni account. Richiedono Admin JWT.
/// Le operazioni mutative (disable/enable/revoke/deletion) richiedono reason
/// obbligatoria e ruolo Owner/Operator (enforced nel service); SecurityAuditor
/// ha accesso in sola lettura ai metadata. Nessun contenuto utente esposto.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserOperationsService _ops;
    private readonly ICurrentAdmin _current;

    public AdminUsersController(IAdminUserOperationsService ops, ICurrentAdmin current)
    {
        _ops = ops;
        _current = current;
    }

    [HttpGet]
    public async Task<ActionResult<AdminUserListResponse>> List(
        [FromQuery] string? q,
        [FromQuery] bool? disabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _ops.ListAsync(q, disabled, page, pageSize, ct));

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserMetadataDto>> Get(Guid userId, CancellationToken ct)
        => Ok(await _ops.GetAsync(userId, ct));

    [HttpPost("{userId:guid}/disable")]
    public async Task<ActionResult<AdminOperationResultDto>> Disable(Guid userId, [FromBody] AdminUserOperationRequest request, CancellationToken ct)
        => Ok(await _ops.DisableAsync(BuildContext(), userId, request, ct));

    [HttpPost("{userId:guid}/enable")]
    public async Task<ActionResult<AdminOperationResultDto>> Enable(Guid userId, [FromBody] AdminUserOperationRequest request, CancellationToken ct)
        => Ok(await _ops.EnableAsync(BuildContext(), userId, request, ct));

    [HttpPost("{userId:guid}/revoke-sessions")]
    public async Task<ActionResult<AdminOperationResultDto>> RevokeSessions(Guid userId, [FromBody] AdminUserOperationRequest request, CancellationToken ct)
        => Ok(await _ops.RevokeSessionsAsync(BuildContext(), userId, request, ct));

    [HttpPost("{userId:guid}/deletion-requests")]
    public async Task<ActionResult<AdminOperationResultDto>> StartDeletion(Guid userId, [FromBody] AdminUserOperationRequest request, CancellationToken ct)
        => Ok(await _ops.StartDeletionAsync(BuildContext(), userId, request, ct));

    private AdminOperationContext BuildContext()
    {
        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var client = new AdminClientInfo(string.IsNullOrWhiteSpace(ua) ? null : ua, ip);
        return new AdminOperationContext(_current.RequireAdminUserId(), _current.Roles, client);
    }
}
