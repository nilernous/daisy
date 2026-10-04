using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Daisy.Api.Controllers;

/// <summary>UC-01 – UC-07.</summary>
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _auth.RegisterAsync(request, ct));

    /// <summary>Send X-Cart-Session to merge the guest cart (UC-31).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken ct) =>
        await _auth.LoginAsync(request, GuestSessionId, ct);

    [HttpPost("logout")]
    [Authorize(Policy = Policies.Authenticated)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshRequest request, CancellationToken ct) =>
        await _auth.RefreshAsync(request, ct);

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Password)]
    public async Task<ActionResult<MessageDto>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct) =>
        await _auth.ForgotPasswordAsync(request, ct);

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Password)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize(Policy = Policies.Authenticated)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(CurrentUserId, request, ct);
        return NoContent();
    }
}
