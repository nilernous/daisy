using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Daisy.Api.Controllers;

/// <summary>UC-32 – UC-35.</summary>
[Route("api/checkout")]
[Authorize(Policy = Policies.Customer)]
public sealed class CheckoutController : ApiControllerBase
{
    private readonly CheckoutService _checkout;

    public CheckoutController(CheckoutService checkout)
    {
        _checkout = checkout;
    }

    [HttpGet]
    public async Task<ActionResult<CheckoutDto>> GetCheckout(CancellationToken ct) =>
        await _checkout.GetCheckoutAsync(CurrentUserId, ct);

    [HttpPost("voucher")]
    [EnableRateLimiting(RateLimits.Voucher)]
    public async Task<ActionResult<CheckoutTotalsDto>> PreviewVoucher(ApplyVoucherRequest request, CancellationToken ct) =>
        await _checkout.PreviewVoucherAsync(CurrentUserId, request, ct);

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await _checkout.PlaceOrderAsync(CurrentUserId, request, ct);
        return Created($"/api/orders/{result.OrderId}", result);
    }
}
