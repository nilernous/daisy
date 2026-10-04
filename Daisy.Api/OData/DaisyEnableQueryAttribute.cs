using Daisy.Api.Infrastructure;
using Daisy.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData;

namespace Daisy.Api.OData;

/// <summary>
/// OData query settings of API_DESIGN §7: $filter, $orderby, $top, $skip, $count, $select;
/// no $expand; MaxTop 100; 20 items when $top is missing; invalid queries return the standard 400 body.
/// </summary>
public sealed class DaisyEnableQueryAttribute : EnableQueryAttribute
{
    public const int DefaultPageSize = 20;

    public DaisyEnableQueryAttribute()
    {
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
            | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select;
        AllowedFunctions = AllowedFunctions.Contains | AllowedFunctions.StartsWith | AllowedFunctions.EndsWith
            | AllowedFunctions.ToLower | AllowedFunctions.Any;
        AllowedLogicalOperators = AllowedLogicalOperators.All;
        AllowedArithmeticOperators = AllowedArithmeticOperators.None;
        MaxTop = 100;
        MaxNodeCount = 100;
        MaxAnyAllExpressionDepth = 1;
        MaxExpansionDepth = 0;
        HandleNullPropagation = HandleNullPropagationOption.False;
        EnsureStableOrdering = true;
    }

    public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
    {
        var settings = new ODataQuerySettings
        {
            HandleNullPropagation = HandleNullPropagationOption.False,
            EnsureStableOrdering = true,
            PageSize = queryOptions.Top is null ? DefaultPageSize : null,
            TimeZone = Application.Common.VietnamTime.TimeZone,
        };
        return queryOptions.ApplyTo(queryable, settings);
    }

    public override void OnActionExecuting(ActionExecutingContext actionExecutingContext)
    {
        base.OnActionExecuting(actionExecutingContext);
        actionExecutingContext.Result = ToStandardError(actionExecutingContext.HttpContext, actionExecutingContext.Result);
    }

    public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
    {
        base.OnActionExecuted(actionExecutedContext);
        actionExecutedContext.Result = ToStandardError(actionExecutedContext.HttpContext, actionExecutedContext.Result);
    }

    /// <summary>Replaces the OData library's 400 body with the standard error body (API_DESIGN §4.1).</summary>
    private static IActionResult? ToStandardError(HttpContext context, IActionResult? result)
    {
        if (result is not ObjectResult { StatusCode: 400 } objectResult || objectResult.Value is ErrorResponse)
        {
            return result;
        }

        var message = objectResult.Value switch
        {
            ODataError error => error.Message,
            SerializableError serializable when serializable.TryGetValue("Message", out var m) => m?.ToString(),
            string text => text,
            _ => null,
        };

        return new BadRequestObjectResult(ErrorResponse.Create(
            context,
            400,
            ErrorCodes.ValidationFailed,
            string.IsNullOrWhiteSpace(message) ? "The OData query is invalid." : message));
    }
}
