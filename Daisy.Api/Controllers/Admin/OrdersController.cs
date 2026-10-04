using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Orders;
using Daisy.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-54 – UC-57. The order list is GET /odata/orders (UC-53).</summary>
[Route("api/admin/orders")]
[Authorize(Policy = Policies.Staff)]
public sealed class OrdersController : ApiControllerBase
{
    private readonly OrderService _orders;
    private readonly PaymentService _payments;

    public OrdersController(OrderService orders, PaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderAdminDetailDto>> GetAdminOrder(int id, CancellationToken ct) =>
        await _orders.GetAdminDetailAsync(id, ct);

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        await _orders.UpdateStatusAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<CanceledOrderDto>> CancelOrder(int id, CancelOrderRequest request, CancellationToken ct) =>
        await _orders.CancelByStaffAsync(id, request, ct);

    [HttpPatch("{id:int}/payment")]
    public async Task<IActionResult> UpdatePayment(int id, UpdatePaymentRequest request, CancellationToken ct)
    {
        await _payments.UpdatePaymentAsync(id, request, ct);
        return NoContent();
    }
}
