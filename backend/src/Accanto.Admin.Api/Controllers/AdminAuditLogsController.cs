using Accanto.Admin.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Admin.Api.Controllers;

/// <summary>
/// Lettura dell'audit log admin. Richiede Admin JWT. Espone SOLO metadata
/// tecnici: mai body, mai contenuti utente. In v0.1 l'accesso e' consentito a
/// tutti i ruoli admin autenticati (Owner/Operator/SecurityAuditor) — la
/// SecurityAuditor ha qui il suo scopo di sola lettura.
/// </summary>
[ApiController]
[Route("api/admin/audit-logs")]
[Authorize]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IAdminAuditQueryService _audit;

    public AdminAuditLogsController(IAdminAuditQueryService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<AdminAuditLogListResponse>> List(
        [FromQuery] Guid? adminUserId,
        [FromQuery] string? action,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _audit.ListAsync(adminUserId, action, targetType, targetId, from, to, page, pageSize, ct));
}
