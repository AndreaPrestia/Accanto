using System.Security.Claims;

namespace Accanto.Admin.Api.Common;

public interface ICurrentAdmin
{
    Guid? AdminUserId { get; }
    Guid RequireAdminUserId();
}

public class CurrentAdmin : ICurrentAdmin
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentAdmin(IHttpContextAccessor accessor) { _accessor = accessor; }

    public Guid? AdminUserId
    {
        get
        {
            var sub = _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _accessor.HttpContext?.User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid RequireAdminUserId() => AdminUserId ?? throw new UnauthorizedAccessException("Admin non autenticato.");
}
