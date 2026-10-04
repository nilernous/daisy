using System.Text.RegularExpressions;
using Daisy.Application.Common;
using Daisy.Application.Features.Cart;
using Daisy.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Infrastructure;

/// <summary>
/// Base for REST controllers. Controllers only receive requests, call Application services
/// and return responses (ARCHITECTURE §22); errors are handled by ExceptionMiddleware.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract partial class ApiControllerBase : ControllerBase
{
    public const string CartSessionHeader = "X-Cart-Session";

    [GeneratedRegex("^[A-Za-z0-9-]{8,100}$")]
    private static partial Regex SessionIdRegex();

    /// <summary>The signed-in user's id. Owner is always taken from the token (BR-08).</summary>
    protected int CurrentUserId =>
        CurrentUserService.GetUserId(User) ?? throw new UnauthorizedException(ErrorCodes.Unauthorized, "Sign in to continue.");

    /// <summary>
    /// A valid Bearer token takes precedence; otherwise the guest cart in X-Cart-Session (API_DESIGN §3.4).
    /// A Bearer token that is invalid or expired gets 401, so the client refreshes it (UC-04) instead of
    /// being treated as a guest.
    /// </summary>
    protected CartOwner? ResolveCartOwner(bool required = true)
    {
        if (CurrentUserService.GetUserId(User) is int userId)
        {
            return CartOwner.ForUser(userId);
        }

        if (Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException(ErrorCodes.Unauthorized, "Sign in to continue.");
        }

        var sessionId = GuestSessionId;
        if (sessionId is not null)
        {
            return CartOwner.ForSession(sessionId);
        }

        return required
            ? throw new BadRequestException(ErrorCodes.CartOwnerRequired, $"Sign in or send the {CartSessionHeader} header.")
            : null;
    }

    protected string? GuestSessionId
    {
        get
        {
            var value = Request.Headers[CartSessionHeader].ToString().Trim();
            return SessionIdRegex().IsMatch(value) ? value : null;
        }
    }
}
