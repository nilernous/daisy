using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Dashboard;

/// <summary>Dashboard figures as defined in BR-40 (UC-67 – UC-70).</summary>
public sealed class DashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;

    public DashboardService(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>UC-67. Sum of TotalAmount of Completed orders by day / month of CompletedAt.</summary>
    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueAsync(DashboardQuery query, CancellationToken ct)
    {
        var (from, to, byMonth) = Resolve(query, withGroupBy: true);
        var (start, end) = Bounds(from, to);

        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderStatus == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end)
            .Select(o => new { Date = o.CompletedAt!.Value.Date, o.TotalAmount })
            .GroupBy(o => o.Date)
            .Select(g => new { g.Key, Revenue = g.Sum(x => x.TotalAmount), Count = g.Count() })
            .ToListAsync(ct);

        return Periods(from, to, byMonth)
            .Select(p =>
            {
                var inPeriod = rows.Where(r => InPeriod(DateOnly.FromDateTime(r.Key), p, byMonth)).ToList();
                return new RevenuePointDto(Label(p, byMonth), inPeriod.Sum(r => r.Revenue), inPeriod.Sum(r => r.Count));
            })
            .ToList();
    }

    /// <summary>UC-68. Every status is present.</summary>
    public async Task<IReadOnlyList<OrderStatusCountDto>> GetOrdersByStatusAsync(DashboardQuery query, CancellationToken ct)
    {
        var (from, to, _) = Resolve(query, withGroupBy: false);
        var (start, end) = Bounds(from, to);

        var counts = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Enum.GetValues<OrderStatus>()
            .Select(s => new OrderStatusCountDto(s, counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0))
            .ToList();
    }

    /// <summary>UC-69. Quantity of items in non-canceled orders, highest first.</summary>
    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DashboardQuery query, CancellationToken ct)
    {
        var (from, to, _) = Resolve(query, withGroupBy: false);
        var top = query.Top ?? 10;
        if (top is < 1 or > 50)
        {
            throw new ValidationException([new ValidationFailure("top", "Top must be between 1 and 50.")]);
        }

        var (start, end) = Bounds(from, to);
        var rows = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.OrderStatus != OrderStatus.Canceled && i.Order.CreatedAt >= start && i.Order.CreatedAt < end)
            .GroupBy(i => i.Variant!.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .Take(top)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.ProductId))
            .Select(p => new
            {
                p.ProductId,
                p.ProductName,
                Thumbnail = p.Images.OrderByDescending(i => i.IsThumbnail).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            })
            .ToDictionaryAsync(p => p.ProductId, ct);

        return rows
            .Select(r => new TopProductDto(r.ProductId, products[r.ProductId].ProductName, products[r.ProductId].Thumbnail, r.Quantity, r.Revenue))
            .ToList();
    }

    /// <summary>UC-70. Customer accounts by day / month of CreatedAt.</summary>
    public async Task<IReadOnlyList<NewCustomerPointDto>> GetNewCustomersAsync(DashboardQuery query, CancellationToken ct)
    {
        var (from, to, byMonth) = Resolve(query, withGroupBy: true);
        var (start, end) = Bounds(from, to);

        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Customer && u.CreatedAt >= start && u.CreatedAt < end)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Periods(from, to, byMonth)
            .Select(p => new NewCustomerPointDto(
                Label(p, byMonth),
                rows.Where(r => InPeriod(DateOnly.FromDateTime(r.Key), p, byMonth)).Sum(r => r.Count)))
            .ToList();
    }

    private (DateOnly From, DateOnly To, bool ByMonth) Resolve(DashboardQuery query, bool withGroupBy)
    {
        var today = _time.GetVietnamToday();
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-29);
        var errors = new List<ValidationFailure>();

        if (from > to)
        {
            errors.Add(new("from", "From must be on or before To."));
        }

        var groupBy = string.IsNullOrWhiteSpace(query.GroupBy) ? "day" : query.GroupBy.Trim().ToLowerInvariant();
        var byMonth = groupBy == "month";
        if (withGroupBy && groupBy is not ("day" or "month"))
        {
            errors.Add(new("groupBy", "Group by must be day or month."));
        }
        else if (withGroupBy && !byMonth && to.DayNumber - from.DayNumber > 365)
        {
            errors.Add(new("to", "A daily range can be at most 366 days."));
        }
        else if (to.DayNumber - from.DayNumber > 5 * 366)
        {
            errors.Add(new("to", "The range can be at most 5 years."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (from, to, byMonth);
    }

    private static (DateTime Start, DateTime End) Bounds(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue));

    private static IEnumerable<DateOnly> Periods(DateOnly from, DateOnly to, bool byMonth)
    {
        if (!byMonth)
        {
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                yield return d;
            }

            yield break;
        }

        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            yield return m;
        }
    }

    private static bool InPeriod(DateOnly date, DateOnly period, bool byMonth) =>
        byMonth ? date.Year == period.Year && date.Month == period.Month : date == period;

    private static string Label(DateOnly period, bool byMonth) =>
        byMonth ? period.ToString("yyyy-MM") : period.ToString("yyyy-MM-dd");
}
