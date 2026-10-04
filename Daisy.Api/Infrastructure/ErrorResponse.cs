using System.Text.Json;
using Daisy.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Daisy.Api.Infrastructure;

/// <summary>The error body of every failed request (API_DESIGN §4.1).</summary>
public sealed record ErrorResponse(
    int Status,
    string Code,
    string Message,
    IDictionary<string, string[]>? Errors,
    object? Details,
    string TraceId)
{
    public static ErrorResponse Create(HttpContext context, int status, string code, string message, IDictionary<string, string[]>? errors = null, object? details = null) =>
        new(status, code, message, errors, details, context.TraceIdentifier);

    public static Task WriteAsync(HttpContext context, int status, string code, string message, IDictionary<string, string[]>? errors = null, object? details = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = Create(context, status, code, message, errors, details);
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, ApiJson.Options));
    }

    /// <summary>Converts "Items[0].Quantity" or "$.addressId" to "items[0].quantity".</summary>
    public static string ToCamelCaseKey(string key)
    {
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        return string.Join('.', trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    /// <summary>Used for [ApiController] automatic 400 responses (malformed JSON, wrong types).</summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { ValidationState: ModelValidationState.Invalid })
            .GroupBy(e => string.IsNullOrEmpty(e.Key) ? "body" : ToCamelCaseKey(e.Key))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(e => e.Value!.Errors)
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                    .Distinct()
                    .ToArray());

        var body = Create(context.HttpContext, 400, ErrorCodes.ValidationFailed, "One or more fields are invalid.", errors);
        return new BadRequestObjectResult(body);
    }
}
