using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Orders;
using Daisy.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-35, UC-37 – UC-40. Customers see only their own orders (BR-08).</summary>
[Route("api/orders")]
[Authorize(Policy = Policies.Customer)]
public sealed class OrdersController : ApiControllerBase
{
    private readonly OrderService _orders;
    private readonly PaymentService _payments;

    public OrdersController(OrderService orders, PaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetMyOrders([FromQuery] OrderListQuery query, CancellationToken ct) =>
        await _orders.GetMyOrdersAsync(CurrentUserId, query, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> GetMyOrder(int id, CancellationToken ct) =>
        await _orders.GetMyOrderAsync(CurrentUserId, id, ct);

    [HttpGet("{id:int}/payment-qr")]
    public async Task<ActionResult<PaymentQrDto>> GetPaymentQr(int id, CancellationToken ct) =>
        await _payments.GetPaymentQrAsync(CurrentUserId, id, ct);

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<CanceledOrderDto>> CancelMyOrder(int id, CancelOrderRequest request, CancellationToken ct) =>
        await _orders.CancelMyOrderAsync(CurrentUserId, id, request, ct);
}
