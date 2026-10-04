using System.ComponentModel.DataAnnotations;
using Daisy.Mvc.Models.Api;

namespace Daisy.Mvc.ViewModels;

// ---- Home and catalog ---------------------------------------------------------------------

public sealed record HomeViewModel(
    IReadOnlyList<ProductListItem> FlashSale,
    DateTimeOffset? FlashSaleEndsAt,
    IReadOnlyList<ProductListItem> Featured,
    IReadOnlyList<ProductListItem> NewArrivals,
    IReadOnlyList<ProductListItem> BestSellers,
    IReadOnlyList<CategoryNode> Categories);

public sealed class ProductFilter
{
    public string? Q { get; set; }

    public string? Category { get; set; }

    public bool Sale { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public string? Size { get; set; }

    public string? Color { get; set; }

    public string? Gender { get; set; }

    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    /// <summary>Query string with one value changed, for filter and sort links.</summary>
    public string With(string? key = null, string? value = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["category"] = Category,
            ["sale"] = Sale ? "true" : null,
            ["min"] = Min?.ToString("0"),
            ["max"] = Max?.ToString("0"),
            ["size"] = Size,
            ["color"] = Color,
            ["gender"] = Gender,
            ["sort"] = Sort,
        };
        if (key is not null)
        {
            values[key] = value;
            values["page"] = null;
        }
        else if (Page > 1)
        {
            values["page"] = Page.ToString();
        }

        var parts = values.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}");
        return "/products?" + string.Join('&', parts);
    }

    public string PageUrl(int page)
    {
        var url = With();
        var withoutPage = System.Text.RegularExpressions.Regex.Replace(url, "&?page=\\d+", string.Empty);
        return withoutPage + (withoutPage.EndsWith('?') ? string.Empty : "&") + "page=" + page;
    }
}

public sealed record ProductListViewModel(
    ProductFilter Filter,
    IReadOnlyList<ProductListItem> Products,
    int TotalCount,
    int TotalPages,
    string Heading,
    CategoryNode? Category,
    IReadOnlyList<CategoryNode> Categories);

public sealed record ProductDetailViewModel(ProductDetail Product, ProductReviews Reviews, int? RatingFilter, bool CanWishlist);

// ---- Cart and checkout ----------------------------------------------------------------------------

public sealed record CheckoutViewModel(Checkout Checkout, CheckoutTotals Totals, string? VoucherCode, string? VoucherError, PlaceOrderForm Form);

public sealed class PlaceOrderForm
{
    [Required(ErrorMessage = "Chọn địa chỉ nhận hàng.")]
    public int? AddressId { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

    public string? VoucherCode { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? CustomerNote { get; set; }

    public decimal ExpectedTotal { get; set; }
}

// ---- Orders ------------------------------------------------------------------------------------------

public sealed record OrdersViewModel(PagedResult<OrderSummary> Orders, OrderStatus? Status);

public sealed record PaymentViewModel(OrderDetail Order, PaymentQr Qr, bool CanSimulate);

// ---- Account -----------------------------------------------------------------------------------------

public sealed class LoginForm
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    public string Password { get; set; } = string.Empty;

    public bool Remember { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần 8–100 ký tự.")]
    [RegularExpression("^(?=.*[A-Za-zÀ-ỹ])(?=.*\\d).+$", ErrorMessage = "Mật khẩu cần có ít nhất một chữ và một số.")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordForm
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordForm
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần 8–100 ký tự.")]
    [RegularExpression("^(?=.*[A-Za-zÀ-ỹ])(?=.*\\d).+$", ErrorMessage = "Mật khẩu cần có ít nhất một chữ và một số.")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordForm
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần 8–100 ký tự.")]
    [RegularExpression("^(?=.*[A-Za-zÀ-ỹ])(?=.*\\d).+$", ErrorMessage = "Mật khẩu cần có ít nhất một chữ và một số.")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ---- Profile ------------------------------------------------------------------------------------------

public sealed class ProfileForm
{
    [Required(ErrorMessage = "Nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression("^(\\+84|0)\\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ 0901234567).")]
    public string? Phone { get; set; }

    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    public IFormFile? Avatar { get; set; }

    public bool RemoveAvatar { get; set; }
}

public sealed class AddressForm
{
    [Required(ErrorMessage = "Nhập tên người nhận.")]
    [StringLength(150)]
    public string ReceiverName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập số điện thoại.")]
    [RegularExpression("^(\\+84|0)\\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ 0901234567).")]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập tỉnh/thành phố.")]
    [StringLength(100)]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập quận/huyện.")]
    [StringLength(100)]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập phường/xã.")]
    [StringLength(100)]
    public string Ward { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập số nhà, tên đường.")]
    [StringLength(255)]
    public string StreetAddress { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? ReturnUrl { get; set; }

    public static AddressForm From(Address a) => new()
    {
        ReceiverName = a.ReceiverName,
        ReceiverPhone = a.ReceiverPhone,
        Province = a.Province,
        District = a.District,
        Ward = a.Ward,
        StreetAddress = a.StreetAddress,
        IsDefault = a.IsDefault,
    };

    public object ToRequest() => new { ReceiverName, ReceiverPhone, Province, District, Ward, StreetAddress, IsDefault };
}

public sealed record ProfileViewModel(Profile Profile, IReadOnlyList<Address> Addresses, ProfileForm Form, AddressForm NewAddress);
