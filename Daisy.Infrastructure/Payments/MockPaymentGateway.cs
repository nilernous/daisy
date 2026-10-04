using System.Globalization;
using System.Text.Json;
using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daisy.Infrastructure.Payments;

/// <summary>
/// Development / demo gateway. It uses the same VietQR codes and the same SePay webhook format,
/// so a simulated transfer goes through exactly the same verification and matching code.
/// </summary>
public sealed class MockPaymentGateway : SePayPaymentGateway, IPaymentSimulator
{
    private readonly TimeProvider _time;

    public MockPaymentGateway(IOptions<VietQrOptions> options, ILogger<MockPaymentGateway> logger, TimeProvider time)
        : base(options.Value, logger)
    {
        _time = time;
    }

    public override string ProviderName => "Mock";

    public PaymentWebhookRequest BuildWebhook(string reference, decimal amount, string content)
    {
        var body = JsonSerializer.Serialize(
            new
            {
                id = _time.GetUtcNow().ToUnixTimeMilliseconds(),
                gateway = Options.BankName,
                transactionDate = _time.GetVietnamNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                accountNumber = Options.AccountNumber,
                content,
                transferType = "in",
                transferAmount = amount,
                referenceCode = reference,
            },
            JsonOptions);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = $"Apikey {Options.WebhookApiKey}",
        };
        return new PaymentWebhookRequest(headers, body);
    }
}
