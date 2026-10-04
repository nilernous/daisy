using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daisy.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daisy.Infrastructure.Payments;

/// <summary>
/// VietQR codes (NAPAS) plus the SePay bank webhook service.
/// SePay calls POST /api/payments/webhook with "Authorization: Apikey {key}" and a JSON body.
/// </summary>
public class SePayPaymentGateway : IPaymentGateway
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger _logger;

    public SePayPaymentGateway(IOptions<VietQrOptions> options, ILogger<SePayPaymentGateway> logger)
        : this(options.Value, logger)
    {
    }

    protected SePayPaymentGateway(VietQrOptions options, ILogger logger)
    {
        Options = options;
        _logger = logger;
    }

    public virtual string ProviderName => "SePay";

    protected VietQrOptions Options { get; }

    public Task<PaymentQrResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var amount = decimal.Round(request.Amount, 0).ToString(CultureInfo.InvariantCulture);
        var url = $"https://img.vietqr.io/image/{Uri.EscapeDataString(Options.BankBin)}-{Uri.EscapeDataString(Options.AccountNumber)}-{Uri.EscapeDataString(Options.QrTemplate)}.png"
            + $"?amount={amount}&addInfo={Uri.EscapeDataString(request.OrderCode)}&accountName={Uri.EscapeDataString(Options.AccountName)}";

        return Task.FromResult(new PaymentQrResult(
            Options.BankBin,
            Options.BankName,
            Options.AccountNumber,
            Options.AccountName,
            request.Amount,
            request.OrderCode,
            url));
    }

    public Task<PaymentResult> VerifyWebhookAsync(PaymentWebhookRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasValidApiKey(request.Headers))
        {
            return Task.FromResult(PaymentResult.Invalid);
        }

        SePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayload>(request.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The webhook body is not valid JSON.");
            return Task.FromResult(new PaymentResult(true, null));
        }

        if (payload is null || payload.TransferAmount <= 0)
        {
            return Task.FromResult(new PaymentResult(true, null));
        }

        if (!string.Equals(payload.AccountNumber?.Trim(), Options.AccountNumber, StringComparison.Ordinal))
        {
            _logger.LogWarning("Ignoring a transfer to account {Account}, which is not the receiving account.", payload.AccountNumber);
            return Task.FromResult(new PaymentResult(true, null));
        }

        var reference = string.IsNullOrWhiteSpace(payload.ReferenceCode) ? $"SEPAY-{payload.Id}" : payload.ReferenceCode.Trim();
        var transferredAt = DateTime.TryParseExact(payload.TransactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : Application.Common.VietnamTime.ToVietnamTime(DateTimeOffset.UtcNow);

        var transfer = new BankTransfer(
            reference,
            payload.TransferAmount,
            payload.Content,
            transferredAt,
            payload.AccountNumber!.Trim(),
            string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new PaymentResult(true, transfer));
    }

    /// <summary>Constant-time comparison of "Apikey {key}" (API_DESIGN §6.8).</summary>
    private bool HasValidApiKey(IReadOnlyDictionary<string, string> headers)
    {
        if (string.IsNullOrEmpty(Options.WebhookApiKey)
            || !headers.TryGetValue("Authorization", out var header)
            || !header.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var provided = Encoding.UTF8.GetBytes(header["Apikey ".Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(Options.WebhookApiKey);
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    /// <summary>The body SePay sends for each transaction.</summary>
    protected sealed class SePayWebhookPayload
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("gateway")]
        public string? Gateway { get; set; }

        [JsonPropertyName("transactionDate")]
        public string? TransactionDate { get; set; }

        [JsonPropertyName("accountNumber")]
        public string? AccountNumber { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("transferType")]
        public string? TransferType { get; set; }

        [JsonPropertyName("transferAmount")]
        public decimal TransferAmount { get; set; }

        [JsonPropertyName("referenceCode")]
        public string? ReferenceCode { get; set; }
    }
}
