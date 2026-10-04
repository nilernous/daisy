namespace Daisy.Application.Interfaces;

/// <summary>
/// Builds a webhook call that the mock gateway accepts, so a transfer can be simulated
/// in development. Only registered when VietQr:Provider is Mock.
/// </summary>
public interface IPaymentSimulator
{
    PaymentWebhookRequest BuildWebhook(string reference, decimal amount, string content);
}
