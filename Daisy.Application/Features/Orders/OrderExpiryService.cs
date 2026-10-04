using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daisy.Application.Features.Orders;

/// <summary>
/// UC-72. Cancels VietQR orders that were not paid before PaymentExpiresAt + grace period
/// and releases their stock (BR-41). Called every minute by UnpaidOrderExpiryJob.
/// </summary>
public sealed class OrderExpiryService
{
    private readonly IApplicationDbContext _db;
    private readonly OrderService _orders;
    private readonly TimeProvider _time;
    private readonly PaymentDeadlineOptions _deadline;
    private readonly ILogger<OrderExpiryService> _logger;

    public OrderExpiryService(
        IApplicationDbContext db,
        OrderService orders,
        TimeProvider time,
        IOptions<PaymentDeadlineOptions> deadline,
        ILogger<OrderExpiryService> logger)
    {
        _db = db;
        _orders = orders;
        _time = time;
        _deadline = deadline.Value;
        _logger = logger;
    }

    /// <returns>The number of orders canceled.</returns>
    public async Task<int> CancelExpiredOrdersAsync(CancellationToken ct)
    {
        var cutoff = _time.GetVietnamNow() - _deadline.ExpiryGrace;
        var ids = await _db.Orders.AsNoTracking()
            .Where(o => o.PaymentMethod == PaymentMethod.Online
                && o.PaymentStatus == PaymentStatus.Unpaid
                && o.OrderStatus == OrderStatus.Pending
                && o.PaymentExpiresAt != null
                && o.PaymentExpiresAt <= cutoff)
            .OrderBy(o => o.PaymentExpiresAt)
            .Select(o => o.OrderId)
            .Take(200)
            .ToListAsync(ct);

        var canceled = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await _orders.TryCancelAsync(id, Order.ExpiredCancelReason, expiredOnly: true, ct))
                {
                    canceled++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing order is rolled back and retried on the next run (UC-72 A2).
                _logger.LogError(ex, "Expiring unpaid order {OrderId} failed.", id);
            }
        }

        if (canceled > 0)
        {
            _logger.LogInformation("Canceled {Count} unpaid VietQR order(s) past their payment deadline.", canceled);
        }

        return canceled;
    }
}
