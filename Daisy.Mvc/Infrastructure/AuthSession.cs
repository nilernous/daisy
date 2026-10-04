using System.Security.Claims;
using Daisy.Mvc.Models.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Daisy.Mvc.Infrastructure;

/// <summary>
/// The signed-in session of Daisy.Mvc: an encrypted auth cookie that holds the user's claims and the
/// API access and refresh tokens (API_DESIGN §3.1). The browser never sees the tokens in JavaScript.
/// </summary>
public static class AuthSession
{
    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string CartCookie = "daisy.cart";

    public static async Task SignInAsync(HttpContext context, AuthResult auth, bool remember)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, auth.User.Id.ToString()),
                new Claim(ClaimTypes.Name, auth.User.FullName),
                new Claim(ClaimTypes.Email, auth.User.Email),
                new Claim(ClaimTypes.Role, auth.User.Role.ToString()),
                new Claim("avatar", auth.User.AvatarUrl ?? string.Empty),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = auth.RefreshTokenExpiresAt,
            AllowRefresh = false,
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AccessTokenName, Value = auth.AccessToken },
            new AuthenticationToken { Name = RefreshTokenName, Value = auth.RefreshToken },
        ]);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
        context.User = new ClaimsPrincipal(identity);
        context.Items[AccessTokenName] = auth.AccessToken;
        context.Items[RefreshTokenName] = auth.RefreshToken;
    }

    public static async Task<string?> GetTokenAsync(HttpContext context, string name) =>
        context.Items.TryGetValue(name, out var value) && value is string fresh
            ? fresh
            : await context.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, name);

    public static Task SignOutAsync(HttpContext context)
    {
        context.Items.Remove(AccessTokenName);
        context.Items.Remove(RefreshTokenName);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        return context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static string? CartSessionId(HttpContext context) =>
        context.Items.TryGetValue(CartCookie, out var value) && value is string id ? id : context.Request.Cookies[CartCookie];

    public static int? UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsStaffOrAdmin(this ClaimsPrincipal user) => user.IsInRole("Staff") || user.IsInRole("Admin");
}
