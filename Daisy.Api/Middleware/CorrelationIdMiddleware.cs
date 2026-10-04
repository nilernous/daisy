using System.Text.RegularExpressions;

namespace Daisy.Api.Middleware;

/// <summary>
/// Optional X-Correlation-Id sent by Daisy.Mvc (API_DESIGN §2.5): added to the log scope so MVC and API
/// logs can be matched, and echoed back in the response. Values that are not a short id are ignored.
/// </summary>
public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex IdRegex();

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[HeaderName].ToString();
        if (!IdRegex().IsMatch(id))
        {
            await _next(context);
            return;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(context);
        }
    }
}
