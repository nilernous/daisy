using Daisy.Application.Common;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Orders;

public sealed record OrderListQuery : PageRequest
{
    public OrderStatus? Status { get; init; }
}

public sealed record OrderSummaryDto(
    int Id,
    string OrderCode,
    DateTime CreatedAt,
    OrderStatus OrderStatus,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    int ItemCount,
    string? ThumbnailUrl);

public sealed record OrderItemDto(
    int OrderItemId,
    int ProductId,
    string ProductSlug,
    string ProductName,
    string? Size,
    string? Color,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool CanReview,
    int? ReviewId);

/// <summary>Order detail from snapshots (BR-28); used by the customer and, extended, by staff.</summary>
public class OrderDetailDto
{
    public int Id { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public OrderStatus OrderStatus { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public string ReceiverName { get; init; } = string.Empty;

    public string ReceiverPhone { get; init; } = string.Empty;

    public string ShippingAddress { get; init; } = string.Empty;

    public string? CustomerNote { get; init; }

    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];

    public string? VoucherCode { get; init; }

    public decimal SubtotalAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal TotalAmount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? PaymentExpiresAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? CanceledAt { get; init; }

    public string? CancelReason { get; init; }

    public bool CanCancel { get; init; }

    public bool CanPayWithQr { get; init; }
}

public sealed record OrderCustomerDto(int Id, string FullName, string Email, string? Phone);

public sealed class OrderAdminDetailDto : OrderDetailDto
{
    public OrderCustomerDto Customer { get; init; } = null!;

    public string? TransactionCode { get; init; }

    public IReadOnlyList<OrderStatus> AllowedStatusTransitions { get; init; } = [];

    public IReadOnlyList<string> AllowedPaymentActions { get; init; } = [];
}

/// <summary>OData entity of /odata/orders (API_DESIGN §7.2).</summary>
public sealed class OrderAdminListDto
{
    public int Id { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public int CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerEmail { get; init; } = string.Empty;

    public string ReceiverPhone { get; init; } = string.Empty;

    public OrderStatus OrderStatus { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public decimal TotalAmount { get; init; }

    public int ItemCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? PaidAt { get; init; }
}

public sealed record CancelOrderRequest(string Reason);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);

public sealed record CanceledOrderDto(
    int Id,
    string OrderCode,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    DateTime? CanceledAt,
    bool RefundRequired);
