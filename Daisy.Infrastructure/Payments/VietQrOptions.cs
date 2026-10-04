namespace Daisy.Infrastructure.Payments;

/// <summary>
/// Configuration section "VietQr": receiving account, webhook key and provider (ARCHITECTURE §17).
/// PaymentTimeoutMinutes / ExpiryGraceMinutes are read by the Application (PaymentDeadlineOptions).
/// </summary>
public sealed class VietQrOptions
{
    public const string SectionName = "VietQr";

    /// <summary>SePay (production) or Mock (development / demo).</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Receiving bank BIN, e.g. 970436 for Vietcombank.</summary>
    public string BankBin { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    /// <summary>img.vietqr.io template, e.g. compact2.</summary>
    public string QrTemplate { get; set; } = "compact2";

    /// <summary>Key the webhook service sends as "Authorization: Apikey {key}".</summary>
    public string WebhookApiKey { get; set; } = string.Empty;

    public bool IsMock => string.Equals(Provider, "Mock", StringComparison.OrdinalIgnoreCase);
}
