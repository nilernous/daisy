using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Payments;
using Daisy.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-36. Called by the bank webhook service; authenticated by its API key, not a JWT.</summary>
[Route("api/payments")]
[AllowAnonymous]
public sealed class PaymentsController : ApiControllerBase
{
    private readonly PaymentService _payments;

    public PaymentsController(PaymentService payments)
    {
        _payments = payments;
    }

    /// <summary>Returns 200 for every verified call, including duplicates and unmatched transfers.</summary>
    [HttpPost("webhook")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<WebhookAckDto>> ReceiveWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return await _payments.HandleWebhookAsync(new PaymentWebhookRequest(headers, body), ct);
    }
}

/// <summary>
/// Development only, with VietQr:Provider = Mock: simulates a bank transfer so the VietQR flow can be
/// tried without a real bank. Returns 404 in every other environment.
/// </summary>
[Route("api/dev/payments")]
[Authorize(Policy = Policies.Authenticated)]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevPaymentsController : ApiControllerBase
{
    private readonly PaymentService _payments;
    private readonly IHostEnvironment _environment;

    public DevPaymentsController(PaymentService payments, IHostEnvironment environment)
    {
        _payments = payments;
        _environment = environment;
    }

    [HttpPost("simulate-transfer")]
    public async Task<IActionResult> SimulateTransfer(SimulateTransferRequest request, CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(await _payments.SimulateTransferAsync(request, ct));
    }
}
