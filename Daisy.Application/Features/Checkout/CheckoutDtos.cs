using Daisy.Application.Features.Profile;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Checkout;

public sealed record CheckoutItemDto(
    int CartItemId,
    int VariantId,
    string ProductName,
    string? Size,
    string? Color,
    string? ThumbnailUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record VoucherInfoDto(
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount)
{
    public static VoucherInfoDto From(Voucher v) => new(v.Code, v.Name, v.DiscountType, v.DiscountValue, v.MaxDiscountAmount);
}

public sealed record CheckoutTotalsDto(
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal TotalAmount,
    VoucherInfoDto? Voucher);

public sealed record CheckoutDto(
    IReadOnlyList<CheckoutItemDto> Items,
    IReadOnlyList<AddressDto> Addresses,
    int? DefaultAddressId,
    IReadOnlyList<PaymentMethod> PaymentMethods,
    CheckoutTotalsDto Totals,
    bool CanPlaceOrder);

public sealed record ApplyVoucherRequest(string Code);

public sealed record PlaceOrderRequest(
    int AddressId,
    PaymentMethod PaymentMethod,
    string? VoucherCode,
    string? CustomerNote,
    decimal ExpectedTotal);

public sealed record PlaceOrderResultDto(
    int OrderId,
    string OrderCode,
    OrderStatus OrderStatus,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    DateTime? PaymentExpiresAt,
    string? PaymentQrUrl);

public sealed record UnavailableItemDto(int CartItemId, string Reason, int AvailableStock);
