using System.Net;
using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Daisy.Mvc.Infrastructure;

/// <summary>Configuration section "Api".</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Base URL of Daisy.Api, e.g. https://localhost:7001.</summary>
    public string BaseUrl { get; set; } = "https://localhost:7001";
}

/// <summary>Turns API image paths (/uploads/...) into absolute URLs served by Daisy.Api.</summary>
public sealed class MediaUrls
{
    private readonly string _base;

    public MediaUrls(Microsoft.Extensions.Options.IOptions<ApiOptions> options)
    {
        _base = options.Value.BaseUrl.TrimEnd('/');
    }

    public string? For(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null
        : path.StartsWith('/') ? _base + path
        : path;
}

/// <summary>Gives every visitor a guest cart id, sent to the API as X-Cart-Session (API_DESIGN §3.4).</summary>
public sealed class CartSessionMiddleware
{
    private readonly RequestDelegate _next;

    public CartSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(context.Request.Cookies[AuthSession.CartCookie]))
        {
            var id = Guid.NewGuid().ToString("N");
            context.Items[AuthSession.CartCookie] = id;
            context.Response.Cookies.Append(AuthSession.CartCookie, id, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(30),
            });
        }

        return _next(context);
    }
}

/// <summary>
/// Global handling of API errors that a page cannot fix itself: 401 → sign in again,
/// 403 → access denied page, 404 → not found page.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException api)
        {
            return;
        }

        var request = context.HttpContext.Request;
        switch (api.Status)
        {
            case HttpStatusCode.Unauthorized:
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "", returnUrl = request.Path + request.QueryString });
                context.ExceptionHandled = true;
                break;
            case HttpStatusCode.Forbidden:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
                context.ExceptionHandled = true;
                break;
            case HttpStatusCode.NotFound:
                context.Result = new ViewResult { ViewName = "~/Views/Shared/NotFound.cshtml", StatusCode = 404 };
                context.ExceptionHandled = true;
                break;
        }
    }
}

/// <summary>Base for MVC controllers: flash messages and API error display.</summary>
public abstract class WebController : Controller
{
    public const string FlashKey = "flash";
    public const string FlashToneKey = "flashTone";

    protected void Flash(string message, string tone = "good")
    {
        TempData[FlashKey] = message;
        TempData[FlashToneKey] = tone;
    }

    /// <summary>Shows an API error on the form: field errors next to fields, the code's message on top.</summary>
    protected void AddApiError(ApiException ex, string? prefix = null)
    {
        foreach (var (field, messages) in ex.FieldErrors)
        {
            var key = string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
            foreach (var message in messages)
            {
                ModelState.AddModelError(key, Translate(message));
            }
        }

        ModelState.AddModelError(string.Empty, ErrorMessages.For(ex));
    }

    /// <summary>Validation messages come from the API in English; the form fields already carry Vietnamese hints.</summary>
    private static string Translate(string message) =>
        message.Contains("must not be empty", StringComparison.OrdinalIgnoreCase) || message.Contains("is required", StringComparison.OrdinalIgnoreCase)
            ? "Không được để trống."
            : message.Contains("email", StringComparison.OrdinalIgnoreCase) ? "Email không hợp lệ."
            : message.Contains("Phone", StringComparison.Ordinal) ? "Số điện thoại không hợp lệ (ví dụ 0901234567)."
            : message.Contains("Password", StringComparison.Ordinal) ? "Mật khẩu cần 8–100 ký tự, có ít nhất một chữ và một số."
            : message.Contains("do not match", StringComparison.OrdinalIgnoreCase) ? "Mật khẩu nhập lại không khớp."
            : message.Contains("Slug", StringComparison.OrdinalIgnoreCase) ? "Slug chỉ gồm chữ thường không dấu, số và dấu gạch ngang."
            : "Giá trị không hợp lệ.";

    protected IActionResult RedirectToLocal(string? returnUrl, string fallbackAction = "Index", string fallbackController = "Home") =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(fallbackAction, fallbackController, new { area = "" });
}

public static class FlashExtensions
{
    public static (string? Message, string Tone) ReadFlash(this ITempDataDictionary tempData) =>
        (tempData[WebController.FlashKey] as string, tempData[WebController.FlashToneKey] as string ?? "good");
}
