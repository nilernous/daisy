using Daisy.Application.Common;
using Daisy.Application.Features.Payments;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Orders;

public sealed class OrderService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;

    public OrderService(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    // ---- Customer (BR-08: only their own orders) --------------------------------------

    /// <summary>UC-37. Newest first.</summary>
    public async Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(int userId, OrderListQuery query, CancellationToken ct)
    {
        query.ValidatePage();
        var orders = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        if (query.Status is OrderStatus status)
        {
            orders = orders.Where(o => o.OrderStatus == status);
        }

        return await orders
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderId)
            .Select(o => new OrderSummaryDto(
                o.OrderId,
                o.OrderCode,
                o.CreatedAt,
                o.OrderStatus,
                o.PaymentMethod,
                o.PaymentStatus,
                o.TotalAmount,
                o.Items.Sum(i => i.Quantity),
                o.Items.OrderBy(i => i.OrderItemId).Select(i => i.ImageUrlSnapshot).FirstOrDefault()))
            .ToPagedResultAsync(query, ct);
    }

    /// <summary>UC-38 / UC-39.</summary>
    public async Task<OrderDetailDto> GetMyOrderAsync(int userId, int orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(o => o.OrderId == orderId && o.UserId == userId, ct);
        return ToDetail(order);
    }

    /// <summary>UC-40.</summary>
    public async Task<CanceledOrderDto> CancelMyOrderAsync(int userId, int orderId, CancelOrderRequest request, CancellationToken ct)
    {
        ValidateReason(request);
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");
        return await CancelAsync(order, request.Reason.Trim(), ct);
    }

    // ---- Staff -----------------------------------------------------------------------

    /// <summary>Query behind GET /odata/orders (UC-53).</summary>
    public IQueryable<OrderAdminListDto> QueryAdminList() =>
        _db.Orders.AsNoTracking().Select(o => new OrderAdminListDto
        {
            Id = o.OrderId,
            OrderCode = o.OrderCode,
            CustomerId = o.UserId,
            CustomerName = o.User!.FullName,
            CustomerEmail = o.User.Email,
            ReceiverPhone = o.ReceiverPhone,
            OrderStatus = o.OrderStatus,
            PaymentMethod = o.PaymentMethod,
            PaymentStatus = o.PaymentStatus,
            TotalAmount = o.TotalAmount,
            ItemCount = o.Items.Sum(i => i.Quantity),
            CreatedAt = o.CreatedAt,
            PaidAt = o.PaidAt,
        });

    /// <summary>UC-54.</summary>
    public async Task<OrderAdminDetailDto> GetAdminDetailAsync(int orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(o => o.OrderId == orderId, ct, includeUser: true);
        var detail = ToDetail(order);
        var user = order.User!;

        return new OrderAdminDetailDto
        {
            Id = detail.Id,
            OrderCode = detail.OrderCode,
            OrderStatus = detail.OrderStatus,
            PaymentMethod = detail.PaymentMethod,
            PaymentStatus = detail.PaymentStatus,
            ReceiverName = detail.ReceiverName,
            ReceiverPhone = detail.ReceiverPhone,
            ShippingAddress = detail.ShippingAddress,
            CustomerNote = detail.CustomerNote,
            Items = detail.Items,
            VoucherCode = detail.VoucherCode,
            SubtotalAmount = detail.SubtotalAmount,
            DiscountAmount = detail.DiscountAmount,
            ShippingFee = detail.ShippingFee,
            TotalAmount = detail.TotalAmount,
            CreatedAt = detail.CreatedAt,
            PaymentExpiresAt = detail.PaymentExpiresAt,
            PaidAt = detail.PaidAt,
            CompletedAt = detail.CompletedAt,
            CanceledAt = detail.CanceledAt,
            CancelReason = detail.CancelReason,
            CanCancel = detail.CanCancel,
            CanPayWithQr = detail.CanPayWithQr,
            Customer = new OrderCustomerDto(user.UserId, user.FullName, user.Email, user.Phone),
            TransactionCode = order.TransactionCode,
            AllowedStatusTransitions = order.AllowedNextStatuses(),
            AllowedPaymentActions = PaymentService.AllowedActions(order).Select(a => a.ToString()).ToList(),
        };
    }

    /// <summary>
    /// UC-55. One step forward only (BR-35). The update is conditional on the status the
    /// staff member saw, so concurrent changes cannot skip a step.
    /// </summary>
    public async Task UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
            ?? throw new NotFoundException("Order not found.");
        order.EnsureCanMoveTo(request.Status);

        var now = _time.GetVietnamNow();
        var current = order.OrderStatus;
        var next = request.Status;
        var query = _db.Orders.Where(o => o.OrderId == orderId && o.OrderStatus == current);
        if (next == OrderStatus.Confirmed && order.PaymentMethod == PaymentMethod.Online)
        {
            query = query.Where(o => o.PaymentStatus == PaymentStatus.Paid);
        }

        var updated = next == OrderStatus.Completed
            ? await query.ExecuteUpdateAsync(s => s
                .SetProperty(o => o.OrderStatus, next)
                .SetProperty(o => o.CompletedAt, now), ct)
            : await query.ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, next), ct);

        if (updated == 0)
        {
            throw new ConflictException(ErrorCodes.InvalidStatusTransition, "The order changed meanwhile. Reload it and try again.");
        }
    }

    /// <summary>UC-56.</summary>
    public async Task<CanceledOrderDto> CancelByStaffAsync(int orderId, CancelOrderRequest request, CancellationToken ct)
    {
        ValidateReason(request);
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
            ?? throw new NotFoundException("Order not found.");
        return await CancelAsync(order, request.Reason.Trim(), ct);
    }

    // ---- Cancellation core (BR-36) -------------------------------------------------------

    private async Task<CanceledOrderDto> CancelAsync(Order order, string reason, CancellationToken ct)
    {
        order.EnsureCancelable();
        var canceled = await TryCancelAsync(order.OrderId, reason, expiredOnly: false, ct);
        if (!canceled)
        {
            throw new ConflictException(ErrorCodes.OrderNotCancelable, "This order can no longer be canceled.");
        }

        var after = await _db.Orders.AsNoTracking().FirstAsync(o => o.OrderId == order.OrderId, ct);
        return new CanceledOrderDto(
            after.OrderId,
            after.OrderCode,
            after.OrderStatus,
            after.PaymentStatus,
            after.CanceledAt,
            RefundRequired: after.PaymentStatus == PaymentStatus.Paid);
    }

    /// <summary>
    /// Cancels the order and restores stock, SoldCount and voucher usage in one transaction.
    /// The status change is a conditional update, so two cancellations (customer, staff,
    /// expiry job) or a cancellation racing a webhook can never both succeed.
    /// </summary>
    /// <param name="expiredOnly">UC-72: only an Online, Unpaid, Pending order is canceled.</param>
    internal async Task<bool> TryCancelAsync(int orderId, string reason, bool expiredOnly, CancellationToken ct)
    {
        var now = _time.GetVietnamNow();
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var target = expiredOnly
            ? _db.Orders.Where(o => o.OrderId == orderId
                && o.OrderStatus == OrderStatus.Pending
                && o.PaymentStatus == PaymentStatus.Unpaid
                && o.PaymentMethod == PaymentMethod.Online)
            : _db.Orders.Where(o => o.OrderId == orderId
                && (o.OrderStatus == OrderStatus.Pending || o.OrderStatus == OrderStatus.Confirmed));

        var updated = await target.ExecuteUpdateAsync(s => s
            .SetProperty(o => o.OrderStatus, OrderStatus.Canceled)
            .SetProperty(o => o.CancelReason, reason)
            .SetProperty(o => o.CanceledAt, now), ct);

        if (updated == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => new { i.VariantId, i.Quantity, i.Variant!.ProductId })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var quantity = item.Quantity;
            await _db.ProductVariants
                .Where(v => v.VariantId == item.VariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantity), ct);
            await _db.Products
                .Where(p => p.ProductId == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount >= quantity ? p.SoldCount - quantity : 0), ct);
        }

        var voucherId = await _db.Orders.Where(o => o.OrderId == orderId).Select(o => o.VoucherId).FirstAsync(ct);
        if (voucherId is int id)
        {
            await _db.Vouchers
                .Where(v => v.VoucherId == id && v.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount - 1), ct);
        }

        await transaction.CommitAsync(ct);
        return true;
    }

    // ---- Helpers ---------------------------------------------------------------------

    private async Task<Order> LoadOrderAsync(System.Linq.Expressions.Expression<Func<Order, bool>> predicate, CancellationToken ct, bool includeUser = false)
    {
        var query = _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .Include(o => o.Items).ThenInclude(i => i.Review)
            .Include(o => o.Voucher)
            .AsSplitQuery();
        if (includeUser)
        {
            query = query.Include(o => o.User);
        }

        return await query.FirstOrDefaultAsync(predicate, ct) ?? throw new NotFoundException("Order not found.");
    }

    private OrderDetailDto ToDetail(Order order)
    {
        var now = _time.GetVietnamNow();
        var completed = order.OrderStatus == OrderStatus.Completed;
        return new OrderDetailDto
        {
            Id = order.OrderId,
            OrderCode = order.OrderCode,
            OrderStatus = order.OrderStatus,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            ReceiverName = order.ReceiverName,
            ReceiverPhone = order.ReceiverPhone,
            ShippingAddress = order.ShippingAddress,
            CustomerNote = order.CustomerNote,
            Items = order.Items.OrderBy(i => i.OrderItemId).Select(i => new OrderItemDto(
                i.OrderItemId,
                i.Variant!.ProductId,
                i.Variant.Product!.Slug,
                i.ProductNameSnapshot,
                i.SizeSnapshot,
                i.ColorSnapshot,
                i.ImageUrlSnapshot,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal,
                CanReview: completed && i.Review is null,
                i.Review?.ReviewId)).ToList(),
            VoucherCode = order.Voucher?.Code,
            SubtotalAmount = order.SubtotalAmount,
            DiscountAmount = order.DiscountAmount,
            ShippingFee = order.ShippingFee,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            PaymentExpiresAt = order.PaymentExpiresAt,
            PaidAt = order.PaidAt,
            CompletedAt = order.CompletedAt,
            CanceledAt = order.CanceledAt,
            CancelReason = order.CancelReason,
            CanCancel = order.CanCancel,
            CanPayWithQr = order.CanPayWithQr(now),
        };
    }

    private static void ValidateReason(CancelOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
        {
            throw new ValidationException([new ValidationFailure("reason", "A cancellation reason of at most 500 characters is required.")]);
        }
    }
}
