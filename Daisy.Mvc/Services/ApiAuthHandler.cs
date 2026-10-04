using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Daisy.Mvc.Services;

/// <summary>
/// Adds "Authorization: Bearer" for signed-in users or "X-Cart-Session" for guests (API_DESIGN §2.5),
/// and refreshes an expired access token once, as in UC-04, before retrying the request.
/// </summary>
public sealed class ApiAuthHandler : DelegatingHandler
{
    public const string RawClientName = "DaisyApiRaw";

    private const string CorrelationIdKey = "X-Correlation-Id";

    private readonly IHttpContextAccessor _accessor;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ApiAuthHandler> _logger;

    public ApiAuthHandler(IHttpContextAccessor accessor, IHttpClientFactory clients, ILogger<ApiAuthHandler> logger)
    {
        _accessor = accessor;
        _clients = clients;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _accessor.HttpContext;
        if (context is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Buffer the body so the request can be sent again after a token refresh.
        byte[]? body = null;
        if (request.Content is not null and not MultipartFormDataContent)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        var signedIn = context.User.Identity?.IsAuthenticated == true;
        await AttachAsync(context, request, signedIn);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || !signedIn || request.Content is MultipartFormDataContent)
        {
            return response;
        }

        if (!await TryRefreshAsync(context, cancellationToken))
        {
            return response;
        }

        response.Dispose();
        using var retry = Clone(request, body);
        await AttachAsync(context, retry, signedIn: true);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task AttachAsync(HttpContext context, HttpRequestMessage request, bool signedIn)
    {
        // One id per browser request, so its API calls can be found in the API log (API_DESIGN §2.5).
        if (context.Items[CorrelationIdKey] is not string correlationId)
        {
            correlationId = Guid.NewGuid().ToString();
            context.Items[CorrelationIdKey] = correlationId;
        }

        request.Headers.Remove(CorrelationIdKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdKey, correlationId);

        request.Headers.Authorization = null;
        request.Headers.Remove("X-Cart-Session");
        if (signedIn && await AuthSession.GetTokenAsync(context, AuthSession.AccessTokenName) is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (AuthSession.CartSessionId(context) is { Length: > 0 } session)
        {
            request.Headers.TryAddWithoutValidation("X-Cart-Session", session);
        }
    }

    private async Task<bool> TryRefreshAsync(HttpContext context, CancellationToken ct)
    {
        var refreshToken = await AuthSession.GetTokenAsync(context, AuthSession.RefreshTokenName);
        if (string.IsNullOrEmpty(refreshToken))
        {
            await AuthSession.SignOutAsync(context);
            return false;
        }

        var raw = _clients.CreateClient(RawClientName);
        using var response = await raw.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, ApiClient.Json, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Refreshing the access token failed with {Status}; signing out.", (int)response.StatusCode);
            await AuthSession.SignOutAsync(context);
            return false;
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResult>(ApiClient.Json, ct);
        if (auth is null)
        {
            return false;
        }

        var current = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var remember = current.Properties?.IsPersistent ?? false;
        if (!context.Response.HasStarted)
        {
            await AuthSession.SignInAsync(context, auth, remember);
        }
        else
        {
            context.Items[AuthSession.AccessTokenName] = auth.AccessToken;
            context.Items[AuthSession.RefreshTokenName] = auth.RefreshToken;
        }

        return true;
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original, byte[]? body)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in original.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
