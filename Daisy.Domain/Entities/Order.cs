using Daisy.Domain.Common;
using Daisy.Domain.Enums;

namespace Daisy.Domain.Entities;

public class Order
{
    public const string ExpiredCancelReason = "Payment not received in time";

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public int? VoucherId { get; set; }

    public string ReceiverName { get; set; } = string.Empty;

    public string ReceiverPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public OrderStatus OrderStatus { get; private set; } = OrderStatus.Pending;

    public PaymentMethod PaymentMethod { get; private set; } = PaymentMethod.COD;

    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;

    public string? TransactionCode { get; private set; }

    public decimal SubtotalAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal ShippingFee { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string? CustomerNote { get; set; }

    public string? CancelReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>VietQR payment deadline (BR-41); null for COD.</summary>
    public DateTime? PaymentExpiresAt { get; private set; }

    public DateTime? PaidAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime? CanceledAt { get; private set; }

    public User? User { get; set; }

    public Voucher? Voucher { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    /// <summary>
    /// Creates a new order with the amounts calculated by the backend (BR-24).
    /// The receiver and address are copied as a snapshot (BR-28).
    /// </summary>
    public static Order Place(
        int userId,
        string orderCode,
        UserAddress address,
        PaymentMethod paymentMethod,
        int? voucherId,
        decimal subtotal,
        decimal discount,
        decimal shippingFee,
        string? customerNote,
        DateTime now,
        TimeSpan paymentTimeout)
    {
        var total = subtotal - discount + shippingFee;
        if (subtotal < 0 || discount < 0 || shippingFee < 0 || discount > subtotal || total < 0)
        {
            throw new InvalidOperationException("Invalid order amounts.");
        }

        return new Order
        {
            UserId = userId,
            OrderCode = orderCode,
            VoucherId = voucherId,
            ReceiverName = address.ReceiverName,
            ReceiverPhone = address.ReceiverPhone,
            ShippingAddress = address.FullAddress,
            OrderStatus = OrderStatus.Pending,
            PaymentMethod = paymentMethod,
            PaymentStatus = PaymentStatus.Unpaid,
            SubtotalAmount = subtotal,
            DiscountAmount = discount,
            ShippingFee = shippingFee,
            TotalAmount = total,
            CustomerNote = customerNote,
            CreatedAt = now,
            PaymentExpiresAt = paymentMethod == PaymentMethod.Online ? now.Add(paymentTimeout) : null,
        };
    }

    public bool CanCancel => OrderStatus is OrderStatus.Pending or OrderStatus.Confirmed;

    /// <summary>Cancel only Pending / Confirmed orders (BR-36).</summary>
    public void EnsureCancelable()
    {
        if (!CanCancel)
        {
            throw new DomainException(ErrorCodes.OrderNotCancelable, "This order can no longer be canceled.");
        }
    }

    /// <summary>The next statuses staff may set (BR-35); cancel has its own action.</summary>
    public IReadOnlyList<OrderStatus> AllowedNextStatuses() => OrderStatus switch
    {
        OrderStatus.Pending when PaymentMethod == PaymentMethod.Online && PaymentStatus != PaymentStatus.Paid
            => Array.Empty<OrderStatus>(),
        OrderStatus.Pending => new[] { OrderStatus.Confirmed },
        OrderStatus.Confirmed => new[] { OrderStatus.Shipping },
        OrderStatus.Shipping => new[] { OrderStatus.Completed },
        _ => Array.Empty<OrderStatus>(),
    };

    public void EnsureCanMoveTo(OrderStatus next)
    {
        if (OrderStatus == OrderStatus.Pending
            && next == OrderStatus.Confirmed
            && PaymentMethod == PaymentMethod.Online
            && PaymentStatus != PaymentStatus.Paid)
        {
            throw new DomainException(ErrorCodes.OrderNotPaid, "Wait until the VietQR payment is received before confirming this order.");
        }

        if (!AllowedNextStatuses().Contains(next))
        {
            throw new DomainException(
                ErrorCodes.InvalidStatusTransition,
                $"The order cannot move from {OrderStatus} to {next}.");
        }
    }

    /// <summary>The VietQR code is shown while the order is Online, Unpaid, not canceled and before its deadline (BR-41).</summary>
    public bool CanPayWithQr(DateTime now) =>
        PaymentMethod == PaymentMethod.Online
        && PaymentStatus == PaymentStatus.Unpaid
        && OrderStatus != OrderStatus.Canceled
        && (PaymentExpiresAt is null || now <= PaymentExpiresAt);

    /// <summary>A transfer can be applied to this order (BR-32, BR-42).</summary>
    public bool CanAcceptTransfer =>
        PaymentMethod == PaymentMethod.Online
        && PaymentStatus == PaymentStatus.Unpaid
        && OrderStatus != OrderStatus.Canceled;
}
