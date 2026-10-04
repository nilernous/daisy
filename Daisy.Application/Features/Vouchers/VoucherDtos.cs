using Daisy.Application.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Vouchers;

public sealed record VoucherDto(
    int Id,
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    int UsedCount,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive,
    string State)
{
    public static VoucherDto From(Voucher v, DateTime now) =>
        new(v.VoucherId, v.Code, v.Name, v.DiscountType, v.DiscountValue, v.MaxDiscountAmount, v.MinOrderAmount,
            v.UsageLimit, v.UsedCount, v.StartAt, v.EndAt, v.IsActive, v.GetState(now));
}

public sealed record VoucherRequest(
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive = true);

public sealed record VoucherQuery : PageRequest
{
    public string? Search { get; init; }

    public bool? IsActive { get; init; }

    /// <summary>Upcoming, Running or Expired.</summary>
    public string? State { get; init; }
}

public sealed record VoucherOrderDto(
    int OrderId,
    string OrderCode,
    string CustomerName,
    decimal DiscountAmount,
    decimal TotalAmount,
    OrderStatus OrderStatus,
    DateTime CreatedAt);

public sealed record VoucherUsageDto(
    VoucherDto Voucher,
    decimal TotalDiscountAmount,
    PagedResult<VoucherOrderDto> Orders);
