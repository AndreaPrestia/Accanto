using Accanto.Api.Common;
using Accanto.Application.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _svc;
    private readonly ICurrentUser _currentUser;

    public AccountController(IAccountService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _svc.ChangePasswordAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }
}
