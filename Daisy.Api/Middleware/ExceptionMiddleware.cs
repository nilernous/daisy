using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Api.Middleware;

/// <summary>
/// Converts exceptions into the standard error body (ARCHITECTURE §28, API_DESIGN §4.2).
/// Controllers do not repeat try/catch logic.
/// </summary>
public sealed class ExceptionMiddleware
{
    /// <summary>Domain rule codes that mean "the request can never succeed" rather than a state conflict.</summary>
    private static readonly HashSet<string> BadRequestDomainCodes =
    [
        ErrorCodes.InvalidSale,
        ErrorCodes.InvalidCategoryParent,
        ErrorCodes.UnsupportedFile,
        ErrorCodes.CannotModifySelf,
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDatabaseErrorClassifier dbErrors)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to send.
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await HandleAsync(context, ex, dbErrors);
        }
    }

    private Task HandleAsync(HttpContext context, Exception exception, IDatabaseErrorClassifier dbErrors)
    {
        switch (exception)
        {
            case AppException app:
                return ErrorResponse.WriteAsync(context, app.Status, app.Code, app.Message, details: app.Details);

            case DomainException domain:
                var status = BadRequestDomainCodes.Contains(domain.Code) ? 400 : 409;
                return ErrorResponse.WriteAsync(context, status, domain.Code, domain.Message, details: domain.Details);

            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => ErrorResponse.ToCamelCaseKey(string.IsNullOrEmpty(e.PropertyName) ? "body" : e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                return ErrorResponse.WriteAsync(context, 400, ErrorCodes.ValidationFailed, "One or more fields are invalid.", errors);

            case DbUpdateConcurrencyException:
                return ErrorResponse.WriteAsync(context, 409, ErrorCodes.ConcurrencyConflict, "The data changed meanwhile. Reload and try again.");

            case DbUpdateException db when dbErrors.IsUniqueConstraintViolation(db):
                return ErrorResponse.WriteAsync(context, 409, ErrorCodes.ConcurrencyConflict, "The data changed meanwhile. Reload and try again.");

            case BadHttpRequestException bad:
                return ErrorResponse.WriteAsync(context, bad.StatusCode, ErrorCodes.ValidationFailed, "The request could not be read.");

            default:
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                return ErrorResponse.WriteAsync(context, 500, ErrorCodes.InternalError, "Something went wrong. Please try again later.");
        }
    }
}
