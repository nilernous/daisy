using Daisy.Domain.Enums;

namespace Daisy.Domain.Entities;

public class Voucher
{
    public int VoucherId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinOrderAmount { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>
    /// Returns why the voucher cannot be applied (BR-26), or null when it is eligible.
    /// </summary>
    public string? GetIneligibilityReason(decimal subtotal, DateTime now)
    {
        if (!IsActive)
        {
            return VoucherIneligibility.Inactive;
        }

        if (now < StartAt)
        {
            return VoucherIneligibility.NotStarted;
        }

        if (now > EndAt)
        {
            return VoucherIneligibility.Expired;
        }

        if (UsageLimit is not null && UsedCount >= UsageLimit)
        {
            return VoucherIneligibility.UsageLimitReached;
        }

        if (subtotal < MinOrderAmount)
        {
            return VoucherIneligibility.BelowMinimumOrder;
        }

        return null;
    }

    /// <summary>
    /// Discount from the subtotal (BR-27): Percent is capped at MaxDiscountAmount,
    /// and the discount never exceeds the subtotal. Amounts are whole VND.
    /// </summary>
    public decimal CalculateDiscount(decimal subtotal)
    {
        var discount = DiscountType == DiscountType.Percent
            ? Math.Floor(subtotal * DiscountValue / 100m)
            : DiscountValue;

        if (DiscountType == DiscountType.Percent && MaxDiscountAmount is not null)
        {
            discount = Math.Min(discount, MaxDiscountAmount.Value);
        }

        return Math.Max(0, Math.Min(discount, subtotal));
    }

    public string GetState(DateTime now) =>
        now < StartAt ? VoucherState.Upcoming
        : now > EndAt ? VoucherState.Expired
        : VoucherState.Running;
}

public static class VoucherIneligibility
{
    public const string Inactive = "Inactive";
    public const string NotStarted = "NotStarted";
    public const string Expired = "Expired";
    public const string BelowMinimumOrder = "BelowMinimumOrder";
    public const string UsageLimitReached = "UsageLimitReached";
}

public static class VoucherState
{
    public const string Upcoming = "Upcoming";
    public const string Running = "Running";
    public const string Expired = "Expired";
}
