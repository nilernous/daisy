using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-01 – UC-07. Tokens live only in the encrypted auth cookie.</summary>
[Route("account")]
public sealed class AccountController(AuthApiService auth, ILogger<AccountController> logger) : WebController
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["Title"] = "Đăng nhập";
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    /// <summary>The guest cart is merged into the account by the API (UC-31).</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        ViewData["Title"] = "Đăng nhập";
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            var result = await auth.LoginAsync(form.Email, form.Password);
            await AuthSession.SignInAsync(HttpContext, result, form.Remember);
            var isStaff = result.User.Role is Models.Api.UserRole.Staff or Models.Api.UserRole.Admin;
            return isStaff && string.IsNullOrEmpty(form.ReturnUrl)
                ? Redirect("/admin")
                : RedirectToLocal(form.ReturnUrl);
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.TooManyRequests)
        {
            ModelState.AddModelError(string.Empty, ErrorMessages.For(ex));
            return View(form);
        }
    }

    [HttpGet("register")]
    public IActionResult Register(string? returnUrl)
    {
        ViewData["Title"] = "Tạo tài khoản";
        return View(new RegisterForm { ReturnUrl = returnUrl });
    }

    /// <summary>UC-01, then signs in directly so the customer can keep shopping.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        ViewData["Title"] = "Tạo tài khoản";
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await auth.RegisterAsync(form.FullName.Trim(), form.Email.Trim(), form.Password, form.ConfirmPassword);
            var result = await auth.LoginAsync(form.Email.Trim(), form.Password);
            await AuthSession.SignInAsync(HttpContext, result, remember: true);
            Flash($"Chào mừng {result.User.FullName} đến với Daisy.");
            return RedirectToLocal(form.ReturnUrl);
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.TooManyRequests)
        {
            AddApiError(ex);
            return View(form);
        }
    }

    /// <summary>UC-03.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            try
            {
                await auth.LogoutAsync();
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException)
            {
                // The stored refresh token expires on its own (API_DESIGN §6.1).
                logger.LogInformation(ex, "API logout failed; clearing the cookie anyway.");
            }
        }

        await AuthSession.SignOutAsync(HttpContext);
        return Redirect("/");
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Quên mật khẩu";
        return View(new ForgotPasswordForm());
    }

    /// <summary>UC-05. The same message whether or not the email exists (BR-05).</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordForm form)
    {
        ViewData["Title"] = "Quên mật khẩu";
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await auth.ForgotPasswordAsync(form.Email.Trim());
            ViewData["Sent"] = true;
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.BadRequest)
        {
            ModelState.AddModelError(string.Empty, ErrorMessages.For(ex));
        }

        return View(form);
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? token)
    {
        ViewData["Title"] = "Đặt mật khẩu mới";
        return View(new ResetPasswordForm { Token = token ?? string.Empty });
    }

    /// <summary>UC-06.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm form)
    {
        ViewData["Title"] = "Đặt mật khẩu mới";
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await auth.ResetPasswordAsync(form.Token, form.NewPassword, form.ConfirmPassword);
            Flash("Đã đặt mật khẩu mới. Hãy đăng nhập lại.");
            return RedirectToAction(nameof(Login));
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.TooManyRequests)
        {
            AddApiError(ex);
            return View(form);
        }
    }

    [HttpGet("change-password")]
    [Authorize]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Đổi mật khẩu";
        return View(new ChangePasswordForm());
    }

    /// <summary>UC-07. The API revokes the session, so the user is signed in again with the new password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form)
    {
        ViewData["Title"] = "Đổi mật khẩu";
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await auth.ChangePasswordAsync(form.CurrentPassword, form.NewPassword, form.ConfirmPassword);
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            var result = await auth.LoginAsync(email, form.NewPassword);
            await AuthSession.SignInAsync(HttpContext, result, remember: true);
            Flash("Đã đổi mật khẩu.");
            return User.IsStaffOrAdmin() ? Redirect("/admin") : Redirect("/profile");
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest)
        {
            AddApiError(ex);
            return View(form);
        }
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Không có quyền truy cập";
        return View();
    }
}
