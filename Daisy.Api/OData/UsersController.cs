using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Daisy.Api.OData;

/// <summary>GET /odata/users — UC-63.</summary>
[Authorize(Policy = Policies.Admin)]
public sealed class UsersController : ODataController
{
    private readonly UserService _users;

    public UsersController(UserService users)
    {
        _users = users;
    }

    [DaisyEnableQuery]
    public IQueryable<UserListDto> Get() => _users.QueryAdminList();
}
