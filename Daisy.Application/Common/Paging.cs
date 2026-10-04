using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Common;

/// <summary>Page-based pagination for REST list endpoints (API_DESIGN §2.4).</summary>
public record PageRequest
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    public static void ValidatePage(this PageRequest page)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();
        if (page.Page < 1)
        {
            errors.Add(new("page", "Page must be at least 1."));
        }

        if (page.PageSize is < 1 or > PageRequest.MaxPageSize)
        {
            errors.Add(new("pageSize", $"Page size must be between 1 and {PageRequest.MaxPageSize}."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
