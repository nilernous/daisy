namespace Daisy.Application.Common;

/// <summary>Configuration section "Checkout" (API_DESIGN §11).</summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    /// <summary>Flat shipping fee in VND.</summary>
    public decimal ShippingFee { get; set; } = 30000;

    /// <summary>Optional: subtotal from which shipping is free.</summary>
    public decimal? FreeShippingThreshold { get; set; }

    public decimal CalculateShippingFee(decimal subtotal) =>
        FreeShippingThreshold is not null && subtotal >= FreeShippingThreshold ? 0 : ShippingFee;
}

/// <summary>Payment deadline settings from the "VietQr" section (BR-41).</summary>
public sealed class PaymentDeadlineOptions
{
    public const string SectionName = "VietQr";

    public int PaymentTimeoutMinutes { get; set; } = 30;

    public int ExpiryGraceMinutes { get; set; } = 5;

    public TimeSpan PaymentTimeout => TimeSpan.FromMinutes(PaymentTimeoutMinutes);

    public TimeSpan ExpiryGrace => TimeSpan.FromMinutes(ExpiryGraceMinutes);
}

/// <summary>Configuration section "PasswordReset" (BR-05).</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int TokenMinutes { get; set; } = 30;

    /// <summary>MVC page that receives the token, e.g. https://localhost:7002/account/reset-password.</summary>
    public string ResetUrl { get; set; } = string.Empty;
}

/// <summary>Upload limits for images (API_DESIGN §6.2, §6.11).</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Storage";

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public long MaxAvatarBytes { get; set; } = 2 * 1024 * 1024;

    public int MaxFilesPerUpload { get; set; } = 10;
}
