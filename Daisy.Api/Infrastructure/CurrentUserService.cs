using System.Security.Claims;
using Daisy.Application.Interfaces;
using Daisy.Domain.Enums;

namespace Daisy.Api.Infrastructure;

/// <summary>Reads the user from the JWT claims "sub" and "role" (API_DESIGN §3.1).</summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int? UserId => GetUserId(_accessor.HttpContext?.User);

    public UserRole? Role => Enum.TryParse<UserRole>(_accessor.HttpContext?.User.FindFirstValue("role"), out var role) ? role : null;

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public static int? GetUserId(ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirstValue("sub"), out var id) ? id : null;
}
