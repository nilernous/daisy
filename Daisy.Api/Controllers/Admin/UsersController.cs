using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-64 – UC-66 (Admin only). The user list is GET /odata/users (UC-63).</summary>
[Route("api/admin/users")]
[Authorize(Policy = Policies.Admin)]
public sealed class UsersController : ApiControllerBase
{
    private readonly UserService _users;

    public UsersController(UserService users)
    {
        _users = users;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailDto>> GetUserDetail(
        int id,
        [FromQuery] int ordersPage = 1,
        [FromQuery] int ordersPageSize = 10,
        CancellationToken ct = default) =>
        await _users.GetDetailAsync(id, new PageRequest { Page = ordersPage, PageSize = ordersPageSize }, ct);

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetUserStatus(int id, SetUserStatusRequest request, CancellationToken ct)
    {
        await _users.SetStatusAsync(CurrentUserId, id, request, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> SetUserRole(int id, SetUserRoleRequest request, CancellationToken ct)
    {
        await _users.SetRoleAsync(CurrentUserId, id, request, ct);
        return NoContent();
    }
}
