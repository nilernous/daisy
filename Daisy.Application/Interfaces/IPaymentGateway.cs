namespace Daisy.Application.Interfaces;

public sealed record PaymentRequest(string OrderCode, decimal Amount);

/// <summary>VietQR transfer information shown to the customer.</summary>
public sealed record PaymentQrResult(
    string BankBin,
    string BankName,
    string AccountNumber,
    string AccountName,
    decimal Amount,
    string TransferContent,
    string QrImageUrl);

/// <summary>The raw webhook call: headers and body as received.</summary>
public sealed record PaymentWebhookRequest(
    IReadOnlyDictionary<string, string> Headers,
    string Body);

/// <summary>A webhook transaction normalized for Daisy (API_DESIGN §6.8).</summary>
public sealed record BankTransfer(
    string Reference,
    decimal Amount,
    string? Content,
    DateTime TransferredAt,
    string AccountNumber,
    bool IsIncoming);

public sealed record PaymentResult(bool IsValid, BankTransfer? Transfer)
{
    public static PaymentResult Invalid { get; } = new(false, null);
}

/// <summary>
/// VietQR payment provider. The Application project does not know whether the
/// implementation is the SePay webhook service or the mock used in development.
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    /// <summary>Builds the VietQR transfer info (QR image, account, amount, transfer content).</summary>
    Task<PaymentQrResult> CreatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies a webhook reporting an incoming bank transfer.</summary>
    Task<PaymentResult> VerifyWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default);
}
