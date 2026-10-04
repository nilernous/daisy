using System.Globalization;
using Daisy.Mvc.Models.Api;

namespace Daisy.Mvc.Infrastructure;

/// <summary>Vietnamese labels and formats used by the views.</summary>
public static class Display
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string Money(decimal value) => value.ToString("#,0", Vi) + "đ";

    public static string Money(decimal? value) => value is null ? "—" : Money(value.Value);

    public static string Number(decimal value) => value.ToString("#,0", Vi);

    public static DateTimeOffset InVietnam(DateTimeOffset value) => value.ToOffset(VietnamOffset);

    public static string DateTime(DateTimeOffset? value) =>
        value is null ? "—" : InVietnam(value.Value).ToString("HH:mm, dd/MM/yyyy", Vi);

    public static string Date(DateTimeOffset? value) =>
        value is null ? "—" : InVietnam(value.Value).ToString("dd/MM/yyyy", Vi);

    public static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", Vi) ?? "—";

    /// <summary>Value for &lt;input type="datetime-local"&gt;, in Vietnam time.</summary>
    public static string InputDateTime(DateTimeOffset? value) =>
        value is null ? string.Empty : InVietnam(value.Value).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Reads &lt;input type="datetime-local"&gt; as Vietnam time.</summary>
    public static DateTimeOffset? FromInput(DateTime? local) =>
        local is null ? null : new DateTimeOffset(System.DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), VietnamOffset);

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Canceled => "Đã hủy",
        _ => status.ToString(),
    };

    public static string Label(PaymentStatus status) => status switch
    {
        PaymentStatus.Unpaid => "Chưa thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Failed => "Thanh toán lỗi",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        _ => status.ToString(),
    };

    public static string Label(PaymentMethod method) => method switch
    {
        PaymentMethod.COD => "Thanh toán khi nhận hàng",
        PaymentMethod.Online => "Chuyển khoản VietQR",
        _ => method.ToString(),
    };

    public static string Label(UserRole role) => role switch
    {
        UserRole.Customer => "Khách hàng",
        UserRole.Staff => "Nhân viên",
        UserRole.Admin => "Quản trị",
        _ => role.ToString(),
    };

    public static string Label(UserStatus status) => status == UserStatus.Active ? "Đang hoạt động" : "Đã khóa";

    public static string Label(PaymentMatchResult result) => result switch
    {
        PaymentMatchResult.Matched => "Đã khớp đơn",
        PaymentMatchResult.NoOrder => "Không tìm thấy đơn",
        PaymentMatchResult.Underpaid => "Chuyển thiếu tiền",
        PaymentMatchResult.OrderNotPayable => "Đơn không nhận thanh toán",
        _ => result.ToString(),
    };

    public static string Label(PaymentResolution? resolution) => resolution switch
    {
        PaymentResolution.Applied => "Đã gán vào đơn",
        PaymentResolution.Refunded => "Đã hoàn tiền",
        PaymentResolution.Ignored => "Bỏ qua",
        null => "Chờ xử lý",
        _ => resolution.ToString()!,
    };

    public static string PaymentActionLabel(string action) => action switch
    {
        "ConfirmCod" => "Xác nhận đã thu tiền COD",
        "ConfirmTransfer" => "Xác nhận chuyển khoản thủ công",
        "Refund" => "Đánh dấu đã hoàn tiền",
        _ => action,
    };

    public static string VoucherState(string state) => state switch
    {
        "Upcoming" => "Sắp diễn ra",
        "Running" => "Đang chạy",
        "Expired" => "Đã hết hạn",
        _ => state,
    };

    public static string Gender(string? value) => value switch
    {
        "Male" => "Nam",
        "Female" => "Nữ",
        "Other" => "Khác",
        _ => "—",
    };

    public static string GenderTarget(string? value) => value switch
    {
        "Men" => "Nam",
        "Women" => "Nữ",
        "Unisex" => "Unisex",
        "Kids" => "Trẻ em",
        _ => "—",
    };

    public static string Discount(DiscountType type, decimal value, decimal? max) =>
        type == DiscountType.Percent
            ? $"Giảm {Number(value)}%" + (max is null ? string.Empty : $", tối đa {Money(max.Value)}")
            : $"Giảm {Money(value)}";

    /// <summary>CSS modifier for status pills.</summary>
    public static string Tone(OrderStatus status) => status switch
    {
        OrderStatus.Completed => "good",
        OrderStatus.Canceled => "bad",
        OrderStatus.Shipping => "info",
        OrderStatus.Confirmed => "info",
        _ => "wait",
    };

    public static string Tone(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "good",
        PaymentStatus.Refunded => "muted",
        PaymentStatus.Failed => "bad",
        _ => "wait",
    };

    /// <summary>First letters for the image placeholder, e.g. "Áo thun cotton" → "ÁT".</summary>
    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0], Vi)));
}
